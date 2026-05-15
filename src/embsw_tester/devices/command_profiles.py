from __future__ import annotations

import re
from dataclasses import dataclass
from typing import Any, Dict, List, Mapping, Optional

from embsw_tester.adapters import AdapterContext, AdapterRegistry, AdapterResult
from embsw_tester.devices.mach_sent_gateway import (
    SENT_CHANNEL_1_FAST_FRAME_ID,
    SENT_CHANNEL_2_FAST_FRAME_ID,
    GatewayFrame,
    MachSentGatewayError,
    parse_gateway_frame,
    parse_sent_fast_frame,
)
from embsw_tester.devices.vupower_k import (
    VuPowerKError,
    format_vupower_command,
    is_vupower_query,
    parse_vupower_response,
)
from embsw_tester.runtime.expressions import render_template


class DeviceCommandError(RuntimeError):
    """Raised when a device-level command profile cannot be executed."""


@dataclass(frozen=True)
class DeviceCommandExecution:
    resolved_inputs: Dict[str, Any]
    outputs: Dict[str, Any]
    save_value: Optional[Any] = None


def execute_device_command(
    command_type: str,
    args: Dict[str, Any],
    tool_profile_snapshot: Mapping[str, Any],
    adapter_registry: AdapterRegistry,
    adapter_context: AdapterContext,
) -> DeviceCommandExecution:
    device_name = _required_text(args, "device")
    device_config = _device_config(tool_profile_snapshot, device_name)
    profile_name = str(device_config.get("command_profile", ""))
    command_definition = _command_definition(
        tool_profile_snapshot,
        device_name,
        profile_name,
        command_type,
    )
    serial_steps: List[Dict[str, Any]] = []
    save_value: Optional[Any] = None

    if command_definition.get("protocol") == "mach_sent_gateway":
        return _execute_mach_sent_gateway_command(
            command_type,
            device_name,
            profile_name,
            command_definition,
            args,
            adapter_registry,
            adapter_context,
        )
    if command_definition.get("protocol") == "vupower_k_usb":
        return _execute_vupower_k_command(
            command_type,
            device_name,
            profile_name,
            command_definition,
            args,
            adapter_registry,
            adapter_context,
        )

    if "write" in command_definition:
        write_args = _serial_write_args(device_name, command_definition, args)
        write_result = _execute_serial(
            adapter_registry,
            "serial.write",
            write_args,
            adapter_context,
        )
        serial_steps.append(_serial_step("serial.write", write_args, write_result))

    if "read" in command_definition:
        read_definition = _read_definition(command_definition)
        read_args = _serial_read_args(device_name, read_definition, args)
        read_result = _execute_serial(
            adapter_registry,
            "serial.read",
            read_args,
            adapter_context,
        )
        serial_steps.append(_serial_step("serial.read", read_args, read_result))
        save_value = _read_save_value(read_result.values, read_definition, args)

    outputs: Dict[str, Any] = {
        "device": device_name,
        "command_profile": profile_name,
        "serial": serial_steps,
    }
    if save_value is not None:
        outputs["value"] = save_value

    return DeviceCommandExecution(
        resolved_inputs=dict(args),
        outputs=outputs,
        save_value=save_value,
    )


def _execute_mach_sent_gateway_command(
    command_type: str,
    device_name: str,
    profile_name: str,
    command_definition: Mapping[str, Any],
    args: Mapping[str, Any],
    adapter_registry: AdapterRegistry,
    adapter_context: AdapterContext,
) -> DeviceCommandExecution:
    if command_type != "sent_usb.read":
        raise DeviceCommandError(
            f"Protocol 'mach_sent_gateway' does not support command '{command_type}'."
        )

    channel = _sent_channel(args, command_definition)
    expected_message_id = _sent_fast_frame_message_id(channel)
    timeout_ms = int(args.get("timeout_ms", command_definition.get("timeout_ms", 1000)))
    max_frames = int(args.get("max_frames", command_definition.get("max_frames", 1)))
    serial_steps: List[Dict[str, Any]] = []
    last_frame: Optional[GatewayFrame] = None

    try:
        for _ in range(max_frames):
            frame = _read_mach_gateway_frame(
                device_name,
                timeout_ms,
                adapter_registry,
                adapter_context,
                serial_steps,
            )
            last_frame = frame
            if frame.message_id == expected_message_id:
                parsed = parse_sent_fast_frame(frame)
                outputs = {
                    "device": device_name,
                    "command_profile": profile_name,
                    "protocol": "mach_sent_gateway",
                    "channel": channel,
                    "frame": frame.to_dict(),
                    "serial": serial_steps,
                    "value": parsed,
                }
                return DeviceCommandExecution(
                    resolved_inputs=dict(args),
                    outputs=outputs,
                    save_value=parsed,
                )
    except MachSentGatewayError as exc:
        raise DeviceCommandError(str(exc)) from exc

    got = "none" if last_frame is None else str(last_frame.message_id)
    raise DeviceCommandError(
        f"Expected SENT channel {channel} fast frame message id {expected_message_id}, got {got}."
    )


