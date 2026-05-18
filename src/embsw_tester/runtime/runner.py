from __future__ import annotations

import asyncio
import inspect
import time
import uuid
from copy import deepcopy
from dataclasses import dataclass, field
import json
from pathlib import Path
from typing import Any, Callable, Dict, Iterable, List, Mapping, Optional, Sequence

from embsw_tester.adapters import AdapterContext, AdapterRegistry, create_default_adapter_registry
from embsw_tester.devices.command_profiles import DeviceCommandError, execute_device_command
from embsw_tester.dsl.catalog import COMMAND_SPECS
from embsw_tester.dsl.models import FunctionDef, NormalizedCommand, ResolvedPackage, TestcaseDef
from embsw_tester.runtime.expressions import ExpressionError, evaluate_value
from embsw_tester.runtime.models import CommandEvent, RunResult, TestcaseResult


class CommandFailed(RuntimeError):
    """Raised for expected DSL command failures such as failed assertions."""


class CommandAborted(RuntimeError):
    """Raised when the active run control requests a user stop."""

    def __init__(self, reason: str = "stop_requested") -> None:
        self.reason = reason
        super().__init__("Run stopped by user.")


@dataclass
class Frame:
    variables: Dict[str, Any] = field(default_factory=dict)


@dataclass
class RuntimeContext:
    run_id: str
    package: ResolvedPackage
    sleep_fn: Callable[[float], Any]
    adapter_registry: AdapterRegistry
    event_callback: Optional[Callable[[CommandEvent], Any]] = None
    run_control: Optional["RuntimeControl"] = None


@dataclass
class RuntimeControl:
    control_file: Optional[Path] = None
    breakpoint_lines: set[int] = field(default_factory=set)
    poll_interval_s: float = 0.05

    def pause_reason(self, command: NormalizedCommand) -> Optional[str]:
        if self.control_file is None:
            return None
        payload = self.read_payload()
        if self.stop_reason(payload) is not None:
            return None
        if command.source_line in self.active_breakpoint_lines(payload):
            self.write_state("paused", reason="breakpoint")
            return "breakpoint"
        if self.read_state(payload) == "paused":
            return "pause_requested"
        return None

    def stop_reason(self, payload: Optional[Mapping[str, Any]] = None) -> Optional[str]:
        if self.control_file is None:
            return None
        current_payload = self.read_payload() if payload is None else payload
        if self.read_state(current_payload) not in {"stopping", "stop_requested", "aborted"}:
            return None
        reason = current_payload.get("reason")
        return str(reason) if reason else "stop_requested"

    def wait_until_resumed(self, sleep_fn: Callable[[float], None]) -> None:
        if self.control_file is None:
            return
        while self.read_state() == "paused":
            sleep_fn(self.poll_interval_s)

    async def wait_until_resumed_async(self, sleep_fn: Callable[[float], Any]) -> None:
        if self.control_file is None:
            return
        while self.read_state() == "paused":
            await _maybe_await(sleep_fn(self.poll_interval_s))

    def read_payload(self) -> Mapping[str, Any]:
        if self.control_file is None or not self.control_file.exists():
            return {}
        try:
            payload = json.loads(self.control_file.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError):
            return {}
        return payload if isinstance(payload, Mapping) else {}

    def read_state(self, payload: Optional[Mapping[str, Any]] = None) -> str:
        current_payload = self.read_payload() if payload is None else payload
        state = current_payload.get("state")
        return str(state) if state else "running"

    def active_breakpoint_lines(self, payload: Optional[Mapping[str, Any]] = None) -> set[int]:
        current_payload = self.read_payload() if payload is None else payload
        if "breakpoint_lines" not in current_payload:
            return set(self.breakpoint_lines)
        raw_lines = current_payload.get("breakpoint_lines")
        if not isinstance(raw_lines, Sequence) or isinstance(raw_lines, (str, bytes)):
            return set()
        return {
            int(line)
            for line in raw_lines
            if isinstance(line, int) or isinstance(line, str) and line.isdigit()
        }

    def write_state(self, state: str, reason: Optional[str] = None) -> None:
        if self.control_file is None:
            return
        current_payload = self.read_payload()
        breakpoint_lines = self.active_breakpoint_lines(current_payload)
        payload: Dict[str, Any] = {
            "state": state,
            "breakpoint_lines": sorted(breakpoint_lines),
        }
        if reason is not None:
            payload["reason"] = reason
        self.control_file.parent.mkdir(parents=True, exist_ok=True)
        self.control_file.write_text(
            json.dumps(payload, ensure_ascii=False),
            encoding="utf-8",
        )


