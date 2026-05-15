from __future__ import annotations

from decimal import Decimal, InvalidOperation
from typing import Any, Mapping, Optional


class VuPowerKError(ValueError):
    """Raised when a VuPower K-series USB command cannot be built or parsed."""


QUERY_ACTIONS = {
    "identify",
    "measure_current",
    "measure_voltage",
    "mode",
    "read_current",
    "read_output",
    "read_voltage",
    "system_error",
}


def format_vupower_command(action: str, args: Mapping[str, Any]) -> str:
    normalized_action = _normalize_action(action)
    channel = _channel(args.get("channel", "P1"))

    if normalized_action == "raw":
        text = args.get("text")
        if text is None:
            raise VuPowerKError("Raw VuPower command requires 'text'.")
        return str(text)
    if normalized_action == "apply":
        return (
            f"APPL {channel},"
            f"{_limit_value(args, 'voltage')},"
            f"{_limit_value(args, 'current')}"
        )
    if normalized_action == "set_voltage":
        return f"SOUR:VOLT {channel},{_limit_value(args, 'voltage')}"
    if normalized_action == "set_current":
        return f"SOUR:CURR {channel},{_limit_value(args, 'current')}"
    if normalized_action == "read_voltage":
        return f"SOUR:VOLT? {channel}"
    if normalized_action == "read_current":
        return f"SOUR:CURR? {channel}"
    if normalized_action == "mode":
        return f"SOUR:FLOW? {channel}"
    if normalized_action == "output":
        return f"OUTP:STAT {channel},{_state(args)}"
    if normalized_action == "read_output":
        return f"OUTP:STAT? {channel}"
    if normalized_action == "measure_voltage":
        return f"MEAS:{'VOLTA' if _truthy(args.get('average')) else 'VOLT'}? {channel}"
    if normalized_action == "measure_current":
        return f"MEAS:{'CURRA' if _truthy(args.get('average')) else 'CURR'}? {channel}"
    if normalized_action == "track":
        return f"OUTP:TRACK {_state(args)}"
    if normalized_action == "read_track":
        return "OUTP:TRACK?"
    if normalized_action == "reset":
        return "*RST"
    if normalized_action == "identify":
        return "*IDN?"
    if normalized_action == "system_error":
        return "SYST:ERR?"

    raise VuPowerKError(f"Unsupported VuPower K action '{action}'.")


def parse_vupower_response(action: str, text: str) -> Any:
    normalized_action = _normalize_action(action)
    stripped = text.strip()
    if normalized_action in {"measure_current", "measure_voltage", "read_current", "read_voltage"}:
        return float(stripped)
    if normalized_action in {"read_output", "read_track"}:
        if stripped == "1":
            return True
        if stripped == "0":
            return False
        raise VuPowerKError(f"VuPower boolean response must be 0 or 1, got '{stripped}'.")
    if normalized_action == "mode":
        if stripped == "1":
            return "CV"
        if stripped == "0":
            return "CC"
        raise VuPowerKError(f"VuPower mode response must be 0 or 1, got '{stripped}'.")
    if normalized_action == "system_error":
        return int(stripped)
    return stripped


def is_vupower_query(action: str, args: Mapping[str, Any]) -> bool:
    normalized_action = _normalize_action(action)
    if "read" in args:
        return _truthy(args.get("read"))
    return normalized_action in QUERY_ACTIONS or format_vupower_command(action, args).endswith("?")


def _normalize_action(action: str) -> str:
    return str(action).strip().lower().replace("-", "_")


def _channel(value: Any) -> str:
    text = str(value).strip().upper()
    if text in {"1", "P1"}:
        return "P1"
    if text in {"2", "P2"}:
        return "P2"
    raise VuPowerKError("VuPower channel must be P1, P2, 1, or 2.")


def _limit_value(args: Mapping[str, Any], name: str) -> str:
    value = args.get(name)
    if value is None:
        raise VuPowerKError(f"VuPower action requires '{name}'.")
    text = str(value).strip()
    if text.upper() in {"MIN", "MAX"}:
        return text.upper()
    try:
        return f"{Decimal(text):.3f}"
    except InvalidOperation as exc:
        raise VuPowerKError(f"VuPower {name} must be numeric, MIN, or MAX.") from exc


def _state(args: Mapping[str, Any]) -> str:
    value: Optional[Any] = args.get("state", args.get("value"))
    if value is None:
        raise VuPowerKError("VuPower output action requires 'state'.")
    if _truthy(value):
        return "ON"
    if _falsy(value):
        return "OFF"
    raise VuPowerKError("VuPower state must be ON/OFF or true/false.")


def _truthy(value: Any) -> bool:
    if isinstance(value, bool):
        return value
    return str(value).strip().lower() in {"1", "true", "on", "yes"}


def _falsy(value: Any) -> bool:
    if isinstance(value, bool):
        return not value
    return str(value).strip().lower() in {"0", "false", "off", "no"}
