from __future__ import annotations

import socket
from dataclasses import dataclass
from typing import Any, Callable, Dict, List, Mapping, Optional, Protocol, Sequence, Tuple

from embsw_tester.adapters.base import AdapterContext, AdapterResult


class Trace32CommandTransport(Protocol):
    name: str

    def execute_command(self, command: str, timeout_ms: int) -> AdapterResult:
        ...


@dataclass
class FakeTrace32Transport:
    name: str
    result: AdapterResult

    def __post_init__(self) -> None:
        self.commands: List[Tuple[str, int]] = []

    def execute_command(self, command: str, timeout_ms: int) -> AdapterResult:
        self.commands.append((command, timeout_ms))
        return self.result


class RclTrace32Transport:
    name = "rcl"

    def __init__(self, client: Any, command_method: str = "cmd"):
        self._client = client
        self._command_method = command_method

    def execute_command(self, command: str, timeout_ms: int) -> AdapterResult:
        try:
            command_callable = getattr(self._client, self._command_method)
            response = command_callable(command)
        except Exception as exc:
            return AdapterResult(
                success=False,
                status="failed",
                message=f"Trace32 RCL command failed: {exc}.",
                values={"transport": "rcl", "command": command},
            )
        return _coerce_transport_response(
            response,
            transport="rcl",
            command=command,
            success_message="Trace32 RCL command executed.",
        )


class UdpTrace32Transport:
    name = "udp"

    def __init__(
        self,
        host: str,
        port: int,
        terminator: str = "\n",
        encoding: str = "utf-8",
        response_bytes: int = 4096,
        socket_factory: Callable[..., Any] = socket.socket,
    ):
        self._host = host
        self._port = int(port)
        self._terminator = terminator
        self._encoding = encoding
        self._response_bytes = int(response_bytes)
        self._socket_factory = socket_factory

    def execute_command(self, command: str, timeout_ms: int) -> AdapterResult:
        udp_socket = None
        try:
            udp_socket = self._socket_factory(socket.AF_INET, socket.SOCK_DGRAM)
            udp_socket.settimeout(timeout_ms / 1000)
            udp_socket.connect((self._host, self._port))
            udp_socket.sendall(f"{command}{self._terminator}".encode(self._encoding))
            response = udp_socket.recv(self._response_bytes)
        except Exception as exc:
            return AdapterResult(
                success=False,
                status="failed",
                message=f"Trace32 UDP command failed: {exc}.",
                values={
                    "transport": "udp",
                    "command": command,
                    "host": self._host,
                    "port": self._port,
                },
            )
        finally:
            close = getattr(udp_socket, "close", None) if udp_socket is not None else None
            if close is not None:
                close()

        text = response.decode(self._encoding, errors="replace").strip()
        return AdapterResult(
            success=True,
            status="passed",
            message="Trace32 UDP command executed.",
            values={
                "transport": "udp",
                "command": command,
                "host": self._host,
                "port": self._port,
                "value": text,
            },
        )


