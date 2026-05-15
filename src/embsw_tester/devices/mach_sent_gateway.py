from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Dict, List

STX = 0x02
ETX = 0x03
SENT_CHANNEL_1_FAST_FRAME_ID = 100
SENT_CHANNEL_2_FAST_FRAME_ID = 200


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
