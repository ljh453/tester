from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Dict, List, Mapping, Sequence

STX = 0x02
ETX = 0x03
SENT_CHANNEL_1_FAST_FRAME_ID = 100
SENT_CHANNEL_2_FAST_FRAME_ID = 200
SENT_CHANNEL_1_CONFIG_WRITE_ID = 2
SENT_CHANNEL_2_CONFIG_WRITE_ID = 12
SENT_CHANNEL_1_START_ID = 21
SENT_CHANNEL_2_START_ID = 31
SENT_CHANNEL_1_STOP_ID = 22
SENT_CHANNEL_2_STOP_ID = 32
SENT_CHANNEL_1_FAST_TRANSMIT_ID = 41
SENT_CHANNEL_2_FAST_TRANSMIT_ID = 51
SENT_CHANNEL_1_SLOW_TRANSMIT_ID = 42
SENT_CHANNEL_2_SLOW_TRANSMIT_ID = 52


class MachSentGatewayError(ValueError):
    """Raised when a Mach Systems SENT Gateway frame is malformed."""


@dataclass(frozen=True)
class GatewayFrame:
    raw: bytes
    length: int
    message_id: int
    data: bytes
    checksum: int

    def to_dict(self) -> Dict[str, Any]:
        return {
            "raw_hex": _hex(self.raw),
            "length": self.length,
            "message_id": self.message_id,
            "data_hex": _hex(self.data),
            "checksum": self.checksum,
        }


def encode_gateway_frame(message_id: int, data: bytes = b"") -> bytes:
    normalized_data = bytes(data)
    _validate_byte("message_id", message_id)
    length = 1 + len(normalized_data)
    _validate_byte("length", length)
    checksum = _checksum(length, message_id, normalized_data)
    return bytes([STX, length, message_id]) + normalized_data + bytes([checksum, ETX])


def parse_gateway_frame(frame: bytes) -> GatewayFrame:
    raw = bytes(frame)
    if len(raw) < 5:
        raise MachSentGatewayError("Gateway frame is too short.")
    if raw[0] != STX:
        raise MachSentGatewayError("Gateway frame does not start with STX.")
    if raw[-1] != ETX:
        raise MachSentGatewayError("Gateway frame does not end with ETX.")

    length = raw[1]
    expected_length = 1 + 1 + length + 1 + 1
    if len(raw) != expected_length:
        raise MachSentGatewayError(
            f"Gateway frame length mismatch: expected {expected_length} bytes, got {len(raw)}."
        )

    body = raw[2 : 2 + length]
    if not body:
        raise MachSentGatewayError("Gateway frame is missing a message id.")
    message_id = body[0]
    data = body[1:]
    checksum = raw[2 + length]
    expected_checksum = _checksum(length, message_id, data)
    if checksum != expected_checksum:
        raise MachSentGatewayError(
            f"Gateway frame checksum mismatch: expected 0x{expected_checksum:02X}, got 0x{checksum:02X}."
        )

    return GatewayFrame(
        raw=raw,
        length=length,
        message_id=message_id,
        data=data,
        checksum=checksum,
    )


def parse_sent_fast_frame(frame: GatewayFrame) -> Dict[str, Any]:
    channel_by_message_id = {
        SENT_CHANNEL_1_FAST_FRAME_ID: 1,
        SENT_CHANNEL_2_FAST_FRAME_ID: 2,
    }
    try:
        channel = channel_by_message_id[frame.message_id]
    except KeyError as exc:
        raise MachSentGatewayError(
            f"Gateway message id {frame.message_id} is not a SENT fast frame."
        ) from exc

    if len(frame.data) < 2:
        raise MachSentGatewayError("SENT fast frame payload is too short.")

    status_and_count = frame.data[0]
    status_nibble = status_and_count & 0x0F
    data_nibble_count = (status_and_count >> 4) & 0x0F
    available_data_nibbles = _payload_nibbles(frame.data[1:-1])
    if data_nibble_count > len(available_data_nibbles):
        raise MachSentGatewayError(
            "SENT fast frame declares more data nibbles than the payload contains."
        )

    crc_byte = frame.data[-1]
    return {
        "channel": channel,
        "message_id": frame.message_id,
        "status_nibble": status_nibble,
        "data_nibble_count": data_nibble_count,
        "data_nibbles": available_data_nibbles[:data_nibble_count],
        "crc": crc_byte & 0x0F,
        "crc_calculated": (crc_byte >> 4) & 0x0F,
        "frame_data_hex": _hex(frame.data),
    }


