from __future__ import annotations

import hashlib
import re
from pathlib import Path
from typing import Any, Dict, Iterable, List, Mapping, MutableMapping, Optional, Sequence, Set, Tuple

import yaml

from embsw_tester.dsl.catalog import COMMAND_SPECS
from embsw_tester.dsl.models import (
    CommandPath,
    Diagnostic,
    FunctionDef,
    NormalizedCommand,
    ResolvedPackage,
    TestcaseDef,
)
from embsw_tester.tools.profile import load_tool_profile


def compile_file(path: Path) -> ResolvedPackage:
    source_file = path.resolve()
    diagnostics: List[Diagnostic] = []
    visited: Set[Path] = set()
    document = _load_yaml_document(source_file, diagnostics)
    functions: Dict[str, FunctionDef] = {}
    imports: List[str] = []
    tool_profile_snapshot: Dict[str, Any] = {}

    if isinstance(document, Mapping):
        tool_profile_snapshot = _load_tool_profile_snapshot(source_file, document, diagnostics)
        for import_ref in _as_list(document.get("imports")):
            import_path = _resolve_import_path(source_file.parent, import_ref)
            imports.append(str(import_path))
            _load_imported_functions(import_path, functions, diagnostics, visited)

        _collect_functions(source_file, document, functions, diagnostics, path_prefix=("functions",))
        testcases = _collect_testcases(source_file, document, functions, diagnostics)
        _validate_device_commands(testcases, functions, tool_profile_snapshot, diagnostics)
    else:
        testcases = []
        diagnostics.append(
            Diagnostic(
                code="INVALID_DOCUMENT",
                message="YAML document must be a mapping at the top level.",
                source_file=str(source_file),
            )
        )

    return ResolvedPackage(
        source_file=source_file,
        source_file_hash=_sha256_file(source_file),
        imports=imports,
        functions=functions,
        testcases=testcases,
        diagnostics=diagnostics,
        tool_profile_snapshot=tool_profile_snapshot,
    )


def _load_tool_profile_snapshot(
    source_file: Path,
    document: Mapping[str, Any],
    diagnostics: List[Diagnostic],
) -> Dict[str, Any]:
    profile_ref = document.get("tool_profile")
    if profile_ref is None:
        inline_tools = document.get("tools")
        return dict(inline_tools) if isinstance(inline_tools, Mapping) else {}

    profile_path = _resolve_import_path(source_file.parent, profile_ref)
    try:
        return load_tool_profile(profile_path)
    except (OSError, ValueError, yaml.YAMLError) as exc:
        diagnostics.append(
            Diagnostic(
                code="INVALID_TOOL_PROFILE",
                message=str(exc),
                source_file=str(profile_path),
            )
        )
        return {}


def _load_imported_functions(
    path: Path,
    functions: MutableMapping[str, FunctionDef],
    diagnostics: List[Diagnostic],
    visited: Set[Path],
) -> None:
    resolved = path.resolve()
    if resolved in visited:
        diagnostics.append(
            Diagnostic(
                code="CYCLIC_IMPORT",
                message=f"Import cycle detected for '{path}'.",
                source_file=str(resolved),
            )
        )
        return

    visited.add(resolved)
    document = _load_yaml_document(resolved, diagnostics)
    if not isinstance(document, Mapping):
        diagnostics.append(
            Diagnostic(
                code="INVALID_IMPORT",
                message=f"Imported YAML '{path}' must be a mapping.",
                source_file=str(resolved),
            )
        )
        visited.remove(resolved)
        return

    for import_ref in _as_list(document.get("imports")):
        nested_path = _resolve_import_path(resolved.parent, import_ref)
        _load_imported_functions(nested_path, functions, diagnostics, visited)

    _collect_functions(resolved, document, functions, diagnostics, path_prefix=("functions",))
    visited.remove(resolved)


def _collect_functions(
    source_file: Path,
    document: Mapping[str, Any],
    functions: MutableMapping[str, FunctionDef],
    diagnostics: List[Diagnostic],
    path_prefix: CommandPath,
) -> None:
    raw_functions = document.get("functions", {})
    if raw_functions is None:
        return
    if not isinstance(raw_functions, Mapping):
        diagnostics.append(
            Diagnostic(
                code="INVALID_FUNCTIONS",
                message="'functions' must be a mapping.",
                path=path_prefix,
                source_file=str(source_file),
            )
        )
        return

    for name, body in raw_functions.items():
        function_path = path_prefix + (str(name),)
        if name in functions:
            diagnostics.append(
                Diagnostic(
                    code="IMPORT_CONFLICT",
                    message=f"Function '{name}' is defined more than once.",
                    path=function_path,
                    source_file=str(source_file),
                )
            )
            continue
        if not isinstance(body, Mapping):
            diagnostics.append(
                Diagnostic(
                    code="INVALID_FUNCTION",
                    message=f"Function '{name}' must be a mapping.",
                    path=function_path,
                    source_file=str(source_file),
                )
            )
            continue

        steps = _normalize_step_list(
            source_file=source_file,
            raw_steps=body.get("steps", []),
            path_prefix=function_path + ("steps",),
            diagnostics=diagnostics,
            functions=functions,
        )
        functions[str(name)] = FunctionDef(
            name=str(name),
            params=[str(item) for item in _as_list(body.get("params"))],
            returns=[str(item) for item in _as_list(body.get("returns"))],
            steps=steps,
            source_file=str(source_file),
        )


