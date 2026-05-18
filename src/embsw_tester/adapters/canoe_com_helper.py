from __future__ import annotations

import argparse
import json
import sys
from typing import Any, Callable, Mapping, Optional, TextIO

from embsw_tester.adapters.canoe_bridge import (
    CanoeBridgeRequest,
    CanoeBridgeResponse,
)


DispatchFactory = Callable[[str], Any]

CANOE_PROG_ID = "CANoe.Application"
CANALYZER_PROG_ID = "CANalyzer.Application"


class CanoeComClient:
    def __init__(
        self,
        dispatch_factory: Optional[DispatchFactory] = None,
        application_name: str = "canoe",
        prog_id: Optional[str] = None,
    ):
        self._dispatch_factory = dispatch_factory or _default_dispatch_factory
        self._application_name = application_name.strip().lower()
        self._prog_id = prog_id or _prog_id_for_application(self._application_name)
        self._application: Any = None

    def execute(self, request: CanoeBridgeRequest) -> CanoeBridgeResponse:
        try:
            if request.command_type == "canoe.measurement.start":
                values = self._start_measurement(request.args)
                return _passed(request, "CANoe/CANalyzer measurement started.", values)
            if request.command_type == "canoe.measurement.stop":
                values = self._stop_measurement()
                return _passed(request, "CANoe/CANalyzer measurement stopped.", values)
            if request.command_type == "canoe.sysvar.set":
                values = self._set_system_variable(request.args)
                return _passed(request, "Set CANoe/CANalyzer system variable.", values)
            if request.command_type == "canoe.sysvar.read":
                values = self._read_system_variable(request.args)
                return _passed(request, "Read CANoe/CANalyzer system variable.", values)
            if request.command_type == "canoe.signal.read":
                values = self._read_signal(request.args)
                return _passed(request, "Read CANoe/CANalyzer signal.", values)
            return _failed(
                request,
                f"Unsupported CANoe command '{request.command_type}'.",
            )
        except Exception as exc:
            return _failed(request, str(exc), error=exc.__class__.__name__)

    def _get_application(self) -> Any:
        if self._application is None:
            self._application = self._dispatch_factory(self._prog_id)
        return self._application

    def _start_measurement(self, args: Mapping[str, Any]) -> dict[str, Any]:
        application = self._get_application()
        configuration = _optional_text(args, "configuration")
        if configuration:
            application.Open(configuration)
        application.Measurement.Start()

        values = self._common_values()
        values["measurement_running"] = _measurement_running(application, True)
        if configuration:
            values["configuration"] = configuration
        return values

    def _stop_measurement(self) -> dict[str, Any]:
        application = self._get_application()
        application.Measurement.Stop()

        values = self._common_values()
        values["measurement_running"] = _measurement_running(application, False)
        return values

    def _set_system_variable(self, args: Mapping[str, Any]) -> dict[str, Any]:
        namespace = _required_text(args, "namespace")
        name = _required_text(args, "name")
        if "value" not in args:
            raise KeyError("Missing required CANoe argument 'value'.")
        value = args["value"]

        variable = self._system_variable(namespace, name)
        variable.Value = value

        values = self._common_values()
        values.update(
            {
                "namespace": namespace,
                "name": name,
                "key": _system_variable_key(namespace, name),
                "value": _json_safe(value),
            }
        )
        return values

    def _read_system_variable(self, args: Mapping[str, Any]) -> dict[str, Any]:
        namespace = _required_text(args, "namespace")
        name = _required_text(args, "name")

        variable = self._system_variable(namespace, name)
        value = getattr(variable, "Value")

        values = self._common_values()
        values.update(
            {
                "namespace": namespace,
                "name": name,
                "key": _system_variable_key(namespace, name),
                "value": _json_safe(value),
            }
        )
        return values

    def _read_signal(self, args: Mapping[str, Any]) -> dict[str, Any]:
        bus = _optional_text(args, "bus") or "CAN"
        channel = int(args.get("channel", 1))
        message = _required_text(args, "message")
        signal = _required_text(args, "signal")

        application = self._get_application()
        bus_object = _collection_item(application.Bus, bus)
        signal_object = bus_object.GetSignal(channel, message, signal)
        value = getattr(signal_object, "Value")

        values = self._common_values()
        values.update(
            {
                "bus": bus,
                "channel": channel,
                "message": message,
                "signal": signal,
                "value": _json_safe(value),
            }
        )
        return values

    def _system_variable(self, namespace: str, name: str) -> Any:
        application = self._get_application()
        namespace_object = _collection_item(application.System.Namespaces, namespace)
        return _collection_item(namespace_object.Variables, name)

    def _common_values(self) -> dict[str, Any]:
        return {
            "application": self._application_name,
            "prog_id": self._prog_id,
        }


