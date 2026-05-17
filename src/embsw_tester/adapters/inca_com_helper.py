from __future__ import annotations

import json
import sys
from typing import Any, Callable, Iterable, Mapping, Optional, TextIO

from embsw_tester.adapters.inca_bridge import IncaBridgeRequest, IncaBridgeResponse


DispatchFactory = Callable[[str], Any]

INCA_PROG_ID = "Inca.Inca"


class IncaComClient:
    def __init__(
        self,
        dispatch_factory: Optional[DispatchFactory] = None,
        prog_id: str = INCA_PROG_ID,
    ):
        self._dispatch_factory = dispatch_factory or _default_dispatch_factory
        self._prog_id = prog_id
        self._application: Any = None

    def execute(self, request: IncaBridgeRequest) -> IncaBridgeResponse:
        try:
            if request.command_type == "inca.measure.read":
                values = self._read_measurement(request.args)
                return _passed(request, "Read INCA measurement.", values)
            if request.command_type == "inca.calibration.set":
                values = self._set_calibration(request.args)
                return _passed(request, "Set INCA calibration.", values)
            if request.command_type == "inca.recording.start":
                values = self._start_recording(request.args)
                return _passed(request, "INCA recording started.", values)
            if request.command_type == "inca.recording.stop":
                values = self._stop_recording(request.args)
                return _passed(request, "INCA recording stopped.", values)
            return _failed(
                request,
                f"Unsupported INCA command '{request.command_type}'.",
            )
        except Exception as exc:
            return _failed(request, str(exc), error=exc.__class__.__name__)

    def _application_api_version(self) -> Optional[str]:
        application = self._get_application()
        return _call_optional(application, "APIVersion")

    def _get_application(self) -> Any:
        if self._application is None:
            self._application = self._dispatch_factory(self._prog_id)
            _call_optional(self._application, "SuppressMessageBoxes")
        return self._application

    def _get_experiment(self) -> Any:
        application = self._get_application()
        experiment = _call_optional(application, "GetOpenedExperiment")
        if experiment is not None:
            return experiment

        experiment_view = _call_optional(application, "GetOpenedExperimentView")
        if experiment_view is not None:
            experiment = _call_optional(experiment_view, "GetExperiment")
            if experiment is not None:
                return experiment

        experiment = _call_optional(application, "GetExperiment")
        if experiment is not None:
            return experiment

        raise RuntimeError("No opened INCA experiment is available.")

    def _read_measurement(self, args: Mapping[str, Any]) -> dict[str, Any]:
        variable = _required_text(args, "variable")
        device_name = _optional_text(args, "device")
        acquisition_rate = _optional_text(args, "acquisition_rate")
        experiment = self._get_experiment()
        device = self._get_device(experiment, device_name)

        if device is not None and acquisition_rate is not None:
            measure_value = experiment.GetMeasureValueWithAcquisitionRateInDevice(
                variable,
                acquisition_rate,
                device,
            )
        elif device is not None:
            measure_value = experiment.GetMeasureValueInDevice(variable, device)
        elif acquisition_rate is not None:
            measure_value = experiment.GetMeasureValueWithAcquisitionRate(
                variable,
                acquisition_rate,
            )
        else:
            measure_value = experiment.GetMeasureValue(variable)

        values: dict[str, Any] = {
            "variable": variable,
            "value": _json_safe(_read_scalar_value(measure_value)),
        }
        _add_common_values(
            values,
            self._application_api_version(),
            device_name,
            acquisition_rate,
        )
        return values

    def _set_calibration(self, args: Mapping[str, Any]) -> dict[str, Any]:
        parameter = _required_text(args, "parameter")
        if "value" not in args:
            raise KeyError("Missing required INCA argument 'value'.")
        value = args["value"]
        value_kind = str(args.get("value_kind", "phys")).lower()
        if value_kind not in {"phys", "impl"}:
            raise ValueError("'value_kind' must be 'phys' or 'impl'.")

        device_name = _optional_text(args, "device")
        experiment = self._get_experiment()
        device = self._get_device(experiment, device_name)
        if device is not None:
            calibration_value = experiment.GetCalibrationValueInDevice(parameter, device)
        else:
            calibration_value = experiment.GetCalibrationValue(parameter)

        setter_name = _set_scalar_value(calibration_value, value, value_kind)
        values: dict[str, Any] = {
            "parameter": parameter,
            "value": _json_safe(value),
            "value_kind": value_kind,
            "setter": setter_name,
        }
        _add_common_values(values, self._application_api_version(), device_name, None)
        return values

    def _start_recording(self, args: Mapping[str, Any]) -> dict[str, Any]:
        experiment = self._get_experiment()
        name = _optional_text(args, "name")
        output_dir = _optional_text(args, "output_dir")
        file_format = _optional_text(args, "file_format") or _optional_text(
            args,
            "format",
        )

        if name is not None:
            experiment.SetRecordingFileName(name)
        if output_dir is not None:
            experiment.SetRecordingPathName(output_dir)
        if file_format is not None:
            experiment.SetRecordingFileFormat(file_format)
        experiment.StartRecording()

        values: dict[str, Any] = {"recording_active": True}
        if name is not None:
            values["recording_name"] = name
        if output_dir is not None:
            values["output_dir"] = output_dir
        if file_format is not None:
            values["file_format"] = file_format
        api_version = self._application_api_version()
        if api_version is not None:
            values["api_version"] = api_version
        return values

    def _stop_recording(self, args: Mapping[str, Any]) -> dict[str, Any]:
        experiment = self._get_experiment()
        discard = bool(args.get("discard", False))
        if discard:
            experiment.StopAndDiscardRecording()
            values: dict[str, Any] = {
                "recording_active": False,
                "discarded": True,
            }
        else:
            file_name = (
                _optional_text(args, "file_name")
                or _optional_text(args, "name")
                or ""
            )
            file_format = (
                _optional_text(args, "file_format")
                or _optional_text(args, "format")
                or ""
            )
            experiment.StopRecording(file_name, file_format)
            values = {
                "recording_active": False,
                "file_name": file_name,
                "file_format": file_format,
            }

        api_version = self._application_api_version()
        if api_version is not None:
            values["api_version"] = api_version
        return values

    @staticmethod
    def _get_device(experiment: Any, device_name: Optional[str]) -> Any:
        if device_name is None:
            return None
        return experiment.GetDevice(device_name)


