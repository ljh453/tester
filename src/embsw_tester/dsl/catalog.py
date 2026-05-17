from __future__ import annotations

from dataclasses import dataclass, field
from typing import Dict, FrozenSet, Optional


@dataclass(frozen=True)
class CommandSpec:
    type: str
    required_args: FrozenSet[str] = field(default_factory=frozenset)
    optional_args: FrozenSet[str] = field(default_factory=frozenset)
    category: str = "runtime"
    adapter: Optional[str] = None


COMMAND_SPECS: Dict[str, CommandSpec] = {
    "assert.eq": CommandSpec(
        type="assert.eq",
        required_args=frozenset({"left", "right"}),
        category="assert",
    ),
    "assert.gt": CommandSpec(
        type="assert.gt",
        required_args=frozenset({"left", "right"}),
        category="assert",
    ),
    "assert.fail": CommandSpec(
        type="assert.fail",
        required_args=frozenset({"message"}),
        category="assert",
    ),
    "call": CommandSpec(
        type="call",
        required_args=frozenset({"function"}),
        optional_args=frozenset({"args", "out"}),
        category="control",
    ),
    "for": CommandSpec(
        type="for",
        required_args=frozenset({"each", "as", "do"}),
        category="control",
    ),
    "canoe.measurement.start": CommandSpec(
        type="canoe.measurement.start",
        optional_args=frozenset({"configuration"}),
        category="adapter",
        adapter="canoe",
    ),
    "canoe.measurement.stop": CommandSpec(
        type="canoe.measurement.stop",
        category="adapter",
        adapter="canoe",
    ),
    "canoe.signal.read": CommandSpec(
        type="canoe.signal.read",
        required_args=frozenset({"signal"}),
        optional_args=frozenset({"bus", "channel", "save_as"}),
        category="adapter",
        adapter="canoe",
    ),
    "canoe.sysvar.read": CommandSpec(
        type="canoe.sysvar.read",
        required_args=frozenset({"namespace", "name"}),
        optional_args=frozenset({"save_as"}),
        category="adapter",
        adapter="canoe",
    ),
    "canoe.sysvar.set": CommandSpec(
        type="canoe.sysvar.set",
        required_args=frozenset({"namespace", "name", "value"}),
        category="adapter",
        adapter="canoe",
    ),
    "delay": CommandSpec(
        type="delay",
        required_args=frozenset({"ms"}),
        category="runtime",
    ),
    "inca.calibration.set": CommandSpec(
        type="inca.calibration.set",
        required_args=frozenset({"parameter", "value"}),
        optional_args=frozenset({"device", "timeout_ms", "value_kind"}),
        category="adapter",
        adapter="inca",
    ),
    "inca.measure.read": CommandSpec(
        type="inca.measure.read",
        required_args=frozenset({"variable"}),
        optional_args=frozenset({"acquisition_rate", "device", "save_as", "timeout_ms"}),
        category="adapter",
        adapter="inca",
    ),
    "inca.recording.start": CommandSpec(
        type="inca.recording.start",
        optional_args=frozenset({"file_format", "format", "name", "output_dir", "timeout_ms"}),
        category="adapter",
        adapter="inca",
    ),
    "inca.recording.stop": CommandSpec(
        type="inca.recording.stop",
        optional_args=frozenset({"discard", "file_format", "file_name", "format", "name", "timeout_ms"}),
        category="adapter",
        adapter="inca",
    ),
    "log.text": CommandSpec(
        type="log.text",
        required_args=frozenset({"text"}),
        category="logging",
    ),
    "log.value": CommandSpec(
        type="log.value",
        required_args=frozenset({"name", "value"}),
        category="logging",
    ),
    "serial.write": CommandSpec(
        type="serial.write",
        required_args=frozenset({"port", "text"}),
        optional_args=frozenset({"timeout_ms"}),
        category="adapter",
        adapter="serial",
    ),
    "serial.read": CommandSpec(
        type="serial.read",
        required_args=frozenset({"port"}),
        optional_args=frozenset({"timeout_ms", "until", "match", "save_as"}),
        category="adapter",
        adapter="serial",
    ),
    "serial.write_bytes": CommandSpec(
        type="serial.write_bytes",
        required_args=frozenset({"port", "payload_hex"}),
        optional_args=frozenset({"timeout_ms"}),
        category="adapter",
        adapter="serial",
    ),
    "serial.read_bytes": CommandSpec(
        type="serial.read_bytes",
        required_args=frozenset({"port", "count"}),
        optional_args=frozenset({"timeout_ms", "save_as"}),
        category="adapter",
        adapter="serial",
    ),
    "sent_usb.read": CommandSpec(
        type="sent_usb.read",
        required_args=frozenset({"device"}),
        optional_args=frozenset({"channel", "timeout_ms", "until", "save_as", "max_frames"}),
        category="device",
        adapter="serial",
    ),
    "sent_usb.command": CommandSpec(
        type="sent_usb.command",
        required_args=frozenset({"device", "action"}),
        optional_args=frozenset({
            "autostart",
            "auto_start",
            "channel",
            "crc",
            "crc_calculated",
            "crc_mode",
            "crc_received",
            "data",
            "data_nibble_count",
            "data_nibbles",
            "direction",
            "enhanced_config_bit",
            "enhanced_format",
            "enhanced_serial_format",
            "frame_type",
            "max_frames",
            "message_id",
            "pulse_pause_enabled",
            "pulse_pause_frame_period_us",
            "read_ack",
            "rx_forward_mode",
            "save_as",
            "slow_channel_mode",
            "slow_channel_tx_crc_fault",
            "slow_data",
            "slow_frame_type",
            "slow_message_id",
            "status",
            "status_nibble",
            "swap_fast_data_nibbles",
            "timeout_ms",
            "tx_echo_mode",
            "unit_time_us",
        }),
        category="device",
        adapter="serial",
    ),
    "power_supply.command": CommandSpec(
        type="power_supply.command",
        required_args=frozenset({"device"}),
        optional_args=frozenset({
            "action",
            "average",
            "channel",
            "current",
            "match",
            "read",
            "save_as",
            "state",
            "text",
            "timeout_ms",
            "until",
            "value",
            "voltage",
        }),
        category="device",
        adapter="serial",
    ),
    "set": CommandSpec(
        type="set",
        required_args=frozenset({"var", "value"}),
        category="runtime",
    ),
    "trace32.command": CommandSpec(
        type="trace32.command",
        required_args=frozenset({"command"}),
        optional_args=frozenset({"timeout_ms", "transport", "fallback", "save_as"}),
        category="adapter",
        adapter="trace32",
    ),
}