def serve(
    stdin: TextIO = sys.stdin,
    stdout: TextIO = sys.stdout,
    client: Optional[CanoeComClient] = None,
) -> None:
    client = client or CanoeComClient()
    for line in stdin:
        if not line.strip():
            continue
        response = _handle_json_line(line, client)
        stdout.write(json.dumps(response.to_dict(), ensure_ascii=False) + "\n")
        stdout.flush()


def main(argv: Optional[list[str]] = None) -> int:
    parser = argparse.ArgumentParser(description="CANoe/CANalyzer COM JSON-line helper.")
    parser.add_argument(
        "--application",
        choices=("canoe", "canalyzer"),
        default="canoe",
        help="Vector application ProgID family to dispatch.",
    )
    parser.add_argument(
        "--prog-id",
        default=None,
        help="Explicit COM ProgID. Overrides --application.",
    )
    args = parser.parse_args(argv)
    serve(client=CanoeComClient(application_name=args.application, prog_id=args.prog_id))
    return 0


def _handle_json_line(line: str, client: CanoeComClient) -> CanoeBridgeResponse:
    try:
        payload = json.loads(line)
        if not isinstance(payload, Mapping):
            raise ValueError("request must be a JSON object")
        request = CanoeBridgeRequest.from_dict(payload)
    except Exception as exc:
        return CanoeBridgeResponse(
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
            "CANoe/CANalyzer COM helper requires pywin32 on Windows Python."
        ) from exc

    pythoncom.CoInitialize()
    return win32com.client.Dispatch(prog_id)


def _prog_id_for_application(application_name: str) -> str:
    if application_name == "canalyzer":
        return CANALYZER_PROG_ID
    if application_name == "canoe":
        return CANOE_PROG_ID
    raise ValueError("'application_name' must be 'canoe' or 'canalyzer'.")


def _passed(
    request: CanoeBridgeRequest,
    message: str,
    values: Mapping[str, Any],
) -> CanoeBridgeResponse:
    return CanoeBridgeResponse(
        request_id=request.request_id,
        success=True,
        status="passed",
        message=message,
        values=dict(values),
    )


def _failed(
    request: CanoeBridgeRequest,
    message: str,
    error: Optional[str] = None,
) -> CanoeBridgeResponse:
    return CanoeBridgeResponse(
        request_id=request.request_id,
        success=False,
        status="failed",
        message=message,
        error=error,
    )


def _required_text(args: Mapping[str, Any], name: str) -> str:
    value = args.get(name)
    if value is None:
        raise KeyError(f"Missing required CANoe argument '{name}'.")
    return str(value)


def _optional_text(args: Mapping[str, Any], name: str) -> Optional[str]:
    value = args.get(name)
    if value is None:
        return None
    return str(value)


def _collection_item(collection: Any, name: Any) -> Any:
    if callable(collection):
        return collection(name)

    item = getattr(collection, "Item", None)
    if item is not None:
        return item(name)

    get = getattr(collection, "Get", None)
    if get is not None:
        return get(name)

    try:
        return collection[name]
    except Exception as exc:
        raise RuntimeError(f"Cannot get '{name}' from COM collection.") from exc


def _measurement_running(application: Any, fallback: bool) -> bool:
    measurement = application.Measurement
    try:
        return bool(getattr(measurement, "Running"))
    except Exception:
        return fallback


def _system_variable_key(namespace: str, name: str) -> str:
    return f"{namespace}::{name}"


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