def build_sent_gateway_command(action: str, args: Mapping[str, Any]) -> bytes:
    normalized_action = _normalize_action(action)
    channel = _channel(args.get("channel", 1))
    message_id = _command_message_id(normalized_action, channel)

    if normalized_action == "config":
        payload = build_sent_channel_config(args)
    elif normalized_action == "transmit_fast":
        payload = build_sent_fast_frame_payload(args)
    elif normalized_action == "transmit_slow":
        payload = build_sent_slow_frame_payload(args)
    elif normalized_action in {"start", "stop"}:
        payload = b""
    else:
        raise MachSentGatewayError(f"Unsupported Mach SENT Gateway action '{action}'.")
    return encode_gateway_frame(message_id, payload)


def build_sent_channel_config(args: Mapping[str, Any]) -> bytes:
    auto_start = 1 if _truthy(args.get("autostart", args.get("auto_start", False))) else 0
    direction = _direction(args.get("direction", "rx"))
    crc_mode = _crc_mode(args.get("crc_mode", "hw"))
    data_nibble_count = int(args.get("data_nibble_count", 6))
    if data_nibble_count < 1 or data_nibble_count > 6:
        raise MachSentGatewayError("SENT data_nibble_count must be in range 1..6.")

    byte0 = (
        auto_start
        | (direction << 2)
        | (crc_mode << 3)
        | (data_nibble_count << 5)
    )

    pulse_pause_enabled = 1 if _truthy(args.get("pulse_pause_enabled", False)) else 0
    forward_or_echo = int(args.get("rx_forward_mode", args.get("tx_echo_mode", 0)))
    slow_channel_mode = _slow_channel_mode(args.get("slow_channel_mode", 0))
    slow_crc_fault = 1 if _truthy(args.get("slow_channel_tx_crc_fault", False)) else 0
    if forward_or_echo < 0 or forward_or_echo > 2:
        raise MachSentGatewayError("rx_forward_mode/tx_echo_mode must be in range 0..2.")
    byte1 = (
        pulse_pause_enabled
        | (forward_or_echo << 1)
        | (slow_channel_mode << 3)
        | (slow_crc_fault << 6)
    )

    unit_time = _time_us_to_spec_units(args.get("unit_time_us", 3.0), "unit_time_us")
    pulse_pause_period = _time_us_to_spec_units(
        args.get("pulse_pause_frame_period_us", 0),
        "pulse_pause_frame_period_us",
    )
    swap_fast_data_nibbles = 1 if _truthy(args.get("swap_fast_data_nibbles", False)) else 0
    return bytes([
        byte0,
        byte1,
        unit_time & 0xFF,
        (unit_time >> 8) & 0xFF,
        pulse_pause_period & 0xFF,
        (pulse_pause_period >> 8) & 0xFF,
        swap_fast_data_nibbles,
    ])


def build_sent_fast_frame_payload(args: Mapping[str, Any]) -> bytes:
    data_nibbles = _data_nibbles(args.get("data_nibbles", []))
    if len(data_nibbles) < 1 or len(data_nibbles) > 6:
        raise MachSentGatewayError("SENT transmit_fast data_nibbles must contain 1..6 nibbles.")
    status = _nibble(args.get("status", args.get("status_nibble", 0)), "status")
    crc = _nibble(args.get("crc", 0), "crc")
    crc_calculated = _nibble(args.get("crc_calculated", 0), "crc_calculated")

    payload = bytearray()
    payload.append(status | (len(data_nibbles) << 4))
    for index in range(0, len(data_nibbles), 2):
        low = data_nibbles[index]
        high = data_nibbles[index + 1] if index + 1 < len(data_nibbles) else 0
        payload.append(low | (high << 4))
    payload.append(crc | (crc_calculated << 4))
    return bytes(payload)


