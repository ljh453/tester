from __future__ import annotations

from pathlib import Path
from typing import Any, Dict, Mapping

import yaml


def load_tool_profile(path: Path) -> Dict[str, Any]:
    document = yaml.safe_load(Path(path).read_text(encoding="utf-8")) or {}
    if not isinstance(document, Mapping):
        raise ValueError("Tool profile must be a mapping.")
    return normalize_tool_profile(document)


def normalize_tool_profile(document: Mapping[str, Any]) -> Dict[str, Any]:
    normalized: Dict[str, Any] = {}
    serial_section = document.get("serial")
    if serial_section is not None:
        normalized["serial"] = _normalize_serial_section(serial_section)
    for key, value in document.items():
        if key == "serial":
            continue
        normalized[str(key)] = value
    return normalized


def _normalize_serial_section(section: Any) -> Dict[str, Any]:
    if not isinstance(section, Mapping):
        raise ValueError("'serial' profile section must be a mapping.")
    devices = section.get("devices", {})
    if not isinstance(devices, Mapping):
        raise ValueError("'serial.devices' must be a mapping.")
    normalized_devices = {
        str(name): _normalize_serial_device(str(name), config)
        for name, config in devices.items()
    }
    normalized = {
        key: value
        for key, value in section.items()
        if key != "devices"
    }
    normalized["devices"] = normalized_devices
    return normalized


def _normalize_serial_device(name: str, config: Any) -> Dict[str, Any]:
    if not isinstance(config, Mapping):
        raise ValueError(f"Serial device '{name}' must be a mapping.")
    if "device_type" not in config:
        raise ValueError(f"Serial device '{name}' requires 'device_type'.")
    if "port" not in config:
        raise ValueError(f"Serial device '{name}' requires 'port'.")

    normalized = {
        key: value
        for key, value in config.items()
    }
    normalized["device_type"] = str(normalized["device_type"])
    normalized["port"] = str(normalized["port"])
    if "baudrate" in normalized:
        normalized["baudrate"] = int(normalized["baudrate"])
    else:
        normalized["baudrate"] = 9600
    if "command_profile" in normalized:
        normalized["command_profile"] = str(normalized["command_profile"])
    return normalized
