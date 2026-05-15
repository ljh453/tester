from __future__ import annotations

import importlib
from typing import Any, Callable, Mapping, Optional

from embsw_tester.adapters.trace32 import RclTrace32Transport, Trace32Adapter, UdpTrace32Transport

RclClientFactory = Callable[[Mapping[str, Any]], Any]
UdpSocketFactory = Callable[..., Any]


def create_trace32_adapter_from_profile(
    tool_profile_snapshot: Mapping[str, Any],
    rcl_client_factory: Optional[RclClientFactory] = None,
    udp_socket_factory: Optional[UdpSocketFactory] = None,
) -> Trace32Adapter:
    trace32_section = _trace32_section(tool_profile_snapshot)
    return Trace32Adapter(
        rcl_transport=_create_rcl_transport(trace32_section, rcl_client_factory),
        udp_transport=_create_udp_transport(trace32_section, udp_socket_factory),
    )


def has_trace32_profile(tool_profile_snapshot: Mapping[str, Any]) -> bool:
    return isinstance(tool_profile_snapshot.get("trace32"), Mapping)


def _trace32_section(tool_profile_snapshot: Mapping[str, Any]) -> Mapping[str, Any]:
    trace32_section = tool_profile_snapshot.get("trace32", {})
    if not isinstance(trace32_section, Mapping):
        raise ValueError("'trace32' tool profile section must be a mapping.")
    return trace32_section


def _create_rcl_transport(
    trace32_section: Mapping[str, Any],
    rcl_client_factory: Optional[RclClientFactory],
) -> Optional[RclTrace32Transport]:
    rcl_config = trace32_section.get("rcl")
    if rcl_config is None:
        return None
    if not isinstance(rcl_config, Mapping):
        raise ValueError("'trace32.rcl' tool profile section must be a mapping.")
    if not bool(rcl_config.get("enabled", True)):
        return None

    client = _create_rcl_client(rcl_config, rcl_client_factory)
    if client is None:
        return None
    return RclTrace32Transport(
        client=client,
        command_method=str(rcl_config.get("command_method", "cmd")),
    )


def _create_rcl_client(
    rcl_config: Mapping[str, Any],
    rcl_client_factory: Optional[RclClientFactory],
) -> Any:
    if rcl_client_factory is not None:
        return rcl_client_factory(rcl_config)

    import_path = rcl_config.get("client_factory")
    if import_path is None:
        return None

    factory = _load_import_path(str(import_path))
    client_args = rcl_config.get("client_args", {})
    if not isinstance(client_args, Mapping):
        raise ValueError("'trace32.rcl.client_args' must be a mapping.")
    return factory(**dict(client_args))


def _create_udp_transport(
    trace32_section: Mapping[str, Any],
    udp_socket_factory: Optional[UdpSocketFactory],
) -> Optional[UdpTrace32Transport]:
    udp_config = trace32_section.get("udp")
    if udp_config is None:
        return None
    if not isinstance(udp_config, Mapping):
        raise ValueError("'trace32.udp' tool profile section must be a mapping.")
    if not bool(udp_config.get("enabled", True)):
        return None
    if "host" not in udp_config:
        raise ValueError("'trace32.udp' requires 'host' when enabled.")
    if "port" not in udp_config:
        raise ValueError("'trace32.udp' requires 'port' when enabled.")

    kwargs = {
        "host": str(udp_config["host"]),
        "port": int(udp_config["port"]),
        "terminator": str(udp_config.get("terminator", "\n")),
        "encoding": str(udp_config.get("encoding", "utf-8")),
        "response_bytes": int(udp_config.get("response_bytes", 4096)),
    }
    if udp_socket_factory is not None:
        kwargs["socket_factory"] = udp_socket_factory
    return UdpTrace32Transport(**kwargs)


def _load_import_path(import_path: str) -> Callable[..., Any]:
    module_name, separator, attribute_path = import_path.partition(":")
    if not separator or not module_name or not attribute_path:
        raise ValueError(
            "'trace32.rcl.client_factory' must use 'module:attribute' format."
        )

    try:
        module = importlib.import_module(module_name)
    except ImportError as exc:
        raise ValueError(f"Could not import Trace32 RCL factory module '{module_name}'.") from exc

    target: Any = module
    for attribute in attribute_path.split("."):
        try:
            target = getattr(target, attribute)
        except AttributeError as exc:
            raise ValueError(
                f"Trace32 RCL factory '{import_path}' does not exist."
            ) from exc
    if not callable(target):
        raise ValueError(f"Trace32 RCL factory '{import_path}' is not callable.")
    return target
