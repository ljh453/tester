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
