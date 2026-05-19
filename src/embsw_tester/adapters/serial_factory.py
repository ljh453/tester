from __future__ import annotations

from pathlib import Path
from typing import Any, Callable, Dict, Mapping, Optional

from embsw_tester.adapters.canoe_factory import (
    create_canoe_adapter_from_profile,
    has_canoe_profile,
)
from embsw_tester.adapters.inca_factory import (
    PopenFactory,
    RequestIdFactory,
    create_inca_adapter_from_profile,
    has_inca_profile,
)
from embsw_tester.adapters.registry import AdapterRegistry, create_default_adapter_registry
from embsw_tester.adapters.serial import PySerialPort, SerialAdapter, SerialPort, SerialPortSettings
from embsw_tester.adapters.trace32_factory import (
    RclClientFactory,
    UdpSocketFactory,
    create_trace32_adapter_from_profile,
    has_trace32_profile,
)

SerialPortFactory = Callable[[SerialPortSettings], SerialPort]


def create_serial_adapter_from_profile(
    tool_profile_snapshot: Mapping[str, Any],
    evidence_root: Path,
    port_factory: Optional[SerialPortFactory] = None,
) -> SerialAdapter:
    factory = port_factory or PySerialPort
    ports = {
        settings.logical_name: factory(settings)
        for settings in _serial_settings_from_profile(tool_profile_snapshot)
    }
    return SerialAdapter(ports, evidence_root=evidence_root)


def create_adapter_registry_from_tool_profile(
    tool_profile_snapshot: Mapping[str, Any],
    evidence_root: Path,
    serial_port_factory: Optional[SerialPortFactory] = None,
    trace32_rcl_client_factory: Optional[RclClientFactory] = None,
    trace32_udp_socket_factory: Optional[UdpSocketFactory] = None,
    inca_popen_factory: Optional[PopenFactory] = None,
    inca_request_id_factory: Optional[RequestIdFactory] = None,
    canoe_popen_factory: Optional[PopenFactory] = None,
    canoe_request_id_factory: Optional[RequestIdFactory] = None,
) -> AdapterRegistry:
    registry = create_default_adapter_registry()
    if _has_serial_devices(tool_profile_snapshot):
        registry.register(
            "serial",
            create_serial_adapter_from_profile(
                tool_profile_snapshot,
                evidence_root=evidence_root,
                port_factory=serial_port_factory,
            ),
        )
    if has_trace32_profile(tool_profile_snapshot):
        registry.register(
            "trace32",
            create_trace32_adapter_from_profile(
                tool_profile_snapshot,
                rcl_client_factory=trace32_rcl_client_factory,
                udp_socket_factory=trace32_udp_socket_factory,
            ),
        )
    if has_inca_profile(tool_profile_snapshot):
        registry.register(
            "inca",
            create_inca_adapter_from_profile(
                tool_profile_snapshot,
                popen_factory=inca_popen_factory,
                request_id_factory=inca_request_id_factory,
            ),
        )
    if has_canoe_profile(tool_profile_snapshot):
        registry.register(
            "canoe",
            create_canoe_adapter_from_profile(
                tool_profile_snapshot,
                popen_factory=canoe_popen_factory,
                request_id_factory=canoe_request_id_factory,
            ),
        )
    return registry


def _serial_settings_from_profile(
    tool_profile_snapshot: Mapping[str, Any],
) -> list[SerialPortSettings]:
    serial_section = tool_profile_snapshot.get("serial", {})
    if not isinstance(serial_section, Mapping):
        raise ValueError("'serial' tool profile section must be a mapping.")
    devices = serial_section.get("devices", {})
    if not isinstance(devices, Mapping):
        raise ValueError("'serial.devices' tool profile section must be a mapping.")

    return [
        _serial_settings_from_device(str(logical_name), device_config)
        for logical_name, device_config in devices.items()
    ]


def _serial_settings_from_device(
    logical_name: str,
    device_config: Any,
) -> SerialPortSettings:
    if not isinstance(device_config, Mapping):
        raise ValueError(f"Serial device '{logical_name}' must be a mapping.")
    return SerialPortSettings(
        logical_name=logical_name,
        system_port=str(device_config["port"]),
        baudrate=int(device_config.get("baudrate", 9600)),
        timeout_ms=int(device_config.get("timeout_ms", 1000)),
        parity=str(device_config.get("parity", "none")),
        stop_bits=float(device_config.get("stop_bits", 1.0)),
        byte_size=int(device_config.get("byte_size", 8)),
        line_ending=str(device_config.get("line_ending", "\n")),
        encoding=str(device_config.get("encoding", "utf-8")),
        write_flush=_bool_setting(
            device_config.get("write_flush", True),
            "write_flush",
        ),
        dtr=_optional_bool_setting(device_config.get("dtr"), "dtr"),
        rts=_optional_bool_setting(device_config.get("rts"), "rts"),
        device_type=str(device_config.get("device_type", "generic_serial")),
        command_profile=str(device_config.get("command_profile", "raw_line")),
    )


def _has_serial_devices(tool_profile_snapshot: Mapping[str, Any]) -> bool:
    serial_section = tool_profile_snapshot.get("serial")
    return isinstance(serial_section, Mapping) and bool(serial_section.get("devices"))


def _bool_setting(value: Any, field_name: str) -> bool:
    if isinstance(value, bool):
        return value
    normalized = str(value).strip().lower()
    if normalized in {"1", "true", "on", "yes"}:
        return True
    if normalized in {"0", "false", "off", "no"}:
        return False
    raise ValueError(f"Serial setting '{field_name}' must be a boolean value.")


def _optional_bool_setting(value: Any, field_name: str) -> Optional[bool]:
    if value is None:
        return None
    normalized = str(value).strip().lower()
    if normalized in {"", "none", "auto", "default"}:
        return None
    return _bool_setting(value, field_name)