def run_package(
    package: ResolvedPackage,
    run_id: Optional[str] = None,
    sleep_fn: Callable[[float], None] = time.sleep,
    adapter_registry: Optional[AdapterRegistry] = None,
    event_callback: Optional[Callable[[CommandEvent], None]] = None,
    run_control: Optional[RuntimeControl] = None,
    testcase_names: Optional[Sequence[str]] = None,
) -> RunResult:
    try:
        asyncio.get_running_loop()
    except RuntimeError:
        return asyncio.run(
            run_package_async(
                package,
                run_id=run_id,
                sleep_fn=sleep_fn,
                adapter_registry=adapter_registry,
                event_callback=event_callback,
                run_control=run_control,
                testcase_names=testcase_names,
            )
        )
    raise RuntimeError("run_package cannot be called from a running event loop; use run_package_async.")


async def run_package_async(
    package: ResolvedPackage,
    run_id: Optional[str] = None,
    sleep_fn: Callable[[float], Any] = asyncio.sleep,
    adapter_registry: Optional[AdapterRegistry] = None,
    event_callback: Optional[Callable[[CommandEvent], Any]] = None,
    run_control: Optional[RuntimeControl] = None,
    testcase_names: Optional[Sequence[str]] = None,
) -> RunResult:
    resolved_run_id = run_id or str(uuid.uuid4())
    diagnostics = [diagnostic.to_dict() for diagnostic in package.diagnostics]
    if diagnostics:
        return RunResult(
            run_id=resolved_run_id,
            status="failed",
            testcase_results=[],
            diagnostics=diagnostics,
        )

    context = RuntimeContext(
        run_id=resolved_run_id,
        package=package,
        sleep_fn=sleep_fn,
        adapter_registry=adapter_registry or create_default_adapter_registry(),
        event_callback=event_callback,
        run_control=run_control,
    )
    testcases, testcase_filter_diagnostics = _select_testcases(package.testcases, testcase_names)
    if testcase_filter_diagnostics:
        return RunResult(
            run_id=resolved_run_id,
            status="failed",
            testcase_results=[],
            diagnostics=testcase_filter_diagnostics,
        )

    testcase_results = []
    for testcase in testcases:
        testcase_result = await _run_testcase_async(context, testcase)
        testcase_results.append(testcase_result)
        if testcase_result.status == "aborted":
            break

    if any(result.status == "aborted" for result in testcase_results):
        run_status = "aborted"
    elif any(result.status == "failed" for result in testcase_results):
        run_status = "failed"
    else:
        run_status = "passed"
    return RunResult(
        run_id=resolved_run_id,
        status=run_status,
        testcase_results=testcase_results,
    )


def _select_testcases(
    testcases: Sequence[TestcaseDef],
    testcase_names: Optional[Sequence[str]],
) -> tuple[Sequence[TestcaseDef], List[Dict[str, Any]]]:
    requested_names = [
        str(name)
        for name in testcase_names or []
        if str(name).strip()
    ]
    if not requested_names:
        return testcases, []

    requested_name_set = set(requested_names)
    selected = [
        testcase
        for testcase in testcases
        if testcase.name in requested_name_set
    ]
    selected_name_set = {testcase.name for testcase in selected}
    missing_names = [
        name
        for name in dict.fromkeys(requested_names)
        if name not in selected_name_set
    ]
    if missing_names:
        return [], [
            {
                "severity": "error",
                "code": "TESTCASE_NOT_FOUND",
                "message": "Requested testcase(s) not found: "
                + ", ".join(missing_names),
            }
        ]
    return selected, []


