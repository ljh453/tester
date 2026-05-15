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
        optional_args=frozenset({"timeout_ms"}),
        category="adapter",
        adapter="inca",
    ),
    "inca.measure.read": CommandSpec(
        type="inca.measure.read",
        required_args=frozenset({"variable"}),
        optional_args=frozenset({"save_as", "timeout_ms"}),
        category="adapter",
        adapter="inca",
    ),
    "inca.recording.start": CommandSpec(
        type="inca.recording.start",
        optional_args=frozenset({"name", "output_dir", "timeout_ms"}),
        category="adapter",
        adapter="inca",
    ),
    "inca.recording.stop": CommandSpec(
        type="inca.recording.stop",
        optional_args=frozenset({"timeout_ms"}),
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
    "sent_usb.read": CommandSpec(
        type="sent_usb.read",
        required_args=frozenset({"device"}),
        optional_args=frozenset({"channel", "timeout_ms", "until", "save_as"}),
        category="device",
        adapter="serial",
    ),
    "power_supply.command": CommandSpec(
        type="power_supply.command",
        required_args=frozenset({"device"}),
        optional_args=frozenset({"channel", "text", "timeout_ms", "value"}),
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