class Trace32Adapter:
    name = "trace32"

    def __init__(
        self,
        rcl_transport: Optional[Trace32CommandTransport] = None,
        udp_transport: Optional[Trace32CommandTransport] = None,
    ):
        self._rcl_transport = rcl_transport
        self._udp_transport = udp_transport

    def execute(
        self,
        command_type: str,
        args: Dict[str, Any],
        context: AdapterContext,
    ) -> AdapterResult:
        if command_type == "trace32.command_sequence":
            return self._execute_command_sequence(args)
        if command_type != "trace32.command":
            return AdapterResult(
                success=False,
                status="failed",
                message=f"Unsupported Trace32 command '{command_type}'.",
            )
        return self._execute_command(args)

    def _execute_command_sequence(self, args: Dict[str, Any]) -> AdapterResult:
        commands = _required_commands(args, "commands")
        results: List[Dict[str, Any]] = []
        values: List[Any] = []

        for index, command in enumerate(commands):
            command_args = dict(args)
            command_args.pop("commands", None)
            command_args["command"] = command
            result = self._execute_command(command_args)
            result_entry = {
                "command": command,
                "success": result.success,
                "status": result.status,
                "message": result.message,
                "values": result.values,
                "duration_ms": result.duration_ms,
            }
            if result.raw_evidence_ref is not None:
                result_entry["raw_evidence_ref"] = result.raw_evidence_ref
            results.append(result_entry)

            if not result.success:
                return AdapterResult(
                    success=False,
                    status=result.status,
                    message=(
                        "Trace32 command sequence failed at "
                        f"index {index}: {result.message}"
                    ),
                    values={
                        "commands": commands,
                        "failed_index": index,
                        "results": results,
                    },
                )
            values.append(result.values.get("value", dict(result.values)))

        return AdapterResult(
            success=True,
            status="passed",
            message="Trace32 command sequence executed.",
            values={
                "commands": commands,
                "results": results,
                "value": values,
            },
        )

    def _execute_command(self, args: Dict[str, Any]) -> AdapterResult:
        command = _required_text(args, "command")
        timeout_ms = int(args.get("timeout_ms", 1000))
        transport_name = str(args.get("transport", "rcl"))
        fallback_enabled = bool(args.get("fallback", True))

        attempts: List[Dict[str, Any]] = []
        if transport_name == "udp":
            return self._execute_with_transport(
                self._udp_transport,
                "udp",
                command,
                timeout_ms,
                fallback_used=False,
                attempts=attempts,
            )

        rcl_result = self._try_transport(
            self._rcl_transport,
            "rcl",
            command,
            timeout_ms,
            attempts,
        )
        if rcl_result.success:
            return _with_trace32_values(
                rcl_result,
                command=command,
                transport="rcl",
                fallback_used=False,
                attempts=attempts,
            )

        if fallback_enabled:
            return self._execute_with_transport(
                self._udp_transport,
                "udp",
                command,
                timeout_ms,
                fallback_used=True,
                attempts=attempts,
            )

        return _with_trace32_values(
            rcl_result,
            command=command,
            transport="rcl",
            fallback_used=False,
            attempts=attempts,
        )

    def _execute_with_transport(
        self,
        transport: Optional[Trace32CommandTransport],
        transport_name: str,
        command: str,
        timeout_ms: int,
        fallback_used: bool,
        attempts: List[Dict[str, Any]],
    ) -> AdapterResult:
        result = self._try_transport(
            transport,
            transport_name,
            command,
            timeout_ms,
            attempts,
        )
        return _with_trace32_values(
            result,
            command=command,
            transport=transport_name,
            fallback_used=fallback_used,
            attempts=attempts,
        )

    def _try_transport(
        self,
        transport: Optional[Trace32CommandTransport],
        transport_name: str,
        command: str,
        timeout_ms: int,
        attempts: List[Dict[str, Any]],
    ) -> AdapterResult:
        if transport is None:
            result = AdapterResult(
                success=False,
                status="failed",
                message=f"Trace32 {transport_name} transport is not configured.",
            )
        else:
            result = transport.execute_command(command, timeout_ms)
        attempts.append(
            {
                "transport": transport_name,
                "success": result.success,
                "status": result.status,
                "message": result.message,
            }
        )
        return result


def _with_trace32_values(
    result: AdapterResult,
    command: str,
    transport: str,
    fallback_used: bool,
    attempts: List[Dict[str, Any]],
) -> AdapterResult:
    values = dict(result.values)
    values["command"] = command
    values["transport"] = transport
    values["fallback_used"] = fallback_used
    values["attempts"] = list(attempts)
    return AdapterResult(
        success=result.success,
        status=result.status,
        message=result.message,
        values=values,
        raw_evidence_ref=result.raw_evidence_ref,
        duration_ms=result.duration_ms,
    )


def _coerce_transport_response(
    response: Any,
    transport: str,
    command: str,
    success_message: str,
) -> AdapterResult:
    if isinstance(response, AdapterResult):
        values = dict(response.values)
        values.setdefault("transport", transport)
        values.setdefault("command", command)
        return AdapterResult(
            success=response.success,
            status=response.status,
            message=response.message,
            values=values,
            raw_evidence_ref=response.raw_evidence_ref,
            duration_ms=response.duration_ms,
        )

    if isinstance(response, bytes):
        value = response.decode("utf-8", errors="replace").strip()
        return _successful_transport_result(transport, command, value, success_message)

    if isinstance(response, Mapping):
        values = dict(response)
        success = bool(values.pop("success", True))
        status = str(values.pop("status", "passed" if success else "failed"))
        message = str(values.pop("message", success_message))
        values.setdefault("transport", transport)
        values.setdefault("command", command)
        return AdapterResult(
            success=success,
            status=status,
            message=message,
            values=values,
        )

    return _successful_transport_result(transport, command, response, success_message)


def _successful_transport_result(
    transport: str,
    command: str,
    value: Any,
    message: str,
) -> AdapterResult:
    return AdapterResult(
        success=True,
        status="passed",
        message=message,
        values={
            "transport": transport,
            "command": command,
            "value": value,
        },
    )


def _required_text(args: Dict[str, Any], name: str) -> str:
    value = args.get(name)
    if value is None:
        raise KeyError(f"Missing required Trace32 argument '{name}'.")
    return str(value)


def _required_commands(args: Dict[str, Any], name: str) -> List[str]:
    value = args.get(name)
    if value is None:
        raise KeyError(f"Missing required Trace32 argument '{name}'.")
    if isinstance(value, (str, bytes)) or not isinstance(value, Sequence):
        raise ValueError(f"Trace32 '{name}' must be a non-empty list of commands.")
    commands = [str(command) for command in value]
    if not commands or any(not command.strip() for command in commands):
        raise ValueError(f"Trace32 '{name}' must be a non-empty list of commands.")
    return commands
