from __future__ import annotations

import subprocess
import uuid
from typing import Any, Callable, Mapping, Optional, Sequence

from embsw_tester.adapters.canoe import CanoeAdapter
from embsw_tester.adapters.canoe_bridge import create_canoe_bridge_process_transport

PopenFactory = Callable[..., Any]
RequestIdFactory = Callable[[], str]


def create_canoe_adapter_from_profile(
    tool_profile_snapshot: Mapping[str, Any],
    popen_factory: Optional[PopenFactory] = None,
    request_id_factory: Optional[RequestIdFactory] = None,
) -> CanoeAdapter:
    canoe_section = _canoe_section(tool_profile_snapshot)
    helper_config = canoe_section.get("helper")
    if helper_config is None:
        return CanoeAdapter()
    if not isinstance(helper_config, Mapping):
        raise ValueError("'canoe.helper' tool profile section must be a mapping.")
    if not bool(helper_config.get("enabled", True)):
        return CanoeAdapter()

    command = _helper_command(helper_config)
    bridge_transport = create_canoe_bridge_process_transport(
        command,
        popen_factory=popen_factory or subprocess.Popen,
        request_id_factory=request_id_factory or (lambda: str(uuid.uuid4())),
    )
    return CanoeAdapter(bridge_transport=bridge_transport)


def has_canoe_profile(tool_profile_snapshot: Mapping[str, Any]) -> bool:
    return isinstance(tool_profile_snapshot.get("canoe"), Mapping)


def _canoe_section(tool_profile_snapshot: Mapping[str, Any]) -> Mapping[str, Any]:
    canoe_section = tool_profile_snapshot.get("canoe", {})
    if not isinstance(canoe_section, Mapping):
        raise ValueError("'canoe' tool profile section must be a mapping.")
    return canoe_section


def _helper_command(helper_config: Mapping[str, Any]) -> Sequence[str]:
    command = helper_config.get("command")
    if isinstance(command, (str, bytes)) or not isinstance(command, Sequence):
        raise ValueError("'canoe.helper.command' must be a sequence.")
    if not command:
        raise ValueError("'canoe.helper.command' must not be empty.")

    normalized = [str(item) for item in command]
    prog_id = helper_config.get("prog_id")
    if prog_id is not None:
        return [*normalized, "--prog-id", str(prog_id)]

    application = helper_config.get("application")
    if application is not None:
        return [*normalized, "--application", str(application)]
    return normalized
