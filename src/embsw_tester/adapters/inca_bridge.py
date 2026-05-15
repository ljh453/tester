from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any, Dict, Mapping, Optional

from embsw_tester.adapters.base import AdapterResult


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