async def _run_testcase_async(context: RuntimeContext, testcase: TestcaseDef) -> TestcaseResult:
    frame = Frame()
    events: List[CommandEvent] = []
    status = "passed"
    error: Optional[str] = None

    for phase, commands in (
        ("preconditions", testcase.preconditions),
        ("steps", testcase.steps),
        ("postconditions", testcase.postconditions),
    ):
        phase_status, phase_error = await _run_commands_async(
            context,
            testcase.name,
            phase,
            commands,
            frame,
            events,
        )
        if phase_status in {"failed", "aborted"}:
            status = phase_status
            error = phase_error
            break

    if testcase.cleanup and status != "aborted":
        cleanup_status, cleanup_error = await _run_commands_async(
            context,
            testcase.name,
            "cleanup",
            testcase.cleanup,
            frame,
            events,
        )
        if cleanup_status == "aborted":
            status = "aborted"
            error = cleanup_error
        elif cleanup_status == "failed" and status == "passed":
            status = "failed"
            error = cleanup_error

    return TestcaseResult(
        name=testcase.name,
        status=status,
        variables=dict(frame.variables),
        events=events,
        error=error,
    )


async def _run_commands_async(
    context: RuntimeContext,
    testcase_name: str,
    phase: str,
    commands: Sequence[NormalizedCommand],
    frame: Frame,
    events: List[CommandEvent],
) -> tuple[str, Optional[str]]:
    for command in commands:
        abort_event = _abort_event_if_needed(context, testcase_name, phase, command, frame)
        if abort_event is not None:
            events.append(abort_event)
            await _emit_event_async(context, abort_event)
            return "aborted", abort_event.error

        pause_event = _pause_event_if_needed(context, testcase_name, phase, command, frame)
        if pause_event is not None:
            events.append(pause_event)
            await _emit_event_async(context, pause_event)
            if context.run_control is not None:
                await context.run_control.wait_until_resumed_async(context.sleep_fn)

            abort_event = _abort_event_if_needed(context, testcase_name, phase, command, frame)
            if abort_event is not None:
                events.append(abort_event)
                await _emit_event_async(context, abort_event)
                return "aborted", abort_event.error

        await _emit_event_async(context, _running_event(context, testcase_name, phase, command, frame))
        event = await _execute_command_async(context, testcase_name, phase, command, frame, events)
        events.append(event)
        await _emit_event_async(context, event)
        if event.status == "aborted":
            return "aborted", event.error
        if event.status == "failed":
            return "failed", event.error
    return "passed", None


async def _emit_event_async(context: RuntimeContext, event: CommandEvent) -> None:
    if context.event_callback is None:
        return
    await _maybe_await(context.event_callback(event))


async def _maybe_await(value: Any) -> Any:
    if inspect.isawaitable(value):
        return await value
    return value


def _pause_event_if_needed(
    context: RuntimeContext,
    testcase_name: str,
    phase: str,
    command: NormalizedCommand,
    frame: Frame,
) -> Optional[CommandEvent]:
    if context.run_control is None:
        return None
    reason = context.run_control.pause_reason(command)
    if reason is None:
        return None
    return _event(
        context,
        testcase_name,
        phase,
        command,
        "paused",
        {},
        {"reason": reason},
        local_variables=deepcopy(frame.variables),
    )


def _abort_event_if_needed(
    context: RuntimeContext,
    testcase_name: str,
    phase: str,
    command: NormalizedCommand,
    frame: Frame,
) -> Optional[CommandEvent]:
    if context.run_control is None:
        return None
    reason = context.run_control.stop_reason()
    if reason is None:
        return None
    return _event(
        context,
        testcase_name,
        phase,
        command,
        "aborted",
        {},
        {"reason": reason},
        "Run stopped by user.",
        local_variables=deepcopy(frame.variables),
    )