def build_sent_slow_frame_payload(args: Mapping[str, Any]) -> bytes:
    message_id_value = args.get("slow_message_id", args.get("message_id"))
    if message_id_value is None:
        raise MachSentGatewayError("SENT transmit_slow requires slow_message_id.")
    slow_message_id = _byte(message_id_value, "slow_message_id")

    data_value = args.get("data", args.get("slow_data"))
    if data_value is None:
        raise MachSentGatewayError("SENT transmit_slow requires data.")
    data = _uint16(data_value, "data")

    crc_received = _six_bit(args.get("crc_received", args.get("crc", 0)), "crc_received")
    slow_frame_type_value = args.get("slow_frame_type", args.get("frame_type"))
    if slow_frame_type_value is None:
        slow_frame_type = 1 if _slow_channel_mode(args.get("slow_channel_mode", 0)) == 2 else 0
    else:
        slow_frame_type = _slow_frame_type(slow_frame_type_value)
    enhanced_format = 1 if _truthy(
        args.get(
            "enhanced_format",
            args.get("enhanced_config_bit", args.get("enhanced_serial_format", False)),
        )
    ) else 0
    crc_calculated = _six_bit(args.get("crc_calculated", 0), "crc_calculated")

    return bytes([
        slow_message_id,
        data & 0xFF,
        (data >> 8) & 0xFF,
        crc_received | (slow_frame_type << 6) | (enhanced_format << 7),
        crc_calculated,
    ])


def parse_gateway_ack(frame: GatewayFrame, expected_message_id: int) -> bool:
    if frame.message_id != expected_message_id:
        raise MachSentGatewayError(
            f"Expected ACK message id {expected_message_id}, got {frame.message_id}."
        )
    if len(frame.data) != 1:
        raise MachSentGatewayError("Gateway ACK frame must contain one status byte.")
    if frame.data[0] == 1:
        return True
    if frame.data[0] == 0:
        raise MachSentGatewayError(f"Gateway returned ERR for message id {expected_message_id}.")
    raise MachSentGatewayError(f"Gateway ACK status must be 0 or 1, got {frame.data[0]}.")


def command_message_id(action: str, channel: int) -> int:
    return _command_message_id(_normalize_action(action), _channel(channel))


def _payload_nibbles(payload: bytes) -> List[int]:
    nibbles: List[int] = []
    for value in payload:
        nibbles.append(value & 0x0F)
        nibbles.append((value >> 4) & 0x0F)
    return nibbles


def _checksum(length: int, message_id: int, data: bytes) -> int:
    return (length + message_id + sum(data)) & 0xFF


def _validate_byte(name: str, value: int) -> None:
    if value < 0 or value > 0xFF:
        raise MachSentGatewayError(f"{name} must fit in one byte.")


def _hex(payload: bytes) -> str:
    return payload.hex().upper()


def _command_message_id(action: str, channel: int) -> int:
    ids = {
        "config": {
            1: SENT_CHANNEL_1_CONFIG_WRITE_ID,
            2: SENT_CHANNEL_2_CONFIG_WRITE_ID,
        },
        "start": {
            1: SENT_CHANNEL_1_START_ID,
            2: SENT_CHANNEL_2_START_ID,
        },
        "stop": {
            1: SENT_CHANNEL_1_STOP_ID,
            2: SENT_CHANNEL_2_STOP_ID,
        },
        "transmit_fast": {
            1: SENT_CHANNEL_1_FAST_TRANSMIT_ID,
            2: SENT_CHANNEL_2_FAST_TRANSMIT_ID,
        },
        "transmit_slow": {
            1: SENT_CHANNEL_1_SLOW_TRANSMIT_ID,
            2: SENT_CHANNEL_2_SLOW_TRANSMIT_ID,
        },
    }
    try:
        return ids[action][channel]
    except KeyError as exc:
        raise MachSentGatewayError(
            f"Unsupported Mach SENT Gateway action/channel '{action}'/{channel}."
        ) from exc


def _normalize_action(action: str) -> str:
    return str(action).strip().lower().replace("-", "_")


