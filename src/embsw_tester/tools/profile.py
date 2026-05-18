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
    canoe_section = document.get("canoe")
    if canoe_section is not None:
        normalized["canoe"] = _normalize_canoe_section(canoe_section)
    execution_section = document.get("execution")
    if execution_section is not None:
        normalized["execution"] = _normalize_execution_section(execution_section)
    for key, value in document.items():
        if key in {"serial", "trace32", "inca", "canoe", "execution"}:
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
    normalized["parity"] = _normalize_serial_parity(normalized.get("parity", "none"))
    normalized["stop_bits"] = _normalize_serial_stop_bits(normalized.get("stop_bits", 1.0))
    normalized["byte_size"] = _normalize_serial_byte_size(normalized.get("byte_size", 8))
    if "command_profile" in normalized:
        normalized["command_profile"] = str(normalized["command_profile"])
    return normalized


def _normalize_serial_parity(value: Any) -> str:
    normalized = str(value).strip().lower()
    aliases = {
        "n": "none",
        "none": "none",
        "e": "even",
        "even": "even",
        "o": "odd",
        "odd": "odd",
        "m": "mark",
        "mark": "mark",
        "s": "space",
        "space": "space",
    }
    if normalized not in aliases:
        raise ValueError("'serial.devices.*.parity' must be one of none, even, odd, mark, or space.")
    return aliases[normalized]


def _normalize_serial_stop_bits(value: Any) -> float:
    stop_bits = float(value)
    if stop_bits not in {1.0, 1.5, 2.0}:
        raise ValueError("'serial.devices.*.stop_bits' must be 1, 1.5, or 2.")
    return stop_bits


def _normalize_serial_byte_size(value: Any) -> int:
    byte_size = int(value)
    if byte_size not in {5, 6, 7, 8}:
        raise ValueError("'serial.devices.*.byte_size' must be 5, 6, 7, or 8.")
    return byte_size


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


def _normalize_canoe_section(section: Any) -> Dict[str, Any]:
    if not isinstance(section, Mapping):
        raise ValueError("'canoe' profile section must be a mapping.")
    normalized: Dict[str, Any] = {}
    if "helper" in section:
        normalized["helper"] = _normalize_canoe_helper(section["helper"])
    for key, value in section.items():
        if key == "helper":
            continue
        normalized[str(key)] = value
    return normalized


def _normalize_canoe_helper(config: Any) -> Dict[str, Any]:
    if not isinstance(config, Mapping):
        raise ValueError("'canoe.helper' profile section must be a mapping.")
    normalized = {
        key: value
        for key, value in config.items()
        if key != "command"
    }
    normalized["enabled"] = _as_bool(normalized.get("enabled", True))
    if "application" in normalized:
        normalized["application"] = str(normalized["application"]).strip().lower()
    if "prog_id" in normalized:
        normalized["prog_id"] = str(normalized["prog_id"])
    if normalized["enabled"]:
        if "command" not in config:
            raise ValueError("'canoe.helper' requires 'command' when enabled.")
        normalized["command"] = _normalize_command_sequence(config["command"], "canoe.helper")
    elif "command" in config:
        normalized["command"] = _normalize_command_sequence(config["command"], "canoe.helper")
    return normalized


def _normalize_command_sequence(value: Any, path: str = "inca.helper") -> list[str]:
    if isinstance(value, (str, bytes)) or not isinstance(value, list):
        raise ValueError(f"'{path}.command' must be a YAML sequence.")
    if not value:
        raise ValueError(f"'{path}.command' must not be empty.")
    return [str(item) for item in value]


def _normalize_execution_section(section: Any) -> Dict[str, Any]:
    if not isinstance(section, Mapping):
        raise ValueError("'execution' profile section must be a mapping.")
    normalized = {
        str(key): value
        for key, value in section.items()
    }
    normalized["requires_real_hardware"] = _as_bool(
        normalized.get("requires_real_hardware", False)
    )
    normalized["allow_env"] = str(
        normalized.get("allow_env", "EMBSW_TESTER_ALLOW_REAL_HARDWARE")
    )
    return normalized


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