def _running_event(
    context: RuntimeContext,
    testcase_name: str,
    phase: str,
    command: NormalizedCommand,
    frame: Frame,
) -> CommandEvent:
    return _event(
        context,
        testcase_name,
        phase,
        command,
        "running",
        {},
        {},
        local_variables=deepcopy(frame.variables),
    )


async def _execute_command_async(
    context: RuntimeContext,
    testcase_name: str,
    phase: str,
    command: NormalizedCommand,
    frame: Frame,
    events: List[CommandEvent],
) -> CommandEvent:
    try:
        resolved_inputs, outputs = await _dispatch_command_async(
            context,
            testcase_name,
            phase,
            command,
            frame,
            events,
        )
        return _event(
            context,
            testcase_name,
            phase,
            command,
            "passed",
            resolved_inputs,
            outputs,
            local_variables=deepcopy(frame.variables),
        )
    except CommandAborted as exc:
        return _event(
            context,
            testcase_name,
            phase,
            command,
            "aborted",
            {},
            {"reason": exc.reason},
            str(exc),
            local_variables=deepcopy(frame.variables),
        )
    except (CommandFailed, DeviceCommandError, ExpressionError, KeyError) as exc:
        return _event(
            context,
            testcase_name,
            phase,
            command,
            "failed",
            {},
            {},
            str(exc),
            local_variables=deepcopy(frame.variables),
        )


async def _dispatch_command_async(
    context: RuntimeContext,
    testcase_name: str,
    phase: str,
    command: NormalizedCommand,
    frame: Frame,
    events: List[CommandEvent],
) -> tuple[Dict[str, Any], Dict[str, Any]]:
    if command.type == "set":
        var_name = str(command.args["var"])
        value = evaluate_value(command.args["value"], frame.variables)
        frame.variables[var_name] = value
        return {"var": var_name, "value": value}, {"var": var_name, "value": value}

    if command.type == "assert.eq":
        left = evaluate_value(command.args["left"], frame.variables)
        right = evaluate_value(command.args["right"], frame.variables)
        if left != right:
            raise CommandFailed(f"assert.eq failed: {left!r} != {right!r}")
        return {"left": left, "right": right}, {"passed": True}

    if command.type == "assert.gt":
        left = evaluate_value(command.args["left"], frame.variables)
        right = evaluate_value(command.args["right"], frame.variables)
        if not left > right:
            raise CommandFailed(f"assert.gt failed: {left!r} <= {right!r}")
        return {"left": left, "right": right}, {"passed": True}

    if command.type == "assert.fail":
        message = str(evaluate_value(command.args["message"], frame.variables))
        raise CommandFailed(message)

    if command.type == "log.text":
        text = str(evaluate_value(command.args["text"], frame.variables))
        return {"text": text}, {"text": text}

    if command.type == "log.value":
        name = str(command.args["name"])
        value = evaluate_value(command.args["value"], frame.variables)
        return {"name": name, "value": value}, {"name": name, "value": value}

    if command.type == "delay":
        ms = int(evaluate_value(command.args["ms"], frame.variables))
        await _sleep_with_control_async(context, ms / 1000)
        return {"ms": ms}, {"slept_ms": ms}

    if command.type == "call":
        return await _execute_call_async(context, testcase_name, phase, command, frame, events)

    if command.type == "for":
        return await _execute_for_async(context, testcase_name, phase, command, frame, events)

    spec = COMMAND_SPECS.get(command.type)
    if spec is not None and spec.category == "adapter":
        return await _execute_adapter_command_async(context, testcase_name, phase, command, frame)
    if spec is not None and spec.category == "device":
        return await _execute_device_command_async(context, testcase_name, phase, command, frame)

    raise CommandFailed(f"Unsupported command '{command.type}'.")