def _channel(value: Any) -> int:
    text = str(value).strip().upper()
    if text in {"1", "SENT1", "CH1", "CHANNEL1"}:
        return 1
    if text in {"2", "SENT2", "CH2", "CHANNEL2"}:
        return 2
    raise MachSentGatewayError("Mach SENT Gateway channel must be 1 or 2.")


def _direction(value: Any) -> int:
    text = str(value).strip().lower()
    if text in {"tx", "transmit", "0"}:
        return 0
    if text in {"rx", "receive", "1"}:
        return 1
    raise MachSentGatewayError("Mach SENT Gateway direction must be 'tx' or 'rx'.")


def _crc_mode(value: Any) -> int:
    text = str(value).strip().lower()
    mapping = {
        "off": 0,
        "hw": 1,
        "hardware": 1,
        "sw": 2,
        "software": 2,
    }
    if text in mapping:
        return mapping[text]
    try:
        numeric = int(text)
    except ValueError as exc:
        raise MachSentGatewayError("Mach SENT Gateway crc_mode must be off, hw, or sw.") from exc
    if numeric < 0 or numeric > 2:
        raise MachSentGatewayError("Mach SENT Gateway crc_mode must be in range 0..2.")
    return numeric


def _slow_channel_mode(value: Any) -> int:
    text = str(value).strip().lower().replace("-", "_")
    mapping = {
        "0": 0,
        "fast": 0,
        "fast_only": 0,
        "1": 1,
        "short": 1,
        "short_serial": 1,
        "2": 2,
        "enhanced": 2,
        "enhanced_serial": 2,
    }
    if text not in mapping:
        raise MachSentGatewayError(
            "Mach SENT Gateway slow_channel_mode must be fast_only, short_serial, or enhanced_serial."
        )
    return mapping[text]


def _time_us_to_spec_units(value: Any, name: str) -> int:
    numeric = float(value)
    if numeric < 0:
        raise MachSentGatewayError(f"{name} must be non-negative.")
    units = int(round(numeric * 100))
    if units > 0xFFFF:
        raise MachSentGatewayError(f"{name} is too large.")
    return units


def _data_nibbles(value: Any) -> List[int]:
    if not isinstance(value, Sequence) or isinstance(value, (str, bytes)):
        raise MachSentGatewayError("data_nibbles must be a sequence of integers.")
    return [_nibble(item, "data_nibble") for item in value]


def _nibble(value: Any, name: str) -> int:
    numeric = _integer(value, name)
    if numeric < 0 or numeric > 0x0F:
        raise MachSentGatewayError(f"{name} must fit in one nibble.")
    return numeric


def _byte(value: Any, name: str) -> int:
    numeric = _integer(value, name)
    if numeric < 0 or numeric > 0xFF:
        raise MachSentGatewayError(f"{name} must fit in one byte.")
    return numeric


def _uint16(value: Any, name: str) -> int:
    numeric = _integer(value, name)
    if numeric < 0 or numeric > 0xFFFF:
        raise MachSentGatewayError(f"{name} must fit in 16 bits.")
    return numeric


def _six_bit(value: Any, name: str) -> int:
    numeric = _integer(value, name)
    if numeric < 0 or numeric > 0x3F:
        raise MachSentGatewayError(f"{name} must fit in 6 bits.")
    return numeric


def _slow_frame_type(value: Any) -> int:
    text = str(value).strip().lower().replace("-", "_")
    mapping = {
        "0": 0,
        "short": 0,
        "short_serial": 0,
        "1": 1,
        "enhanced": 1,
        "enhanced_serial": 1,
    }
    if text not in mapping:
        raise MachSentGatewayError(
            "Mach SENT Gateway slow_frame_type must be short_serial or enhanced_serial."
        )
    return mapping[text]


def _integer(value: Any, name: str) -> int:
    try:
        if isinstance(value, str):
            return int(value.strip(), 0)
        return int(value)
    except (TypeError, ValueError) as exc:
        raise MachSentGatewayError(f"{name} must be an integer.") from exc


def _truthy(value: Any) -> bool:
    if isinstance(value, bool):
        return value
    return str(value).strip().lower() in {"1", "true", "on", "yes"}
