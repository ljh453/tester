from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any, Dict, Optional, Protocol


@dataclass(frozen=True)
class AdapterContext:
    run_id: str
    testcase: str
    phase: str


@dataclass(frozen=True)
class AdapterResult:
    success: bool
    status: str
    message: str = ""
    values: Dict[str, Any] = field(default_factory=dict)
    raw_evidence_ref: Optional[str] = None
    duration_ms: int = 0

    def to_outputs(self) -> Dict[str, Any]:
        outputs: Dict[str, Any] = {
            "success": self.success,
            "status": self.status,
            "message": self.message,
            "values": self.values,
            "duration_ms": self.duration_ms,
        }
        if self.raw_evidence_ref is not None:
            outputs["raw_evidence_ref"] = self.raw_evidence_ref
        return outputs


class Adapter(Protocol):
    name: str

    def execute(
        self,
        command_type: str,
        args: Dict[str, Any],
        context: AdapterContext,
    ) -> AdapterResult:
        ...
