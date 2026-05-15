from __future__ import annotations

from typing import Any, Dict, Mapping, Optional

from embsw_tester.adapters.base import AdapterContext, AdapterResult


class IncaAdapter:
    name = "inca"

    def __init__(
        self,
        measurements: Optional[Mapping[str, Any]] = None,
        calibrations: Optional[Mapping[str, Any]] = None,
    ):
        self.measurements: Dict[str, Any] = dict(measurements or {})
        self.calibrations: Dict[str, Any] = dict(calibrations or {})
        self.recording_active = False
        self.recording_name: Optional[str] = None
        self.recording_output_dir: Optional[str] = None

    def execute(
        self,
        command_type: str,
        args: Dict[str, Any],
        context: AdapterContext,
    ) -> AdapterResult:
        if command_type == "inca.measure.read":
            return self._read_measurement(args)
        if command_type == "inca.calibration.set":
            return self._set_calibration(args)
        if command_type == "inca.recording.start":
            return self._start_recording(args)
        if command_type == "inca.recording.stop":
            return self._stop_recording()
        return AdapterResult(
            success=False,
            status="failed",
            message=f"Unsupported INCA command '{command_type}'.",
        )

    def _read_measurement(self, args: Mapping[str, Any]) -> AdapterResult:
        variable = _required_text(args, "variable")
        if variable not in self.measurements:
            return AdapterResult(
                success=False,
                status="failed",
                message=f"INCA measurement '{variable}' is not configured.",
                values={"variable": variable},
            )
        return AdapterResult(
            success=True,
            status="passed",
            message=f"Read INCA measurement '{variable}'.",
            values={
                "variable": variable,
                "value": self.measurements[variable],
            },
        )

    def _set_calibration(self, args: Mapping[str, Any]) -> AdapterResult:
        parameter = _required_text(args, "parameter")
        value = args["value"]
        self.calibrations[parameter] = value
        return AdapterResult(
            success=True,
            status="passed",
            message=f"Set INCA calibration '{parameter}'.",
            values={
                "parameter": parameter,
                "value": value,
            },
        )

    def _start_recording(self, args: Mapping[str, Any]) -> AdapterResult:
        self.recording_active = True
        self.recording_name = str(args["name"]) if "name" in args else None
        self.recording_output_dir = str(args["output_dir"]) if "output_dir" in args else None
        values: Dict[str, Any] = {"recording_active": True}
        if self.recording_name is not None:
            values["recording_name"] = self.recording_name
        if self.recording_output_dir is not None:
            values["output_dir"] = self.recording_output_dir
        return AdapterResult(
            success=True,
            status="passed",
            message="INCA recording started.",
            values=values,
        )

    def _stop_recording(self) -> AdapterResult:
        self.recording_active = False
        values: Dict[str, Any] = {"recording_active": False}
        if self.recording_name is not None:
            values["recording_name"] = self.recording_name
        if self.recording_output_dir is not None:
            values["output_dir"] = self.recording_output_dir
        return AdapterResult(
            success=True,
            status="passed",
            message="INCA recording stopped.",
            values=values,
        )


def _required_text(args: Mapping[str, Any], name: str) -> str:
    value = args.get(name)
    if value is None:
        raise KeyError(f"Missing required INCA argument '{name}'.")
    return str(value)