def _collect_testcases(
    source_file: Path,
    document: Mapping[str, Any],
    functions: Mapping[str, FunctionDef],
    diagnostics: List[Diagnostic],
) -> List[TestcaseDef]:
    raw_testcases = document.get("testcases", [])
    if raw_testcases is None:
        return []
    if not isinstance(raw_testcases, Sequence) or isinstance(raw_testcases, (str, bytes)):
        diagnostics.append(
            Diagnostic(
                code="INVALID_TESTCASES",
                message="'testcases' must be a list.",
                path=("testcases",),
                source_file=str(source_file),
            )
        )
        return []

    testcases: List[TestcaseDef] = []
    for index, raw_testcase in enumerate(raw_testcases):
        testcase_path: CommandPath = ("testcases", index)
        if not isinstance(raw_testcase, Mapping):
            diagnostics.append(
                Diagnostic(
                    code="INVALID_TESTCASE",
                    message="Each testcase must be a mapping.",
                    path=testcase_path,
                    source_file=str(source_file),
                )
            )
            continue

        name = str(raw_testcase.get("name", f"testcase_{index}"))
        preconditions = _normalize_step_list(
            source_file,
            raw_testcase.get("preconditions", []),
            testcase_path + ("preconditions",),
            diagnostics,
            functions,
        )
        steps = _normalize_step_list(
            source_file,
            raw_testcase.get("steps", []),
            testcase_path + ("steps",),
            diagnostics,
            functions,
        )
        postconditions = _normalize_step_list(
            source_file,
            raw_testcase.get("postconditions", []),
            testcase_path + ("postconditions",),
            diagnostics,
            functions,
        )
        cleanup = _normalize_step_list(
            source_file,
            raw_testcase.get("cleanup", []),
            testcase_path + ("cleanup",),
            diagnostics,
            functions,
        )
        metadata = {
            key: value
            for key, value in raw_testcase.items()
            if key not in {"name", "preconditions", "steps", "postconditions", "cleanup"}
        }
        testcases.append(
            TestcaseDef(
                name=name,
                preconditions=preconditions,
                steps=steps,
                postconditions=postconditions,
                cleanup=cleanup,
                metadata=metadata,
            )
        )

    return testcases


def _normalize_step_list(
    source_file: Path,
    raw_steps: Any,
    path_prefix: CommandPath,
    diagnostics: List[Diagnostic],
    functions: Mapping[str, FunctionDef],
) -> List[NormalizedCommand]:
    if raw_steps is None:
        return []
    if not isinstance(raw_steps, Sequence) or isinstance(raw_steps, (str, bytes)):
        diagnostics.append(
            Diagnostic(
                code="INVALID_STEPS",
                message="A command list must be a YAML sequence.",
                path=path_prefix,
                source_file=str(source_file),
            )
        )
        return []

    commands: List[NormalizedCommand] = []
    for index, raw_step in enumerate(raw_steps):
        command = _normalize_command(
            source_file,
            raw_step,
            path_prefix + (index,),
            diagnostics,
            functions,
        )
        if command is not None:
            commands.append(command)
    return commands


def _normalize_command(
    source_file: Path,
    raw_step: Any,
    path: CommandPath,
    diagnostics: List[Diagnostic],
    functions: Mapping[str, FunctionDef],
) -> Optional[NormalizedCommand]:
    if not isinstance(raw_step, Mapping) or len(raw_step) != 1:
        diagnostics.append(
            Diagnostic(
                code="INVALID_COMMAND",
                message="Each command must be a mapping with exactly one command name.",
                path=path,
                source_file=str(source_file),
            )
        )
        return None

    command_type, raw_args = next(iter(raw_step.items()))
    command_type = str(command_type)
    args = _normalize_args(raw_args)
    command = NormalizedCommand(
        type=command_type,
        args=args,
        path=path,
        source_file=str(source_file),
    )

    _validate_command(command, diagnostics, functions)
    return command


