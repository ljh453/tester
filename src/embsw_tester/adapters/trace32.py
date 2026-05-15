from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Dict, List, Optional, Protocol, Tuple

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
        if command_type != "trace32.command":
            return AdapterResult(
                success=False,
                status="failed",
                message=f"Unsupported Trace32 command '{command_type}'.",
            )
        return self._execute_command(args)

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


def _required_text(args: Dict[str, Any], name: str) -> str:
    value = args.get(name)
    if value is None:
        raise KeyError(f"Missing required Trace32 argument '{name}'.")
    return str(value)
