import json

from embsw_tester.adapters import AdapterContext
from embsw_tester.adapters.canoe_factory import create_canoe_adapter_from_profile
from embsw_tester.adapters.serial_factory import create_adapter_registry_from_tool_profile


class FakeStreamWriter:
    def __init__(self):
        self.writes = []
        self.flush_count = 0

    def write(self, text):
        self.writes.append(text)

    def flush(self):
        self.flush_count += 1


class FakeStreamReader:
    def __init__(self, lines):
        self.lines = list(lines)

    def readline(self):
        if not self.lines:
            return ""
        return self.lines.pop(0)


class FakeProcess:
    def __init__(self, stdout_lines):
        self.stdin = FakeStreamWriter()
        self.stdout = FakeStreamReader(stdout_lines)
        self.stderr = FakeStreamReader([])


def test_create_canoe_adapter_from_profile_builds_helper_bridge_adapter():
    process = _helper_process("req-1", value=900)
    popen_calls = []

    def popen_factory(command, **kwargs):
        popen_calls.append((command, kwargs))
        return process

    adapter = create_canoe_adapter_from_profile(
        {
            "canoe": {
                "helper": {
                    "enabled": True,
                    "application": "canalyzer",
                    "command": [
                        "C:/Python311/python.exe",
                        "-m",
                        "embsw_tester.adapters.canoe_com_helper",
                    ],
                }
            }
        },
        popen_factory=popen_factory,
        request_id_factory=lambda: "req-1",
    )

    result = adapter.execute(
        "canoe.signal.read",
        {
            "bus": "CAN",
            "channel": 1,
            "message": "ABSData",
            "signal": "CarSpeed",
            "timeout_ms": 2500,
        },
        AdapterContext(run_id="canoe-factory", testcase="case", phase="steps"),
    )

    request = json.loads(process.stdin.writes[0])
    assert popen_calls[0][0] == [
        "C:/Python311/python.exe",
        "-m",
        "embsw_tester.adapters.canoe_com_helper",
        "--application",
        "canalyzer",
    ]
    assert request["request_id"] == "req-1"
    assert request["command_type"] == "canoe.signal.read"
    assert request["args"] == {
        "bus": "CAN",
        "channel": 1,
        "message": "ABSData",
        "signal": "CarSpeed",
    }
    assert request["timeout_ms"] == 2500
    assert result.success is True
    assert result.values["value"] == 900


def test_create_adapter_registry_from_tool_profile_replaces_canoe_adapter(tmp_path):
    process = _helper_process("req-2", value=875)

    registry = create_adapter_registry_from_tool_profile(
        {
            "canoe": {
                "helper": {
                    "enabled": True,
                    "command": [
                        "C:/Python311/python.exe",
                        "-m",
                        "embsw_tester.adapters.canoe_com_helper",
                    ],
                }
            }
        },
        evidence_root=tmp_path,
        canoe_popen_factory=lambda command, **kwargs: process,
        canoe_request_id_factory=lambda: "req-2",
    )

    result = registry.get("canoe").execute(
        "canoe.sysvar.read",
        {"namespace": "Vehicle", "name": "Ignition", "timeout_ms": 1000},
        AdapterContext(run_id="registry-run", testcase="case", phase="steps"),
    )

    request = json.loads(process.stdin.writes[0])
    assert request["request_id"] == "req-2"
    assert result.success is True
    assert result.values["value"] == 875


def _helper_process(request_id, value):
    return FakeProcess(
        [
            json.dumps(
                {
                    "request_id": request_id,
                    "success": True,
                    "status": "passed",
                    "message": "read",
                    "values": {"value": value},
                }
            )
            + "\n"
        ]
    )
