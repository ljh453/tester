from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Dict, List, Optional, Tuple, Union

PathPart = Union[str, int]
CommandPath = Tuple[PathPart, ...]


@dataclass(frozen=True)
class Diagnostic:
    code: str
    message: str
    severity: str = "error"
    path: CommandPath = field(default_factory=tuple)
    source_file: Optional[str] = None

    def to_dict(self) -> Dict[str, Any]:
        return {
            "code": self.code,
            "message": self.message,
            "severity": self.severity,
            "path": list(self.path),
            "source_file": self.source_file,
        }


@dataclass(frozen=True)
class NormalizedCommand:
    type: str
    args: Dict[str, Any]
    path: CommandPath = field(default_factory=tuple)
    source_file: Optional[str] = None

    def to_dict(self) -> Dict[str, Any]:
        return {
            "type": self.type,
            "args": self.args,
            "path": list(self.path),
            "source_file": self.source_file,
        }


@dataclass(frozen=True)
class FunctionDef:
    name: str
    params: List[str]
    returns: List[str]
    steps: List[NormalizedCommand]
    source_file: Optional[str] = None

    def to_dict(self) -> Dict[str, Any]:
        return {
            "name": self.name,
            "params": self.params,
            "returns": self.returns,
            "steps": [step.to_dict() for step in self.steps],
            "source_file": self.source_file,
        }


@dataclass(frozen=True)
class TestcaseDef:
    name: str
    preconditions: List[NormalizedCommand] = field(default_factory=list)
    steps: List[NormalizedCommand] = field(default_factory=list)
    postconditions: List[NormalizedCommand] = field(default_factory=list)
    cleanup: List[NormalizedCommand] = field(default_factory=list)
    metadata: Dict[str, Any] = field(default_factory=dict)

    def to_dict(self) -> Dict[str, Any]:
        return {
            "name": self.name,
            "preconditions": [step.to_dict() for step in self.preconditions],
            "steps": [step.to_dict() for step in self.steps],
            "postconditions": [step.to_dict() for step in self.postconditions],
            "cleanup": [step.to_dict() for step in self.cleanup],
            "metadata": self.metadata,
        }


@dataclass(frozen=True)
class ResolvedPackage:
    source_file: Path
    source_file_hash: str
    imports: List[str]
    functions: Dict[str, FunctionDef]
    testcases: List[TestcaseDef]
    diagnostics: List[Diagnostic]
    schema_version: str = "0.1"
    engine_version: str = "0.1.0"

    def to_dict(self) -> Dict[str, Any]:
        return {
            "schema_version": self.schema_version,
            "engine_version": self.engine_version,
            "source_file": str(self.source_file),
            "source_file_hash": self.source_file_hash,
            "imports": self.imports,
            "functions": {
                name: function.to_dict()
                for name, function in sorted(self.functions.items())
            },
            "testcases": [testcase.to_dict() for testcase in self.testcases],
            "diagnostics": [diagnostic.to_dict() for diagnostic in self.diagnostics],
        }
