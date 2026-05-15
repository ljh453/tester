from __future__ import annotations

from typing import Any, Dict

from embsw_tester.adapters.base import AdapterContext, AdapterResult


class MockAdapter:
    def __init__(self, name: str):
        self.name = name

    def execute(
        self,
        command_type: str,
        args: Dict[str, Any],
        context: AdapterContext,
    ) -> AdapterResult:
        return AdapterResult(
            success=True,
            status="passed",
            message=f"Mock adapter '{self.name}' executed {command_type}.",
            values={
                "mode": "mock",
                "adapter": self.name,
                "command_type": command_type,
                "args": dict(args),
                "testcase": context.testcase,
                "phase": context.phase,
            },
        )
