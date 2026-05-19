import sys
import types

from embsw_tester.adapters.serial import PySerialPort, SerialPortSettings


def test_pyserial_port_passes_framing_settings(monkeypatch):
    calls = []

    class FakeSerial:
        def __init__(self, **kwargs):
            calls.append(kwargs)

    monkeypatch.setitem(sys.modules, "serial", types.SimpleNamespace(Serial=FakeSerial))

    PySerialPort(
        SerialPortSettings(
            logical_name="sent_usb",
            system_port="COM4",
            baudrate=115200,
            timeout_ms=500,
            parity="even",
            stop_bits=2,
            byte_size=7,
        )
    )

    assert calls == [
        {
            "port": "COM4",
            "baudrate": 115200,
            "timeout": 0.5,
            "write_timeout": 0.5,
            "parity": "E",
            "stopbits": 2.0,
            "bytesize": 7,
        }
    ]


def test_pyserial_write_line_uses_configured_line_ending_and_flushes(monkeypatch):
    written = []
    flush_calls = []
    serial_instances = []

    class FakeSerial:
        def __init__(self, **kwargs):
            self.dtr = None
            self.rts = None
            serial_instances.append(self)

        def write(self, payload):
            written.append(bytes(payload))
            return len(payload)

        def flush(self):
            flush_calls.append(True)

    monkeypatch.setitem(sys.modules, "serial", types.SimpleNamespace(Serial=FakeSerial))

    port = PySerialPort(
        SerialPortSettings(
            logical_name="psu",
            system_port="COM3",
            line_ending="crlf",
            write_flush=True,
            dtr=True,
            rts=False,
        )
    )

    bytes_written = port.write_line("OUTP:STAT P1,ON")

    assert written == [b"OUTP:STAT P1,ON\r\n"]
    assert bytes_written == len(b"OUTP:STAT P1,ON\r\n")
    assert flush_calls == [True]
    assert serial_instances[0].dtr is True
    assert serial_instances[0].rts is False
