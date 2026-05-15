import json

from embsw_tester.adapters import AdapterContext
from embsw_tester.adapters.inca_factory import create_inca_adapter_from_profile
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


def test_create_inca_adapter_from_profile_builds_helper_bridge_adapter():
    process = _helper_process("req-1", value=900)
    popen_calls = []

    def popen_factory(command, **kwargs):
        popen_calls.append((command, kwargs))
        return process

    adapter = create_inca_adapter_from_profile(
        {
            "inca": {
                "helper": {
                    "enabled": True,
                    "command": ["C:/Python32/python.exe", "inca_helper.py"],
                }
            }
        },
        popen_factory=popen_factory,
        request_id_factory=lambda: "req-1",
    )

    result = adapter.execute(
        "inca.measure.read",
        {"variable": "EngineSpeed", "timeout_ms": 2500},
        AdapterContext(run_id="inca-factory", testcase="case", phase="steps"),
    )

    request = json.loads(process.stdin.writes[0])
    assert popen_calls[0][0] == ["C:/Python32/python.exe", "inca_helper.py"]
    assert request["request_id"] == "req-1"
    assert request["command_type"] == "inca.measure.read"
    assert request["args"] == {"variable": "EngineSpeed"}
    assert request["timeout_ms"] == 2500
    assert result.success is True
    assert result.values["value"] == 900


def test_create_adapter_registry_from_tool_profile_replaces_inca_adapter(tmp_path):
    process = _helper_process("req-2", value=875)

    registry = create_adapter_registry_from_tool_profile(
        {
            "inca": {
                "helper": {
                    "enabled": True,
                    "command": ["C:/Python32/python.exe", "inca_helper.py"],
                }
            }
        },
        evidence_root=tmp_path,
        inca_popen_factory=lambda command, **kwargs: process,
        inca_request_id_factory=lambda: "req-2",
    )

    result = registry.get("inca").execute(
        "inca.measure.read",
        {"variable": "EngineSpeed", "timeout_ms": 1000},
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
                    "values": {"variable": "EngineSpeed", "value": value},
                }
            )
            + "\n"
        ]
    )
