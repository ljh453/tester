from __future__ import annotations

from typing import Any, Dict, Mapping, Optional

from embsw_tester.adapters.base import AdapterContext, AdapterResult
from embsw_tester.adapters.canoe_bridge import CanoeBridgeTransport


SUPPORTED_CANOE_COMMANDS = {
    "canoe.measurement.start",
    "canoe.measurement.stop",
    "canoe.sysvar.set",
    "canoe.sysvar.read",
    "canoe.signal.read",
}


class CanoeAdapter:
    name = "canoe"

    def __init__(
        self,
        system_variables: Optional[Mapping[str, Any]] = None,
        signals: Optional[Mapping[str, Any]] = None,
        bridge_transport: Optional[CanoeBridgeTransport] = None,
    ):
        self.measurement_running = False
        self.system_variables: Dict[str, Any] = dict(system_variables or {})
        self.signals: Dict[str, Any] = dict(signals or {})
        self._bridge_transport = bridge_transport

    def execute(
        self,
        command_type: str,
        args: Dict[str, Any],
        context: AdapterContext,
    ) -> AdapterResult:
        if self._bridge_transport is not None:
            return self._execute_bridge(command_type, args)
        if command_type == "canoe.measurement.start":
            return self._start_measurement(args)
        if command_type == "canoe.measurement.stop":
            return self._stop_measurement()
        if command_type == "canoe.sysvar.set":
            return self._set_system_variable(args)
        if command_type == "canoe.sysvar.read":
            return self._read_system_variable(args)
        if command_type == "canoe.signal.read":
            return self._read_signal(args)
        return AdapterResult(
            success=False,
            status="failed",
            message=f"Unsupported CANoe command '{command_type}'.",
        )

    def _execute_bridge(self, command_type: str, args: Mapping[str, Any]) -> AdapterResult:
        if command_type not in SUPPORTED_CANOE_COMMANDS:
            return AdapterResult(
                success=False,
                status="failed",
                message=f"Unsupported CANoe command '{command_type}'.",
            )
        bridge_args = dict(args)
        timeout_ms = int(bridge_args.pop("timeout_ms", 1000))
        return self._bridge_transport.execute(command_type, bridge_args, timeout_ms)

    def _start_measurement(self, args: Mapping[str, Any]) -> AdapterResult:
        self.measurement_running = True
        values = {"measurement_running": True}
        if "configuration" in args:
            values["configuration"] = args["configuration"]
        return AdapterResult(
            success=True,
            status="passed",
            message="CANoe/CANalyzer measurement started.",
            values=values,
        )

    def _stop_measurement(self) -> AdapterResult:
        self.measurement_running = False
        return AdapterResult(
            success=True,
            status="passed",
            message="CANoe/CANalyzer measurement stopped.",
            values={"measurement_running": False},
        )

    def _set_system_variable(self, args: Mapping[str, Any]) -> AdapterResult:
        namespace = _required_text(args, "namespace")
        name = _required_text(args, "name")
        value = args["value"]
        key = _system_variable_key(namespace, name)
        self.system_variables[key] = value
        return AdapterResult(
            success=True,
            status="passed",
            message=f"Set CANoe system variable '{key}'.",
            values={
                "namespace": namespace,
                "name": name,
                "key": key,
                "value": value,
            },
        )

    def _read_system_variable(self, args: Mapping[str, Any]) -> AdapterResult:
        namespace = _required_text(args, "namespace")
        name = _required_text(args, "name")
        key = _system_variable_key(namespace, name)
        if key not in self.system_variables:
            return AdapterResult(
                success=False,
                status="failed",
                message=f"CANoe system variable '{key}' is not configured.",
                values={
                    "namespace": namespace,
                    "name": name,
                    "key": key,
                },
            )
        value = self.system_variables[key]
        return AdapterResult(
            success=True,
            status="passed",
            message=f"Read CANoe system variable '{key}'.",
            values={
                "namespace": namespace,
                "name": name,
                "key": key,
                "value": value,
            },
        )

    def _read_signal(self, args: Mapping[str, Any]) -> AdapterResult:
        signal = _required_text(args, "signal")
        if signal not in self.signals:
            return AdapterResult(
                success=False,
                status="failed",
                message=f"CANoe signal '{signal}' is not configured.",
                values={"signal": signal},
            )
        values = {
            "signal": signal,
            "value": self.signals[signal],
        }
        for optional_name in ("bus", "channel"):
            if optional_name in args:
                values[optional_name] = args[optional_name]
        return AdapterResult(
            success=True,
            status="passed",
            message=f"Read CANoe signal '{signal}'.",
            values=values,
        )


def _required_text(args: Mapping[str, Any], name: str) -> str:
    value = args.get(name)
    if value is None:
        raise KeyError(f"Missing required CANoe argument '{name}'.")
    return str(value)


def _system_variable_key(namespace: str, name: str) -> str:
    return f"{namespace}::{name}"
