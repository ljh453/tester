from __future__ import annotations

import subprocess
import uuid
from typing import Any, Callable, Mapping, Optional, Sequence

from embsw_tester.adapters.inca import IncaAdapter
from embsw_tester.adapters.inca_bridge import create_inca_bridge_process_transport

PopenFactory = Callable[..., Any]
RequestIdFactory = Callable[[], str]


def create_inca_adapter_from_profile(
    tool_profile_snapshot: Mapping[str, Any],
    popen_factory: Optional[PopenFactory] = None,
    request_id_factory: Optional[RequestIdFactory] = None,
) -> IncaAdapter:
    inca_section = _inca_section(tool_profile_snapshot)
    helper_config = inca_section.get("helper")
    if helper_config is None:
        return IncaAdapter()
    if not isinstance(helper_config, Mapping):
        raise ValueError("'inca.helper' tool profile section must be a mapping.")
    if not bool(helper_config.get("enabled", True)):
        return IncaAdapter()

    command = _helper_command(helper_config)
    bridge_transport = create_inca_bridge_process_transport(
        command,
        popen_factory=popen_factory or subprocess.Popen,
        request_id_factory=request_id_factory or (lambda: str(uuid.uuid4())),
    )
    return IncaAdapter(bridge_transport=bridge_transport)


def has_inca_profile(tool_profile_snapshot: Mapping[str, Any]) -> bool:
    return isinstance(tool_profile_snapshot.get("inca"), Mapping)


def _inca_section(tool_profile_snapshot: Mapping[str, Any]) -> Mapping[str, Any]:
    inca_section = tool_profile_snapshot.get("inca", {})
    if not isinstance(inca_section, Mapping):
        raise ValueError("'inca' tool profile section must be a mapping.")
    return inca_section


def _helper_command(helper_config: Mapping[str, Any]) -> Sequence[str]:
    command = helper_config.get("command")
    if isinstance(command, (str, bytes)) or not isinstance(command, Sequence):
        raise ValueError("'inca.helper.command' must be a sequence.")
    if not command:
        raise ValueError("'inca.helper.command' must not be empty.")
    return [str(item) for item in command]
