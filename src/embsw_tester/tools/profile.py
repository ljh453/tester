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
    trace32_section = document.get("trace32")
    if trace32_section is not None:
        normalized["trace32"] = _normalize_trace32_section(trace32_section)
    inca_section = document.get("inca")
    if inca_section is not None:
        normalized["inca"] = _normalize_inca_section(inca_section)
    for key, value in document.items():
        if key in {"serial", "trace32", "inca"}:
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


def _normalize_trace32_section(section: Any) -> Dict[str, Any]:
    if not isinstance(section, Mapping):
        raise ValueError("'trace32' profile section must be a mapping.")
    normalized: Dict[str, Any] = {}
    if "rcl" in section:
        normalized["rcl"] = _normalize_trace32_rcl(section["rcl"])
    if "udp" in section:
        normalized["udp"] = _normalize_trace32_udp(section["udp"])
    for key, value in section.items():
        if key in {"rcl", "udp"}:
            continue
        normalized[str(key)] = value
    return normalized


def _normalize_trace32_rcl(config: Any) -> Dict[str, Any]:
    if not isinstance(config, Mapping):
        raise ValueError("'trace32.rcl' profile section must be a mapping.")
    normalized = {
        key: value
        for key, value in config.items()
        if key != "client_args"
    }
    normalized["enabled"] = _as_bool(normalized.get("enabled", True))
    normalized["command_method"] = str(normalized.get("command_method", "cmd"))
    if "client_factory" in normalized:
        normalized["client_factory"] = str(normalized["client_factory"])
    client_args = config.get("client_args", {})
    if not isinstance(client_args, Mapping):
        raise ValueError("'trace32.rcl.client_args' must be a mapping.")
    normalized["client_args"] = dict(client_args)
    return normalized


def _normalize_trace32_udp(config: Any) -> Dict[str, Any]:
    if not isinstance(config, Mapping):
        raise ValueError("'trace32.udp' profile section must be a mapping.")
    enabled = _as_bool(config.get("enabled", True))
    normalized = {
        key: value
        for key, value in config.items()
    }
    normalized["enabled"] = enabled
    if enabled:
        if "host" not in normalized:
            raise ValueError("'trace32.udp' requires 'host' when enabled.")
        if "port" not in normalized:
            raise ValueError("'trace32.udp' requires 'port' when enabled.")
        normalized["host"] = str(normalized["host"])
        normalized["port"] = int(normalized["port"])
    elif "host" in normalized:
        normalized["host"] = str(normalized["host"])
    if "port" in normalized:
        normalized["port"] = int(normalized["port"])
    normalized["terminator"] = str(normalized.get("terminator", "\n"))
    normalized["encoding"] = str(normalized.get("encoding", "utf-8"))
    normalized["response_bytes"] = int(normalized.get("response_bytes", 4096))
    return normalized


def _normalize_inca_section(section: Any) -> Dict[str, Any]:
    if not isinstance(section, Mapping):
        raise ValueError("'inca' profile section must be a mapping.")
    normalized: Dict[str, Any] = {}
    if "helper" in section:
        normalized["helper"] = _normalize_inca_helper(section["helper"])
    for key, value in section.items():
        if key == "helper":
            continue
        normalized[str(key)] = value
    return normalized


def _normalize_inca_helper(config: Any) -> Dict[str, Any]:
    if not isinstance(config, Mapping):
        raise ValueError("'inca.helper' profile section must be a mapping.")
    normalized = {
        key: value
        for key, value in config.items()
        if key != "command"
    }
    normalized["enabled"] = _as_bool(normalized.get("enabled", True))
    if normalized["enabled"]:
        if "command" not in config:
            raise ValueError("'inca.helper' requires 'command' when enabled.")
        normalized["command"] = _normalize_command_sequence(config["command"])
    elif "command" in config:
        normalized["command"] = _normalize_command_sequence(config["command"])
    return normalized


def _normalize_command_sequence(value: Any) -> list[str]:
    if isinstance(value, (str, bytes)) or not isinstance(value, list):
        raise ValueError("'inca.helper.command' must be a YAML sequence.")
    if not value:
        raise ValueError("'inca.helper.command' must not be empty.")
    return [str(item) for item in value]


def _as_bool(value: Any) -> bool:
    if isinstance(value, bool):
        return value
    if isinstance(value, str):
        normalized = value.strip().lower()
        if normalized in {"true", "yes", "on", "1"}:
            return True
        if normalized in {"false", "no", "off", "0"}:
            return False
    return bool(value)
