from embsw_tester.adapters import AdapterContext
from embsw_tester.adapters.serial import FakeSerialPort, SerialAdapter


def test_serial_write_records_tx_and_raw_evidence(tmp_path):
    port = FakeSerialPort(rx_lines=[])
    adapter = SerialAdapter({"psu": port}, evidence_root=tmp_path)

    result = adapter.execute(
        "serial.write",
        {"port": "psu", "text": "OUT 1 ON"},
        AdapterContext(run_id="serial-run", testcase="case", phase="steps"),
    )

    assert result.success is True
    assert port.tx_lines == ["OUT 1 ON"]
    assert result.values["tx"] == "OUT 1 ON"
    assert result.raw_evidence_ref == "raw-logs/serial/serial-run/case.log"
    assert (tmp_path / result.raw_evidence_ref).read_text(encoding="utf-8") == "TX OUT 1 ON\n"


def test_serial_read_returns_rx_and_records_raw_evidence(tmp_path):
    port = FakeSerialPort(rx_lines=["OK"])
    adapter = SerialAdapter({"psu": port}, evidence_root=tmp_path)

    result = adapter.execute(
        "serial.read",
        {"port": "psu", "timeout_ms": 50},
        AdapterContext(run_id="serial-run", testcase="case", phase="steps"),
    )

    assert result.success is True
    assert result.values["text"] == "OK"
    assert result.values["timeout_ms"] == 50
    assert (tmp_path / result.raw_evidence_ref).read_text(encoding="utf-8") == "RX OK\n"


def test_serial_read_accepts_regex_match(tmp_path):
    port = FakeSerialPort(rx_lines=["VALUE:123"])
    adapter = SerialAdapter({"sent_usb": port}, evidence_root=tmp_path)

    result = adapter.execute(
        "serial.read",
        {"port": "sent_usb", "match": r"VALUE:\d+"},
        AdapterContext(run_id="serial-run", testcase="case", phase="steps"),
    )

    assert result.success is True
    assert result.values["text"] == "VALUE:123"
    assert result.values["match"] == r"VALUE:\d+"


def test_serial_read_fails_when_regex_match_misses(tmp_path):
    port = FakeSerialPort(rx_lines=["ERROR"])
    adapter = SerialAdapter({"sent_usb": port}, evidence_root=tmp_path)

    result = adapter.execute(
        "serial.read",
        {"port": "sent_usb", "match": r"VALUE:\d+"},
        AdapterContext(run_id="serial-run", testcase="case", phase="steps"),
    )

    assert result.success is False
    assert result.values["text"] == "ERROR"
    assert "regex" in result.message