def _validate_command(
    command: NormalizedCommand,
    diagnostics: List[Diagnostic],
    functions: Mapping[str, FunctionDef],
) -> None:
    spec = COMMAND_SPECS.get(command.type)
    if spec is None:
        diagnostics.append(
            Diagnostic(
                code="UNKNOWN_COMMAND",
                message=f"Unknown command '{command.type}'.",
                path=command.path,
                source_file=command.source_file,
            )
        )
        return

    for required_arg in sorted(spec.required_args):
        if required_arg not in command.args:
            diagnostics.append(
                Diagnostic(
                    code="MISSING_ARGUMENT",
                    message=f"Command '{command.type}' requires argument '{required_arg}'.",
                    path=command.path,
                    source_file=command.source_file,
                )
            )

    if command.type == "call":
        _validate_call_command(command, diagnostics, functions)


def _validate_call_command(
    command: NormalizedCommand,
    diagnostics: List[Diagnostic],
    functions: Mapping[str, FunctionDef],
) -> None:
    function_name = command.args.get("function")
    if not isinstance(function_name, str):
        diagnostics.append(
            Diagnostic(
                code="INVALID_FUNCTION_REFERENCE",
                message="'call.function' must be a string.",
                path=command.path,
                source_file=command.source_file,
            )
        )
        return

    function_def = functions.get(function_name)
    if function_def is None:
        diagnostics.append(
            Diagnostic(
                code="UNKNOWN_FUNCTION",
                message=f"Function '{function_name}' is not defined.",
                path=command.path,
                source_file=command.source_file,
            )
        )
        return

    out_mapping = command.args.get("out", {})
    if out_mapping is None:
        return
    if not isinstance(out_mapping, Mapping):
        diagnostics.append(
            Diagnostic(
                code="INVALID_OUT_MAPPING",
                message="'call.out' must be a mapping.",
                path=command.path,
                source_file=command.source_file,
            )
        )
        return

    declared_returns = set(function_def.returns)
    for return_name in out_mapping.keys():
        if return_name not in declared_returns:
            diagnostics.append(
                Diagnostic(
                    code="UNKNOWN_RETURN",
                    message=(
                        f"Function '{function_name}' does not declare return "
                        f"'{return_name}'."
                    ),
                    path=command.path,
                    source_file=command.source_file,
                )
            )


def _validate_device_commands(
    testcases: Sequence[TestcaseDef],
    functions: Mapping[str, FunctionDef],
    tool_profile_snapshot: Mapping[str, Any],
    diagnostics: List[Diagnostic],
) -> None:
    for command in _iter_commands(testcases, functions):
        spec = COMMAND_SPECS.get(command.type)
        if spec is None or spec.category != "device":
            continue
        _validate_device_command(command, tool_profile_snapshot, diagnostics)


def _iter_commands(
    testcases: Sequence[TestcaseDef],
    functions: Mapping[str, FunctionDef],
) -> Iterable[NormalizedCommand]:
    for testcase in testcases:
        yield from testcase.preconditions
        yield from testcase.steps
        yield from testcase.postconditions
        yield from testcase.cleanup
    for function_def in functions.values():
        yield from function_def.steps


def _validate_device_command(
    command: NormalizedCommand,
    tool_profile_snapshot: Mapping[str, Any],
    diagnostics: List[Diagnostic],
) -> None:
    device_name = command.args.get("device")
    if not isinstance(device_name, str):
        diagnostics.append(
            Diagnostic(
                code="INVALID_DEVICE_REFERENCE",
                message=f"Command '{command.type}' requires a literal device name.",
                path=command.path,
                source_file=command.source_file,
            )
        )
        return

    devices = _serial_devices(tool_profile_snapshot)
    device_config = devices.get(device_name)
    if not isinstance(device_config, Mapping):
        diagnostics.append(
            Diagnostic(
                code="UNKNOWN_DEVICE",
                message=f"Device '{device_name}' is not declared in the tool profile.",
                path=command.path,
                source_file=command.source_file,
            )
        )
        return

    profile_name = str(device_config.get("command_profile", ""))
    if not profile_name:
        diagnostics.append(
            Diagnostic(
                code="UNKNOWN_COMMAND_PROFILE",
                message=f"Device '{device_name}' does not declare a command_profile.",
                path=command.path,
                source_file=command.source_file,
            )
        )
        return
    if profile_name == "pending":
        diagnostics.append(
            Diagnostic(
                code="PENDING_COMMAND_PROFILE",
                message=(
                    f"Device '{device_name}' uses pending command_profile; "
                    f"command '{command.type}' cannot be compiled yet."
                ),
                path=command.path,
                source_file=command.source_file,
            )
        )
        return

    command_profiles = tool_profile_snapshot.get("command_profiles", {})
    if not isinstance(command_profiles, Mapping):
        diagnostics.append(
            Diagnostic(
                code="UNKNOWN_COMMAND_PROFILE",
                message="'command_profiles' must be a mapping when device commands are used.",
                path=command.path,
                source_file=command.source_file,
            )
        )
        return

    profile = command_profiles.get(profile_name)
    if not isinstance(profile, Mapping):
        diagnostics.append(
            Diagnostic(
                code="UNKNOWN_COMMAND_PROFILE",
                message=f"Command profile '{profile_name}' for device '{device_name}' is not defined.",
                path=command.path,
                source_file=command.source_file,
            )
        )
        return

    commands = profile.get("commands", {})
    command_definition = commands.get(command.type) if isinstance(commands, Mapping) else None
    if not isinstance(command_definition, Mapping):
        diagnostics.append(
            Diagnostic(
                code="UNKNOWN_COMMAND_PROFILE",
                message=(
                    f"Command profile '{profile_name}' for device '{device_name}' "
                    f"does not define command '{command.type}'."
                ),
                path=command.path,
                source_file=command.source_file,
            )
        )
        return

    _validate_response_patterns(command, profile_name, command_definition, diagnostics)