def serve(
    stdin: TextIO = sys.stdin,
    stdout: TextIO = sys.stdout,
    client: Optional[IncaComClient] = None,
) -> None:
    client = client or IncaComClient()
    for line in stdin:
        if not line.strip():
            continue
        response = _handle_json_line(line, client)
        stdout.write(json.dumps(response.to_dict(), ensure_ascii=False) + "\n")
        stdout.flush()


def main() -> int:
    serve()
    return 0


def _handle_json_line(line: str, client: IncaComClient) -> IncaBridgeResponse:
    try:
        payload = json.loads(line)
        if not isinstance(payload, Mapping):
            raise ValueError("request must be a JSON object")
        request = IncaBridgeRequest.from_dict(payload)
    except Exception as exc:
        return IncaBridgeResponse(
            request_id="",
            success=False,
            status="failed",
            message=str(exc),
            error=exc.__class__.__name__,
        )
    return client.execute(request)


def _default_dispatch_factory(prog_id: str) -> Any:
    try:
        import pythoncom  # type: ignore[import-not-found]
        import win32com.client  # type: ignore[import-not-found]
    except ImportError as exc:
        raise RuntimeError(
            "INCA COM helper requires pywin32 on Windows 32bit Python."
        ) from exc

    pythoncom.CoInitialize()
    return win32com.client.Dispatch(prog_id)


def _passed(
    request: IncaBridgeRequest,
    message: str,
    values: Mapping[str, Any],
) -> IncaBridgeResponse:
    return IncaBridgeResponse(
        request_id=request.request_id,
        success=True,
        status="passed",
        message=message,
        values=dict(values),
    )


