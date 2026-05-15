from embsw_tester.adapters import AdapterContext
from embsw_tester.adapters.serial import FakeSerialPort
from embsw_tester.adapters.serial_factory import (
    create_adapter_registry_from_tool_profile,
    create_serial_adapter_from_profile,
)


def test_create_serial_adapter_from_profile_builds_ports_from_profile(tmp_path):
    profile = {
        "serial": {
            "devices": {
                "psu": {
                    "device_type": "power_supply",
                    "port": "COM3",
                    "baudrate": 9600,
                    "command_profile": "pending",
                },
                "sent_usb": {
                    "device_type": "mach_systems_sent_usb",
                    "port": "COM4",
                    "baudrate": 115200,
                    "command_profile": "sent_usb_line",
                },
            }
        }
    }
    created = {}

    def port_factory(settings):
        created[settings.logical_name] = settings
        return FakeSerialPort(rx_lines=["OK"])

    adapter = create_serial_adapter_from_profile(
        profile,
        evidence_root=tmp_path,
        port_factory=port_factory,
    )

    result = adapter.execute(
        "serial.read",
        {"port": "sent_usb", "timeout_ms": 25},
        AdapterContext(run_id="factory-run", testcase="sent_case", phase="steps"),
    )

    assert created["psu"].system_port == "COM3"
    assert created["psu"].device_type == "power_supply"
    assert created["psu"].command_profile == "pending"
    assert created["sent_usb"].system_port == "COM4"
    assert created["sent_usb"].baudrate == 115200
    assert created["sent_usb"].device_type == "mach_systems_sent_usb"
    assert result.values["text"] == "OK"


def test_create_adapter_registry_from_tool_profile_replaces_serial_adapter(tmp_path):
    profile = {
        "serial": {
            "devices": {
                "psu": {
                    "device_type": "power_supply",
                    "port": "COM3",
                    "baudrate": 9600,
                },
            }
        }
    }

    registry = create_adapter_registry_from_tool_profile(
        profile,
        evidence_root=tmp_path,
        serial_port_factory=lambda settings: FakeSerialPort(rx_lines=[]),
    )

    result = registry.get("serial").execute(
        "serial.write",
        {"port": "psu", "text": "OUT 1 ON"},
        AdapterContext(run_id="factory-run", testcase="psu_case", phase="steps"),
    )

    assert result.values["tx"] == "OUT 1 ON"
    assert result.raw_evidence_ref == "raw-logs/serial/factory-run/psu_case.log"