def _read_mach_gateway_frame(
    device_name: str,
    timeout_ms: int,
    adapter_registry: AdapterRegistry,
    adapter_context: AdapterContext,
    serial_steps: List[Dict[str, Any]],
) -> GatewayFrame:
    stx = _read_serial_bytes(
        device_name,
        1,
        timeout_ms,
        adapter_registry,
        adapter_context,
        serial_steps,
    )
    length_bytes = _read_serial_bytes(
        device_name,
        1,
        timeout_ms,
        adapter_registry,
        adapter_context,
        serial_steps,
    )
    remaining = _read_serial_bytes(
        device_name,
        length_bytes[0] + 2,
        timeout_ms,
        adapter_registry,
        adapter_context,
        serial_steps,
    )
    return parse_gateway_frame(stx + length_bytes + remaining)


def _read_serial_bytes(
    device_name: str,
    count: int,
    timeout_ms: int,
    adapter_registry: AdapterRegistry,
    adapter_context: AdapterContext,
    serial_steps: List[Dict[str, Any]],
) -> bytes:
    read_args: Dict[str, Any] = {
        "port": device_name,
        "count": count,
        "timeout_ms": timeout_ms,
    }
    result = _execute_serial(
        adapter_registry,
        "serial.read_bytes",
        read_args,
        adapter_context,
    )
    serial_steps.append(_serial_step("serial.read_bytes", read_args, result))
    return bytes.fromhex(str(result.values["data_hex"]))


def _sent_channel(args: Mapping[str, Any], command_definition: Mapping[str, Any]) -> int:
    channel = int(args.get("channel", command_definition.get("channel", 1)))
    if channel not in (1, 2):
        raise DeviceCommandError("Mach SENT Gateway channel must be 1 or 2.")
    return channel


def _sent_fast_frame_message_id(channel: int) -> int:
    if channel == 1:
        return SENT_CHANNEL_1_FAST_FRAME_ID
    return SENT_CHANNEL_2_FAST_FRAME_ID


def _execute_vupower_k_command(
    command_type: str,
    device_name: str,
    profile_name: str,
    command_definition: Mapping[str, Any],
    args: Mapping[str, Any],
    adapter_registry: AdapterRegistry,
    adapter_context: AdapterContext,
) -> DeviceCommandExecution:
    if command_type != "power_supply.command":
        raise DeviceCommandError(
            f"Protocol 'vupower_k_usb' does not support command '{command_type}'."
        )

    action = str(args.get("action", command_definition.get("action", "raw")))
    command_args = {**dict(command_definition), **dict(args)}
    try:
        command_text = format_vupower_command(action, command_args)
    except VuPowerKError as exc:
        raise DeviceCommandError(str(exc)) from exc

    serial_steps: List[Dict[str, Any]] = []
    write_args = {
        "port": device_name,
        "text": command_text,
    }
    if "timeout_ms" in args:
        write_args["timeout_ms"] = args["timeout_ms"]
    write_result = _execute_serial(
        adapter_registry,
        "serial.write",
        write_args,
        adapter_context,
    )
    serial_steps.append(_serial_step("serial.write", write_args, write_result))

    save_value: Optional[Any] = None
    outputs: Dict[str, Any] = {
        "device": device_name,
        "command_profile": profile_name,
        "protocol": "vupower_k_usb",
        "action": action,
        "command": command_text,
        "serial": serial_steps,
    }
    channel = command_args.get("channel")
    if channel is not None:
        outputs["channel"] = channel

    if is_vupower_query(action, command_args):
        read_definition = _read_definition(command_definition)
        read_args = _serial_read_args(device_name, read_definition, args)
        read_result = _execute_serial(
            adapter_registry,
            "serial.read",
            read_args,
            adapter_context,
        )
        serial_steps.append(_serial_step("serial.read", read_args, read_result))
        try:
            save_value = parse_vupower_response(action, str(read_result.values["text"]))
        except VuPowerKError as exc:
            raise DeviceCommandError(str(exc)) from exc
        outputs["value"] = save_value

    return DeviceCommandExecution(
        resolved_inputs=dict(args),
        outputs=outputs,
        save_value=save_value,
    )