def _failed(
    request: IncaBridgeRequest,
    message: str,
    error: Optional[str] = None,
) -> IncaBridgeResponse:
    return IncaBridgeResponse(
        request_id=request.request_id,
        success=False,
        status="failed",
        message=message,
        error=error,
    )


def _required_text(args: Mapping[str, Any], name: str) -> str:
    value = args.get(name)
    if value is None:
        raise KeyError(f"Missing required INCA argument '{name}'.")
    return str(value)


def _optional_text(args: Mapping[str, Any], name: str) -> Optional[str]:
    value = args.get(name)
    if value is None:
        return None
    return str(value)


def _call_optional(target: Any, method_name: str, *args: Any) -> Any:
    method = getattr(target, method_name, None)
    if method is None:
        return None
    return method(*args)


def _add_common_values(
    values: dict[str, Any],
    api_version: Optional[str],
    device_name: Optional[str],
    acquisition_rate: Optional[str],
) -> None:
    if api_version is not None:
        values["api_version"] = api_version
    if device_name is not None:
        values["device"] = device_name
    if acquisition_rate is not None:
        values["acquisition_rate"] = acquisition_rate


def _read_scalar_value(data_item: Any) -> Any:
    phys_type = str(_call_optional(data_item, "GetPhysType") or "").lower()
    if phys_type:
        method_name = _getter_for_phys_type(phys_type)
        if method_name is not None and hasattr(data_item, method_name):
            return getattr(data_item, method_name)()

    for method_name in (
        "GetDoublePhysValue",
        "GetFloatPhysValue",
        "GetIntegerPhysValue",
        "GetLongPhysValue",
        "GetBooleanPhysValue",
        "GetCharPhysValue",
        "GetImplValue",
    ):
        method = getattr(data_item, method_name, None)
        if method is not None:
            return method()
    raise RuntimeError("INCA data item does not expose a scalar value getter.")


def _getter_for_phys_type(phys_type: str) -> Optional[str]:
    if "bool" in phys_type:
        return "GetBooleanPhysValue"
    if "char" in phys_type or "string" in phys_type:
        return "GetCharPhysValue"
    if "float" in phys_type:
        return "GetFloatPhysValue"
    if "double" in phys_type:
        return "GetDoublePhysValue"
    if "long" in phys_type:
        return "GetLongPhysValue"
    if "int" in phys_type:
        return "GetIntegerPhysValue"
    return None


def _set_scalar_value(target: Any, value: Any, value_kind: str) -> str:
    method_names = list(_setter_candidates(value, value_kind))
    for method_name in method_names:
        method = getattr(target, method_name, None)
        if method is not None:
            method(value)
            return method_name
    raise RuntimeError(
        "INCA calibration value does not expose a compatible setter "
        f"for {value_kind} value {value!r}."
    )


def _setter_candidates(value: Any, value_kind: str) -> Iterable[str]:
    if value_kind == "impl":
        if isinstance(value, bool):
            yield "SetBooleanImplValue"
        elif isinstance(value, int):
            yield "SetIntegerImplValue"
            yield "SetLongImplValue"
        elif isinstance(value, float):
            yield "SetImplValue"
        else:
            yield "SetImplValue"
        return

    if isinstance(value, bool):
        yield "SetBooleanPhysValue"
    elif isinstance(value, int):
        yield "SetIntegerPhysValue"
        yield "SetLongPhysValue"
        yield "SetDoublePhysValue"
    elif isinstance(value, float):
        yield "SetDoublePhysValue"
        yield "SetFloatPhysValue"
    elif isinstance(value, str):
        yield "SetCharPhysValue"
    else:
        yield "SetDoublePhysValue"


def _json_safe(value: Any) -> Any:
    if isinstance(value, (str, int, float, bool)) or value is None:
        return value
    if isinstance(value, Mapping):
        return {str(key): _json_safe(item) for key, item in value.items()}
    if isinstance(value, (list, tuple)):
        return [_json_safe(item) for item in value]
    return str(value)


if __name__ == "__main__":
    raise SystemExit(main())
