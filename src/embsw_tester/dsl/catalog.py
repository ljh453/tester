from __future__ import annotations

from dataclasses import dataclass, field
from typing import Dict, FrozenSet


@dataclass(frozen=True)
class CommandSpec:
    type: str
    required_args: FrozenSet[str] = field(default_factory=frozenset)
    optional_args: FrozenSet[str] = field(default_factory=frozenset)
    category: str = "runtime"


COMMAND_SPECS: Dict[str, CommandSpec] = {
    "assert.eq": CommandSpec(
        type="assert.eq",
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
    "delay": CommandSpec(
        type="delay",
        required_args=frozenset({"ms"}),
        category="runtime",
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
        category="adapter",
    ),
    "set": CommandSpec(
        type="set",
        required_args=frozenset({"var", "value"}),
        category="runtime",
    ),
}
