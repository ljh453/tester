import sys

from embsw_tester.adapters import AdapterContext
from embsw_tester.adapters.serial_factory import create_adapter_registry_from_tool_profile
from embsw_tester.adapters.trace32_factory import create_trace32_adapter_from_profile


class FakeRclClient:
    def __init__(self, response):
        self.response = response
        self.commands = []

    def command(self, command):
        self.commands.append(command)
        return self.response

    def cmd(self, command):
        self.commands.append(command)
        return self.response


class FakeUdpSocket:
    def __init__(self, response=b"STATE:HALTED\n"):
        self.response = response
        self.timeout = None
        self.connected_to = None
        self.sent_payloads = []
        self.closed = False

    def settimeout(self, timeout):
        self.timeout = timeout

    def connect(self, address):
        self.connected_to = address

    def sendall(self, payload):
        self.sent_payloads.append(payload)

    def recv(self, response_bytes):
        return self.response

    def close(self):
        self.closed = True


def test_create_trace32_adapter_from_profile_builds_rcl_and_udp_transports():
    profile = {
        "trace32": {
            "rcl": {
                "enabled": True,
                "command_method": "command",
                "client_args": {"node": "TRACE32-A"},
            },
            "udp": {
                "enabled": True,
                "host": "127.0.0.1",
                "port": 20000,
            },
        }
    }
    factory_configs = []
    sockets = []

    def rcl_client_factory(config):
        factory_configs.append(dict(config))
        return FakeRclClient(
            {
                "success": False,
                "status": "failed",
                "message": "rcl offline",
            }
        )

    def udp_socket_factory(*args, **kwargs):
        socket = FakeUdpSocket()
        sockets.append(socket)
        return socket

    adapter = create_trace32_adapter_from_profile(
        profile,
        rcl_client_factory=rcl_client_factory,
        udp_socket_factory=udp_socket_factory,
    )

    result = adapter.execute(
        "trace32.command",
        {"command": "STATE()", "timeout_ms": 2500},
        AdapterContext(run_id="trace32-factory", testcase="fallback_case", phase="steps"),
    )

    assert result.success is True
    assert result.values["transport"] == "udp"
    assert result.values["fallback_used"] is True
    assert result.values["value"] == "STATE:HALTED"
    assert result.values["attempts"][0]["transport"] == "rcl"
    assert result.values["attempts"][1]["transport"] == "udp"
    assert factory_configs[0]["command_method"] == "command"
    assert factory_configs[0]["client_args"] == {"node": "TRACE32-A"}
    assert sockets[0].connected_to == ("127.0.0.1", 20000)
    assert sockets[0].sent_payloads == [b"STATE()\n"]


def test_create_trace32_adapter_from_profile_loads_import_path_rcl_factory(tmp_path, monkeypatch):
    module_file = tmp_path / "fake_trace32_lab.py"
    module_file.write_text(
        """
class Client:
    def __init__(self, response):
        self.response = response

    def cmd(self, command):
        return self.response


def create_client(response):
    return Client(response)
""".strip(),
        encoding="utf-8",
    )
    monkeypatch.syspath_prepend(str(tmp_path))
    sys.modules.pop("fake_trace32_lab", None)
    profile = {
        "trace32": {
            "rcl": {
                "enabled": True,
                "client_factory": "fake_trace32_lab:create_client",
                "client_args": {"response": "VERSION OK"},
            }
        }
    }

    adapter = create_trace32_adapter_from_profile(profile)

    result = adapter.execute(
        "trace32.command",
        {"command": "VERSION()", "fallback": False},
        AdapterContext(run_id="trace32-factory", testcase="rcl_case", phase="steps"),
    )

    assert result.success is True
    assert result.values["transport"] == "rcl"
    assert result.values["value"] == "VERSION OK"


def test_create_adapter_registry_from_tool_profile_replaces_trace32_adapter(tmp_path):
    profile = {
        "trace32": {
            "rcl": {
                "enabled": True,
                "command_method": "cmd",
            }
        }
    }

    registry = create_adapter_registry_from_tool_profile(
        profile,
        evidence_root=tmp_path,
        trace32_rcl_client_factory=lambda config: FakeRclClient("TRACE32 READY"),
    )

    result = registry.get("trace32").execute(
        "trace32.command",
        {"command": "VERSION()", "fallback": False},
        AdapterContext(run_id="registry-run", testcase="trace32_case", phase="steps"),
    )

    assert result.success is True
    assert result.values["transport"] == "rcl"
    assert result.values["value"] == "TRACE32 READY"
