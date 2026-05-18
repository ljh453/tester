import json
import subprocess

from embsw_tester.adapters.canoe_bridge import (
    JsonLineCanoeBridgeTransport,
    create_canoe_bridge_process_transport,
)


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


def test_json_line_canoe_bridge_transport_writes_request_and_reads_response():
    process = FakeProcess(
        [
            json.dumps(
                {
                    "request_id": "req-1",
                    "success": True,
                    "status": "passed",
                    "message": "read",
                    "values": {"signal": "CarSpeed", "value": 42},
                    "duration_ms": 17,
                }
            )
            + "\n"
        ]
    )
    transport = JsonLineCanoeBridgeTransport(
        process,
        request_id_factory=lambda: "req-1",
    )

    result = transport.execute(
        "canoe.signal.read",
        {"bus": "CAN", "channel": 1, "message": "ABSData", "signal": "CarSpeed"},
        timeout_ms=2500,
    )

    request = json.loads(process.stdin.writes[0])
    assert request == {
        "request_id": "req-1",
        "command_type": "canoe.signal.read",
        "args": {
            "bus": "CAN",
            "channel": 1,
            "message": "ABSData",
            "signal": "CarSpeed",
        },
        "timeout_ms": 2500,
    }
    assert process.stdin.writes[0].endswith("\n")
    assert process.stdin.flush_count == 1
    assert result.success is True
    assert result.status == "passed"
    assert result.message == "read"
    assert result.values == {"signal": "CarSpeed", "value": 42}
    assert result.duration_ms == 17


def test_json_line_canoe_bridge_transport_reports_mismatched_response_id():
    process = FakeProcess(
        [
            json.dumps(
                {
                    "request_id": "other",
                    "success": True,
                    "status": "passed",
                    "message": "read",
                    "values": {},
                }
            )
            + "\n"
        ]
    )
    transport = JsonLineCanoeBridgeTransport(
        process,
        request_id_factory=lambda: "req-1",
    )

    result = transport.execute("canoe.measurement.stop", {}, 1000)

    assert result.success is False
    assert result.status == "failed"
    assert "unexpected response id" in result.message
    assert "req-1" in result.message
    assert "other" in result.message


def test_create_canoe_bridge_process_transport_starts_text_json_line_process():
    process = FakeProcess(
        [
            json.dumps(
                {
                    "request_id": "req-1",
                    "success": True,
                    "status": "passed",
                    "message": "ready",
                    "values": {},
                }
            )
            + "\n"
        ]
    )
    calls = []

    def popen_factory(command, **kwargs):
        calls.append((command, kwargs))
        return process

    transport = create_canoe_bridge_process_transport(
        ["C:/Python311/python.exe", "-m", "embsw_tester.adapters.canoe_com_helper"],
        popen_factory=popen_factory,
        request_id_factory=lambda: "req-1",
    )

    result = transport.execute("canoe.measurement.stop", {}, timeout_ms=500)

    command, kwargs = calls[0]
    assert command == [
        "C:/Python311/python.exe",
        "-m",
        "embsw_tester.adapters.canoe_com_helper",
    ]
    assert kwargs["stdin"] == subprocess.PIPE
    assert kwargs["stdout"] == subprocess.PIPE
    assert kwargs["stderr"] == subprocess.PIPE
    assert kwargs["text"] is True
    assert kwargs["encoding"] == "utf-8"
    assert kwargs["bufsize"] == 1
    assert result.success is True