async def _execute_adapter_command_async(
    context: RuntimeContext,
    testcase_name: str,
    phase: str,
    command: NormalizedCommand,
    frame: Frame,
) -> tuple[Dict[str, Any], Dict[str, Any]]:
    spec = COMMAND_SPECS[command.type]
    if spec.adapter is None:
        raise CommandFailed(f"Command '{command.type}' does not declare an adapter.")

    command_args = _with_command_defaults(context.package, command)
    resolved_inputs = evaluate_value(command_args, frame.variables)
    adapter = context.adapter_registry.get(spec.adapter)
    adapter_context = AdapterContext(
        run_id=context.run_id,
        testcase=testcase_name,
        phase=phase,
    )
    adapter_result = await _execute_adapter_async(
        adapter,
        command.type,
        resolved_inputs,
        adapter_context,
    )
    if not adapter_result.success:
        raise CommandFailed(adapter_result.message or f"Adapter '{spec.adapter}' failed.")
    if "save_as" in command_args:
        frame.variables[str(command_args["save_as"])] = _adapter_save_value(adapter_result.values)
    return resolved_inputs, adapter_result.to_outputs()


async def _execute_adapter_async(
    adapter: Any,
    command_type: str,
    args: Dict[str, Any],
    context: AdapterContext,
) -> AdapterResult:
    execute_async = getattr(adapter, "execute_async", None)
    if callable(execute_async):
        return await _maybe_await(execute_async(command_type, args, context))
    return await asyncio.to_thread(adapter.execute, command_type, args, context)


def _adapter_save_value(values: Dict[str, Any]) -> Any:
    if "text" in values:
        return values["text"]
    if "value" in values:
        return values["value"]
    return dict(values)


def _with_command_defaults(package: ResolvedPackage, command: NormalizedCommand) -> Dict[str, Any]:
    command_defaults = package.tool_profile_snapshot.get("command_defaults", {})
    if not isinstance(command_defaults, Mapping):
        return dict(command.args)
    defaults = command_defaults.get(command.type)
    if not isinstance(defaults, Mapping):
        return dict(command.args)
    merged = dict(defaults)
    merged.update(command.args)
    return merged


async def _execute_device_command_async(
    context: RuntimeContext,
    testcase_name: str,
    phase: str,
    command: NormalizedCommand,
    frame: Frame,
) -> tuple[Dict[str, Any], Dict[str, Any]]:
    resolved_inputs = evaluate_value(command.args, frame.variables)
    execution = await asyncio.to_thread(
        execute_device_command,
        command.type,
        resolved_inputs,
        context.package.tool_profile_snapshot,
        context.adapter_registry,
        AdapterContext(
            run_id=context.run_id,
            testcase=testcase_name,
            phase=phase,
        ),
    )
    if "save_as" in command.args and execution.save_value is not None:
        frame.variables[str(command.args["save_as"])] = execution.save_value
    return execution.resolved_inputs, execution.outputs


async def _execute_call_async(
    context: RuntimeContext,
    testcase_name: str,
    phase: str,
    command: NormalizedCommand,
    frame: Frame,
    events: List[CommandEvent],
) -> tuple[Dict[str, Any], Dict[str, Any]]:
    function_name = str(command.args["function"])
    function_def = context.package.functions[function_name]
    call_args = evaluate_value(command.args.get("args", {}), frame.variables)
    if not isinstance(call_args, Mapping):
        raise CommandFailed("call.args must resolve to a mapping.")

    function_frame = Frame(
        variables={
            param: call_args[param]
            for param in function_def.params
            if param in call_args
        }
    )
    function_status, function_error = await _run_commands_async(
        context,
        testcase_name,
        f"function:{function_name}",
        function_def.steps,
        function_frame,
        events,
    )
    if function_status == "aborted":
        reason = context.run_control.stop_reason() if context.run_control is not None else None
        raise CommandAborted(reason or "stop_requested")
    if function_status == "failed":
        raise CommandFailed(function_error or f"Function '{function_name}' failed.")

    out_mapping = command.args.get("out", {}) or {}
    if not isinstance(out_mapping, Mapping):
        raise CommandFailed("call.out must be a mapping.")

    mapped_outputs: Dict[str, Any] = {}
    for return_name in function_def.returns:
        if return_name not in function_frame.variables:
            continue
        target_name = str(out_mapping.get(return_name, return_name))
        value = function_frame.variables[return_name]
        frame.variables[target_name] = value
        mapped_outputs[target_name] = value

    return {
        "function": function_name,
        "args": dict(call_args),
        "out": dict(out_mapping),
    }, {"returns": mapped_outputs}