def _device_config(
    tool_profile_snapshot: Mapping[str, Any],
    device_name: str,
) -> Mapping[str, Any]:
    serial_section = tool_profile_snapshot.get("serial", {})
    if not isinstance(serial_section, Mapping):
        raise DeviceCommandError("Tool profile does not declare serial devices.")
    devices = serial_section.get("devices", {})
    if not isinstance(devices, Mapping):
        raise DeviceCommandError("Tool profile serial.devices must be a mapping.")
    device_config = devices.get(device_name)
    if not isinstance(device_config, Mapping):
        raise DeviceCommandError(f"Device '{device_name}' is not declared in the tool profile.")
    return device_config


def _command_definition(
    tool_profile_snapshot: Mapping[str, Any],
    device_name: str,
    profile_name: str,
    command_type: str,
) -> Mapping[str, Any]:
    if not profile_name:
        raise DeviceCommandError(f"Device '{device_name}' does not declare a command_profile.")
    if profile_name == "pending":
        raise DeviceCommandError(f"Device '{device_name}' command_profile is pending.")

    profiles = tool_profile_snapshot.get("command_profiles", {})
    if not isinstance(profiles, Mapping):
        raise DeviceCommandError("Tool profile command_profiles must be a mapping.")
    profile = profiles.get(profile_name)
    if not isinstance(profile, Mapping):
        raise DeviceCommandError(f"Command profile '{profile_name}' is not defined.")
    commands = profile.get("commands", {})
    if not isinstance(commands, Mapping):
        raise DeviceCommandError(f"Command profile '{profile_name}' commands must be a mapping.")
    command_definition = commands.get(command_type)
    if not isinstance(command_definition, Mapping):
        raise DeviceCommandError(
            f"Command profile '{profile_name}' does not define command '{command_type}'."
        )
    return command_definition


def _serial_write_args(
    device_name: str,
    command_definition: Mapping[str, Any],
    args: Mapping[str, Any],
) -> Dict[str, Any]:
    text = render_template(str(command_definition["write"]), args)
    write_args: Dict[str, Any] = {
        "port": device_name,
        "text": text,
    }
    if "timeout_ms" in args:
        write_args["timeout_ms"] = args["timeout_ms"]
    return write_args


def _serial_read_args(
    device_name: str,
    read_definition: Mapping[str, Any],
    args: Mapping[str, Any],
) -> Dict[str, Any]:
    read_args: Dict[str, Any] = {"port": device_name}
    timeout_ms = args.get("timeout_ms", read_definition.get("timeout_ms"))
    if timeout_ms is not None:
        read_args["timeout_ms"] = timeout_ms
    until = args.get("until", read_definition.get("until"))
    if until is not None:
        read_args["until"] = render_template(str(until), args)
    match = args.get("match", read_definition.get("match"))
    if match is not None:
        read_args["match"] = render_template(str(match), args)
    return read_args


def _read_definition(command_definition: Mapping[str, Any]) -> Mapping[str, Any]:
    read_definition = command_definition.get("read") or {}
    if not isinstance(read_definition, Mapping):
        raise DeviceCommandError("Device command read definition must be a mapping.")
    return read_definition


def _execute_serial(
    adapter_registry: AdapterRegistry,
    command_type: str,
    args: Dict[str, Any],
    adapter_context: AdapterContext,
) -> AdapterResult:
    adapter = adapter_registry.get("serial")
    result = adapter.execute(command_type, args, adapter_context)
    if not result.success:
        raise DeviceCommandError(result.message or f"Serial command '{command_type}' failed.")
    return result


def _serial_step(
    command_type: str,
    resolved_inputs: Dict[str, Any],
    result: AdapterResult,
) -> Dict[str, Any]:
    return {
        "command_type": command_type,
        "resolved_inputs": resolved_inputs,
        "outputs": result.to_outputs(),
    }


def _read_save_value(
    values: Mapping[str, Any],
    read_definition: Mapping[str, Any],
    args: Mapping[str, Any],
) -> Any:
    raw_value = _save_value(values)
    extract_pattern = read_definition.get("extract")
    if extract_pattern is None:
        return raw_value

    text = str(values.get("text", raw_value))
    rendered_pattern = render_template(str(extract_pattern), args)
    try:
        match = re.search(rendered_pattern, text)
    except re.error as exc:
        raise DeviceCommandError(f"Invalid response extractor '{rendered_pattern}': {exc}.") from exc
    if match is None:
        raise DeviceCommandError(
            f"Serial response did not match extractor '{rendered_pattern}'."
        )
    if "value" in match.groupdict():
        return match.group("value")
    if match.lastindex:
        return match.group(1)
    return match.group(0)


def _save_value(values: Mapping[str, Any]) -> Any:
    if "text" in values:
        return values["text"]
    if "value" in values:
        return values["value"]
    return dict(values)


def _required_text(args: Mapping[str, Any], name: str) -> str:
    value = args.get(name)
    if value is None:
        raise DeviceCommandError(f"Missing required device argument '{name}'.")
    return str(value)
