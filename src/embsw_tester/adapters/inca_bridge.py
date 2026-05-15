from __future__ import annotations

import json
import subprocess
import uuid
from dataclasses import dataclass, field
from typing import Any, Callable, Dict, Mapping, Optional, Protocol, Sequence

from embsw_tester.adapters.base import AdapterResult


class IncaBridgeTransport(Protocol):
    def execute(
        self,
        command_type: str,
        args: Mapping[str, Any],
        timeout_ms: int,
    ) -> AdapterResult:
        ...


@dataclass(frozen=True)
class IncaBridgeRequest:
    request_id: str
    command_type: str
    args: Dict[str, Any]
    timeout_ms: int = 1000

    def to_dict(self) -> Dict[str, Any]:
        return {
            "request_id": self.request_id,
            "command_type": self.command_type,
            "args": self.args,
            "timeout_ms": self.timeout_ms,
        }

    @classmethod
    def from_dict(cls, payload: Mapping[str, Any]) -> "IncaBridgeRequest":
        return cls(
            request_id=str(payload["request_id"]),
            command_type=str(payload["command_type"]),
            args=dict(payload.get("args", {})),
            timeout_ms=int(payload.get("timeout_ms", 1000)),
        )


@dataclass(frozen=True)
class IncaBridgeResponse:
    request_id: str
    success: bool
    status: str
    message: str = ""
    values: Dict[str, Any] = field(default_factory=dict)
    error: Optional[str] = None

    def to_dict(self) -> Dict[str, Any]:
        return {
            "request_id": self.request_id,
            "success": self.success,
            "status": self.status,
            "message": self.message,
            "values": self.values,
            "error": self.error,
        }

    @classmethod
    def from_dict(cls, payload: Mapping[str, Any]) -> "IncaBridgeResponse":
        return cls(
            request_id=str(payload["request_id"]),
            success=bool(payload["success"]),
            status=str(payload["status"]),
            message=str(payload.get("message", "")),
            values=dict(payload.get("values", {})),
            error=payload.get("error"),
        )

    def to_adapter_result(self) -> AdapterResult:
        return AdapterResult(
            success=self.success,
            status=self.status,
            message=self.error or self.message,
            values=dict(self.values),
        )


class JsonLineIncaBridgeTransport:
    def __init__(
        self,
        process: Any,
        request_id_factory: Callable[[], str] = lambda: str(uuid.uuid4()),
    ):
        self._process = process
        self._request_id_factory = request_id_factory

    def execute(
        self,
        command_type: str,
        args: Mapping[str, Any],
        timeout_ms: int,
    ) -> AdapterResult:
        request = IncaBridgeRequest(
            request_id=str(self._request_id_factory()),
            command_type=command_type,
            args=dict(args),
            timeout_ms=int(timeout_ms),
        )
        try:
            self._write_request(request)
            response = self._read_response()
        except Exception as exc:
            return _failed_bridge_result(f"INCA bridge transport failed: {exc}.")

        if response.request_id != request.request_id:
            return _failed_bridge_result(
                "INCA bridge returned unexpected response id "
                f"'{response.request_id}' for request '{request.request_id}'."
            )
        return response.to_adapter_result()

    def _write_request(self, request: IncaBridgeRequest) -> None:
        stdin = getattr(self._process, "stdin", None)
        if stdin is None:
            raise RuntimeError("helper process stdin is not available")
        stdin.write(json.dumps(request.to_dict(), ensure_ascii=False) + "\n")
        stdin.flush()

    def _read_response(self) -> IncaBridgeResponse:
        stdout = getattr(self._process, "stdout", None)
        if stdout is None:
            raise RuntimeError("helper process stdout is not available")
        line = stdout.readline()
        if not line:
            raise RuntimeError("helper process returned no response")
        try:
            payload = json.loads(line)
        except json.JSONDecodeError as exc:
            raise RuntimeError(f"helper process returned invalid JSON: {exc}") from exc
        if not isinstance(payload, Mapping):
            raise RuntimeError("helper process response must be a JSON object")
        return IncaBridgeResponse.from_dict(payload)


def create_inca_bridge_process_transport(
    command: Sequence[str],
    popen_factory: Callable[..., Any] = subprocess.Popen,
    request_id_factory: Callable[[], str] = lambda: str(uuid.uuid4()),
) -> JsonLineIncaBridgeTransport:
    process = popen_factory(
        list(command),
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        encoding="utf-8",
        bufsize=1,
    )
    return JsonLineIncaBridgeTransport(
        process,
        request_id_factory=request_id_factory,
    )


def _failed_bridge_result(message: str) -> AdapterResult:
    return AdapterResult(
        success=False,
        status="failed",
        message=message,
    )