async def _execute_for_async(
    context: RuntimeContext,
    testcase_name: str,
    phase: str,
    command: NormalizedCommand,
    frame: Frame,
    events: List[CommandEvent],
) -> tuple[Dict[str, Any], Dict[str, Any]]:
    resolved_loop_items = evaluate_value(command.args["each"], frame.variables)
    if not _is_loop_iterable(resolved_loop_items):
        raise CommandFailed("for.each must resolve to a list or tuple.")
    loop_items = list(resolved_loop_items)

    loop_variable = str(command.args["as"])
    nested_commands = command.args.get("do", [])
    if not isinstance(nested_commands, Sequence) or not all(
        isinstance(item, NormalizedCommand)
        for item in nested_commands
    ):
        raise CommandFailed("for.do must be a command list.")

    iteration_count = 0
    for item in loop_items:
        frame.variables[loop_variable] = item
        iteration_status, iteration_error = await _run_commands_async(
            context,
            testcase_name,
            phase,
            nested_commands,
            frame,
            events,
        )
        iteration_count += 1
        if iteration_status == "aborted":
            reason = context.run_control.stop_reason() if context.run_control is not None else None
            raise CommandAborted(reason or "stop_requested")
        if iteration_status == "failed":
            raise CommandFailed(iteration_error or "for loop body failed.")

    return {
        "each": list(loop_items),
        "as": loop_variable,
    }, {
        "iterations": iteration_count,
        "last_value": frame.variables.get(loop_variable),
    }


def _is_loop_iterable(value: Any) -> bool:
    return isinstance(value, Iterable) and not isinstance(value, (str, bytes, Mapping))


async def _sleep_with_control_async(context: RuntimeContext, seconds: float) -> None:
    if context.run_control is None:
        await _maybe_await(context.sleep_fn(seconds))
        return

    remaining = max(0.0, seconds)
    poll_interval_s = max(context.run_control.poll_interval_s, 0.001)
    while remaining > 0:
        reason = context.run_control.stop_reason()
        if reason is not None:
            raise CommandAborted(reason)

        if context.run_control.read_state() == "paused":
            await context.run_control.wait_until_resumed_async(context.sleep_fn)
            reason = context.run_control.stop_reason()
            if reason is not None:
                raise CommandAborted(reason)

        chunk = min(remaining, poll_interval_s)
        await _maybe_await(context.sleep_fn(chunk))
        remaining -= chunk

    reason = context.run_control.stop_reason()
    if reason is not None:
        raise CommandAborted(reason)


def _event(
    context: RuntimeContext,
    testcase_name: str,
    phase: str,
    command: NormalizedCommand,
    status: str,
    resolved_inputs: Dict[str, Any],
    outputs: Dict[str, Any],
    error: Optional[str] = None,
    local_variables: Optional[Dict[str, Any]] = None,
) -> CommandEvent:
    return CommandEvent(
        run_id=context.run_id,
        testcase=testcase_name,
        phase=phase,
        command_path=command.path,
        command_type=command.type,
        status=status,
        source_file=command.source_file,
        source_line=command.source_line,
        resolved_inputs=resolved_inputs,
        outputs=outputs,
        local_variables=local_variables or {},
        error=error,
    )
