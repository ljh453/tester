from __future__ import annotations

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


@dataclass
class Frame:
    variables: Dict[str, Any] = field(default_factory=dict)


@dataclass
class RuntimeContext:
    run_id: str
    package: ResolvedPackage
    sleep_fn: Callable[[float], None]
    adapter_registry: AdapterRegistry
    event_callback: Optional[Callable[[CommandEvent], None]] = None
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
        if command.source_line in self.active_breakpoint_lines(payload):
            self.write_state("paused", reason="breakpoint")
            return "breakpoint"
        if self.read_state(payload) == "paused":
            return "pause_requested"
        return None

    def wait_until_resumed(self, sleep_fn: Callable[[float], None]) -> None:
        if self.control_file is None:
            return
        while self.read_state() == "paused":
            sleep_fn(self.poll_interval_s)

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
    testcase_results = [
        _run_testcase(context, testcase)
        for testcase in package.testcases
    ]
    run_status = "failed" if any(result.status == "failed" for result in testcase_results) else "passed"
    return RunResult(
        run_id=resolved_run_id,
        status=run_status,
        testcase_results=testcase_results,
    )


def _run_testcase(context: RuntimeContext, testcase: TestcaseDef) -> TestcaseResult:
    frame = Frame()
    events: List[CommandEvent] = []
    status = "passed"
    error: Optional[str] = None

    for phase, commands in (
        ("preconditions", testcase.preconditions),
        ("steps", testcase.steps),
        ("postconditions", testcase.postconditions),
    ):
        phase_status, phase_error = _run_commands(context, testcase.name, phase, commands, frame, events)
        if phase_status == "failed":
            status = "failed"
            error = phase_error
            break

    if testcase.cleanup:
        cleanup_status, cleanup_error = _run_commands(
            context,
            testcase.name,
            "cleanup",
            testcase.cleanup,
            frame,
            events,
        )
        if cleanup_status == "failed" and status == "passed":
            status = "failed"
            error = cleanup_error

    return TestcaseResult(
        name=testcase.name,
        status=status,
        variables=dict(frame.variables),
        events=events,
        error=error,
    )


def _run_commands(
    context: RuntimeContext,
    testcase_name: str,
    phase: str,
    commands: Sequence[NormalizedCommand],
    frame: Frame,
    events: List[CommandEvent],
) -> tuple[str, Optional[str]]:
    for command in commands:
        pause_event = _pause_event_if_needed(context, testcase_name, phase, command, frame)
        if pause_event is not None:
            events.append(pause_event)
            if context.event_callback is not None:
                context.event_callback(pause_event)
            context.run_control.wait_until_resumed(context.sleep_fn)
        if context.event_callback is not None:
            context.event_callback(_running_event(context, testcase_name, phase, command, frame))
        event = _execute_command(context, testcase_name, phase, command, frame, events)
        events.append(event)
        if context.event_callback is not None:
            context.event_callback(event)
        if event.status == "failed":
            return "failed", event.error
    return "passed", None


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


def _execute_command(
    context: RuntimeContext,
    testcase_name: str,
    phase: str,
    command: NormalizedCommand,
    frame: Frame,
    events: List[CommandEvent],
) -> CommandEvent:
    try:
        resolved_inputs, outputs = _dispatch_command(context, testcase_name, phase, command, frame, events)
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


def _dispatch_command(
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
        _sleep_delay(context, testcase_name, phase, command, frame, events, ms)
        return {"ms": ms}, {"slept_ms": ms}

    if command.type == "call":
        return _execute_call(context, testcase_name, phase, command, frame, events)

    if command.type == "for":
        return _execute_for(context, testcase_name, phase, command, frame, events)

    spec = COMMAND_SPECS.get(command.type)
    if spec is not None and spec.category == "adapter":
        return _execute_adapter_command(context, testcase_name, phase, command, frame)
    if spec is not None and spec.category == "device":
        return _execute_device_command(context, testcase_name, phase, command, frame)

    raise CommandFailed(f"Unsupported command '{command.type}'.")


def _sleep_delay(
    context: RuntimeContext,
    testcase_name: str,
    phase: str,
    command: NormalizedCommand,
    frame: Frame,
    events: List[CommandEvent],
    ms: int,
) -> None:
    remaining_s = max(0.0, ms / 1000)
    poll_interval_s = _delay_poll_interval_s(context, remaining_s)
    while remaining_s > 0:
        current_sleep_s = min(poll_interval_s, remaining_s)
        context.sleep_fn(current_sleep_s)
        remaining_s = max(0.0, remaining_s - current_sleep_s)
        if remaining_s > 0:
            _pause_during_delay_if_requested(context, testcase_name, phase, command, frame, events)


def _delay_poll_interval_s(context: RuntimeContext, remaining_s: float) -> float:
    if context.run_control is None or context.run_control.poll_interval_s <= 0:
        return remaining_s
    return min(context.run_control.poll_interval_s, remaining_s)


def _pause_during_delay_if_requested(
    context: RuntimeContext,
    testcase_name: str,
    phase: str,
    command: NormalizedCommand,
    frame: Frame,
    events: List[CommandEvent],
) -> None:
    if context.run_control is None or context.run_control.read_state() != "paused":
        return
    pause_event = _event(
        context,
        testcase_name,
        phase,
        command,
        "paused",
        {},
        {"reason": "pause_requested"},
        local_variables=deepcopy(frame.variables),
    )
    events.append(pause_event)
    if context.event_callback is not None:
        context.event_callback(pause_event)
    context.run_control.wait_until_resumed(context.sleep_fn)


def _execute_adapter_command(
    context: RuntimeContext,
    testcase_name: str,
    phase: str,
    command: NormalizedCommand,
    frame: Frame,
) -> tuple[Dict[str, Any], Dict[str, Any]]:
    spec = COMMAND_SPECS[command.type]
    if spec.adapter is None:
        raise CommandFailed(f"Command '{command.type}' does not declare an adapter.")

    resolved_inputs = evaluate_value(command.args, frame.variables)
    adapter = context.adapter_registry.get(spec.adapter)
    adapter_result = adapter.execute(
        command.type,
        resolved_inputs,
        AdapterContext(
            run_id=context.run_id,
            testcase=testcase_name,
            phase=phase,
        ),
    )
    if not adapter_result.success:
        raise CommandFailed(adapter_result.message or f"Adapter '{spec.adapter}' failed.")
    if "save_as" in command.args:
        frame.variables[str(command.args["save_as"])] = _adapter_save_value(adapter_result.values)
    return resolved_inputs, adapter_result.to_outputs()


def _adapter_save_value(values: Dict[str, Any]) -> Any:
    if "text" in values:
        return values["text"]
    if "value" in values:
        return values["value"]
    return dict(values)


def _execute_device_command(
    context: RuntimeContext,
    testcase_name: str,
    phase: str,
    command: NormalizedCommand,
    frame: Frame,
) -> tuple[Dict[str, Any], Dict[str, Any]]:
    resolved_inputs = evaluate_value(command.args, frame.variables)
    execution = execute_device_command(
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


def _execute_call(
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
    function_status, function_error = _run_commands(
        context,
        testcase_name,
        f"function:{function_name}",
        function_def.steps,
        function_frame,
        events,
    )
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


def _execute_for(
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
        iteration_status, iteration_error = _run_commands(
            context,
            testcase_name,
            phase,
            nested_commands,
            frame,
            events,
        )
        iteration_count += 1
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