def _validate_response_patterns(
    command: NormalizedCommand,
    profile_name: str,
    command_definition: Mapping[str, Any],
    diagnostics: List[Diagnostic],
) -> None:
    read_definition = command_definition.get("read")
    if read_definition is None:
        return
    if not isinstance(read_definition, Mapping):
        diagnostics.append(
            Diagnostic(
                code="INVALID_RESPONSE_EXTRACTOR",
                message=(
                    f"Command profile '{profile_name}' command '{command.type}' "
                    "read definition must be a mapping."
                ),
                path=command.path,
                source_file=command.source_file,
            )
        )
        return

    _validate_response_pattern(
        command,
        profile_name,
        read_definition,
        "match",
        "INVALID_RESPONSE_MATCHER",
        diagnostics,
    )
    _validate_response_pattern(
        command,
        profile_name,
        read_definition,
        "extract",
        "INVALID_RESPONSE_EXTRACTOR",
        diagnostics,
    )


def _validate_response_pattern(
    command: NormalizedCommand,
    profile_name: str,
    read_definition: Mapping[str, Any],
    field_name: str,
    diagnostic_code: str,
    diagnostics: List[Diagnostic],
) -> None:
    pattern = read_definition.get(field_name)
    if pattern is None or "{{" in str(pattern):
        return
    try:
        re.compile(str(pattern))
    except re.error as exc:
        diagnostics.append(
            Diagnostic(
                code=diagnostic_code,
                message=(
                    f"Command profile '{profile_name}' command '{command.type}' "
                    f"has invalid read.{field_name}: {exc}."
                ),
                path=command.path,
                source_file=command.source_file,
            )
        )


def _serial_devices(tool_profile_snapshot: Mapping[str, Any]) -> Mapping[str, Any]:
    serial_section = tool_profile_snapshot.get("serial", {})
    if not isinstance(serial_section, Mapping):
        return {}
    devices = serial_section.get("devices", {})
    if not isinstance(devices, Mapping):
        return {}
    return devices


def _normalize_args(raw_args: Any) -> Dict[str, Any]:
    if raw_args is None:
        return {}
    if not isinstance(raw_args, Mapping):
        return {"value": raw_args}
    return dict(raw_args)


def _load_yaml_document(path: Path, diagnostics: List[Diagnostic]) -> Any:
    try:
        return yaml.safe_load(path.read_text(encoding="utf-8")) or {}
    except FileNotFoundError:
        diagnostics.append(
            Diagnostic(
                code="FILE_NOT_FOUND",
                message=f"YAML file '{path}' does not exist.",
                source_file=str(path),
            )
        )
        return {}
    except yaml.YAMLError as exc:
        diagnostics.append(
            Diagnostic(
                code="YAML_PARSE_ERROR",
                message=str(exc),
                source_file=str(path),
            )
        )
        return {}


def _resolve_import_path(base_dir: Path, import_ref: Any) -> Path:
    import_path = Path(str(import_ref))
    if import_path.is_absolute():
        return import_path
    return (base_dir / import_path).resolve()


def _as_list(value: Any) -> List[Any]:
    if value is None:
        return []
    if isinstance(value, list):
        return value
    return [value]


def _sha256_file(path: Path) -> str:
    try:
        return hashlib.sha256(path.read_bytes()).hexdigest()
    except FileNotFoundError:
        return ""
