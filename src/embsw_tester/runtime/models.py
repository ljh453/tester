from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any, Dict, List, Optional

from embsw_tester.dsl.models import CommandPath


@dataclass(frozen=True)
class CommandEvent:
    run_id: str
    testcase: str
    phase: str
    command_path: CommandPath
    command_type: str
    status: str
    resolved_inputs: Dict[str, Any] = field(default_factory=dict)
    outputs: Dict[str, Any] = field(default_factory=dict)
    error: Optional[str] = None

    def to_dict(self) -> Dict[str, Any]:
        return {
            "run_id": self.run_id,
            "testcase": self.testcase,
            "phase": self.phase,
            "command_path": list(self.command_path),
            "command_type": self.command_type,
            "status": self.status,
            "resolved_inputs": self.resolved_inputs,
            "outputs": self.outputs,
            "error": self.error,
        }


@dataclass(frozen=True)
class TestcaseResult:
    name: str
    status: str
    variables: Dict[str, Any]
    events: List[CommandEvent]
    error: Optional[str] = None

    def to_dict(self) -> Dict[str, Any]:
        return {
            "name": self.name,
            "status": self.status,
            "variables": self.variables,
            "events": [event.to_dict() for event in self.events],
            "error": self.error,
        }


@dataclass(frozen=True)
class RunResult:
    run_id: str
    status: str
    testcase_results: List[TestcaseResult]
    diagnostics: List[Dict[str, Any]] = field(default_factory=list)

    def to_dict(self) -> Dict[str, Any]:
        return {
            "run_id": self.run_id,
            "status": self.status,
            "testcase_results": [
                testcase_result.to_dict()
                for testcase_result in self.testcase_results
            ],
            "diagnostics": self.diagnostics,
        }
