import asyncio
from pathlib import Path

from embsw_tester.adapters import AdapterContext, AdapterRegistry, AdapterResult
from embsw_tester.adapters.canoe import CanoeAdapter
from embsw_tester.adapters.inca import IncaAdapter
from embsw_tester.adapters.serial import FakeSerialPort, SerialAdapter
from embsw_tester.adapters.trace32 import FakeTrace32Transport, Trace32Adapter
from embsw_tester.dsl.compiler import compile_file
from embsw_tester.runtime import RuntimeControl, run_package, run_package_async


class RecordingAdapter:
    name = "serial"

    def __init__(self):
        self.calls = []

    def execute(self, command_type: str, args: dict, context: AdapterContext) -> AdapterResult:
        self.calls.append((command_type, args, context.testcase, context.phase))
        return AdapterResult(
            success=True,
            status="passed",
            message="recorded",
            values={"echo": args},
            raw_evidence_ref="raw-logs/serial.log",
            duration_ms=7,
        )


class AsyncRecordingAdapter:
    name = "serial"

    def __init__(self):
        self.calls = []

    async def execute_async(self, command_type: str, args: dict, context: AdapterContext) -> AdapterResult:
        await asyncio.sleep(0)
        self.calls.append((command_type, args, context.testcase, context.phase))
        return AdapterResult(
            success=True,
            status="passed",
            message="async recorded",
            values={"echo": args},
            raw_evidence_ref="raw-logs/serial-async.log",
            duration_ms=11,
        )


def test_runtime_async_delay_awaits_sleep_and_event_callbacks(tmp_path: Path):
    test_file = tmp_path / "async-delay.yaml"
    test_file.write_text(
        """
testcases:
  - name: async_delay_case
    steps:
      - delay:
          ms: 120
      - set:
          var: done
          value: true
""".strip(),
        encoding="utf-8",
    )
    observed = []
    sleep_calls = []

    async def sleep_fn(seconds: float) -> None:
        observed.append("sleep:start")
        sleep_calls.append(seconds)
        await asyncio.sleep(0)
        observed.append("sleep:end")

    async def event_callback(event):
        await asyncio.sleep(0)
        observed.append(f"{event.command_type}:{event.status}")

    result = asyncio.run(
        run_package_async(
            compile_file(test_file),
            run_id="async-delay-run",
            sleep_fn=sleep_fn,
            event_callback=event_callback,
        )
    )

    assert result.status == "passed"
    assert sleep_calls == [0.12]
    assert observed[:4] == [
        "delay:running",
        "sleep:start",
        "sleep:end",
        "delay:passed",
    ]
    assert observed[-2:] == ["set:running", "set:passed"]


def test_runtime_async_dispatches_adapter_execute_async(tmp_path: Path):
    test_file = tmp_path / "async-adapter.yaml"
    test_file.write_text(
        """
testcases:
  - name: async_adapter_case
    steps:
      - serial.write:
          port: psu
          text: "OUT 1 ON"
""".strip(),
        encoding="utf-8",
    )
    registry = AdapterRegistry()
    adapter = AsyncRecordingAdapter()
    registry.register("serial", adapter)

    result = asyncio.run(
        run_package_async(
            compile_file(test_file),
            run_id="async-adapter-run",
            adapter_registry=registry,
        )
    )

    event = result.testcase_results[0].events[-1]
    assert result.status == "passed"
    assert adapter.calls == [
        ("serial.write", {"port": "psu", "text": "OUT 1 ON"}, "async_adapter_case", "steps")
    ]
    assert event.outputs["message"] == "async recorded"
    assert event.outputs["raw_evidence_ref"] == "raw-logs/serial-async.log"


def test_runtime_runs_function_call_and_maps_returns(tmp_path: Path):
    test_file = tmp_path / "runtime.yaml"
    test_file.write_text(
        """
functions:
  make_ready:
    params: [input_flag]
    returns: [ready]
    steps:
      - set:
          var: ready
          value: "${input_flag}"
testcases:
  - name: ready_case
    steps:
      - call:
          function: make_ready
          args:
            input_flag: true
          out:
            ready: power_ready
      - assert.eq:
          left: "${power_ready}"
          right: true
""".strip(),
        encoding="utf-8",
    )

    result = run_package(compile_file(test_file), run_id="run-1")

    assert result.status == "passed"
    assert result.testcase_results[0].variables == {"power_ready": True}
    assert result.testcase_results[0].status == "passed"


def test_runtime_records_local_variable_snapshot_per_event(tmp_path: Path):
    test_file = tmp_path / "variable-snapshot.yaml"
    test_file.write_text(
        """
testcases:
  - name: variable_snapshot_case
    steps:
      - set:
          var: rpm
          value: 700
      - set:
          var: power_ready
          value: "${rpm > 0}"
      - log.value:
          name: rpm
          value: "${rpm}"
""".strip(),
        encoding="utf-8",
    )

    result = run_package(compile_file(test_file), run_id="run-variable-snapshot")

    testcase = result.testcase_results[0]
    assert testcase.events[0].local_variables == {"rpm": 700}
    assert testcase.events[1].local_variables == {"rpm": 700, "power_ready": True}
    assert testcase.events[2].local_variables == {"rpm": 700, "power_ready": True}
    assert testcase.events[1].to_dict()["local_variables"] == {
        "rpm": 700,
        "power_ready": True,
    }


def test_runtime_records_source_line_per_event(tmp_path: Path):
    test_file = tmp_path / "source-line.yaml"
    test_file.write_text(
        """
testcases:
  - name: source_line_case
    steps:
      - set:
          var: rpm
          value: 700
      - assert.gt:
          left: "${rpm}"
          right: 0
""".strip(),
        encoding="utf-8",
    )

    result = run_package(compile_file(test_file), run_id="run-source-line")

    testcase = result.testcase_results[0]
    assert testcase.events[0].source_file == str(test_file.resolve())
    assert testcase.events[0].source_line == 4
    assert testcase.events[1].source_line == 7
    assert testcase.events[1].to_dict()["source_line"] == 7


def test_runtime_pauses_on_breakpoint_and_resumes_from_control_file(tmp_path: Path):
    test_file = tmp_path / "breakpoint.yaml"
    test_file.write_text(
        """
testcases:
  - name: breakpoint_case
    steps:
      - set:
          var: rpm
          value: 700
      - assert.eq:
          left: "${rpm}"
          right: 700
""".strip(),
        encoding="utf-8",
    )
    control_file = tmp_path / "control.json"
    control_file.write_text('{"state": "running"}', encoding="utf-8")
    streamed_events = []

    def resume_on_pause(event):
        streamed_events.append(event)
        if event.status == "paused":
            control_file.write_text('{"state": "running"}', encoding="utf-8")

    result = run_package(
        compile_file(test_file),
        run_id="breakpoint-run",
        event_callback=resume_on_pause,
        run_control=RuntimeControl(
            control_file=control_file,
            breakpoint_lines={4},
            poll_interval_s=0,
        ),
    )

    testcase = result.testcase_results[0]
    paused_event = next(event for event in streamed_events if event.status == "paused")
    assert result.status == "passed"
    assert paused_event.command_type == "set"
    assert paused_event.source_line == 4
    assert paused_event.outputs["reason"] == "breakpoint"
    assert testcase.events[0].status == "paused"
    assert testcase.events[1].status == "passed"


def test_runtime_uses_latest_breakpoint_lines_from_control_file(tmp_path: Path):
    test_file = tmp_path / "breakpoint-removed.yaml"
    test_file.write_text(
        """
testcases:
  - name: breakpoint_removed_case
    steps:
      - set:
          var: rpm
          value: 700
""".strip(),
        encoding="utf-8",
    )
    control_file = tmp_path / "control.json"
    control_file.write_text(
        '{"state": "running", "breakpoint_lines": []}',
        encoding="utf-8",
    )
    streamed_events = []

    result = run_package(
        compile_file(test_file),
        run_id="breakpoint-removed-run",
        event_callback=streamed_events.append,
        run_control=RuntimeControl(
            control_file=control_file,
            breakpoint_lines={4},
            poll_interval_s=0,
        ),
    )

    assert result.status == "passed"
    assert all(event.status != "paused" for event in streamed_events)


def test_runtime_honors_manual_pause_request_before_next_command(tmp_path: Path):
    test_file = tmp_path / "manual-pause.yaml"
    test_file.write_text(
        """
testcases:
  - name: manual_pause_case
    steps:
      - set:
          var: rpm
          value: 700
""".strip(),
        encoding="utf-8",
    )
    control_file = tmp_path / "control.json"
    control_file.write_text('{"state": "paused"}', encoding="utf-8")
    streamed_events = []

    def resume_on_pause(event):
        streamed_events.append(event)
        if event.status == "paused":
            control_file.write_text('{"state": "running"}', encoding="utf-8")

    result = run_package(
        compile_file(test_file),
        run_id="manual-pause-run",
        event_callback=resume_on_pause,
        run_control=RuntimeControl(
            control_file=control_file,
            poll_interval_s=0,
        ),
    )

    paused_event = next(event for event in streamed_events if event.status == "paused")
    assert result.status == "passed"
    assert paused_event.command_type == "set"
    assert paused_event.outputs["reason"] == "pause_requested"


def test_runtime_stops_from_control_file_before_next_command(tmp_path: Path):
    test_file = tmp_path / "stop-before-next-command.yaml"
    test_file.write_text(
        """
testcases:
  - name: stop_case
    steps:
      - set:
          var: first
          value: true
      - set:
          var: second
          value: true
""".strip(),
        encoding="utf-8",
    )
    control_file = tmp_path / "control.json"
    control_file.write_text('{"state": "running", "breakpoint_lines": []}', encoding="utf-8")
    streamed_events = []

    def request_stop_after_first_command(event):
        streamed_events.append(event)
        if (
            event.command_type == "set"
            and event.status == "passed"
            and event.outputs.get("var") == "first"
        ):
            control_file.write_text(
                '{"state": "stopping", "reason": "user_stop", "breakpoint_lines": []}',
                encoding="utf-8",
            )

    result = run_package(
        compile_file(test_file),
        run_id="stop-before-next-command-run",
        event_callback=request_stop_after_first_command,
        run_control=RuntimeControl(control_file=control_file, poll_interval_s=0),
    )

    testcase = result.testcase_results[0]
    aborted_event = next(event for event in streamed_events if event.status == "aborted")
    assert result.status == "aborted"
    assert testcase.status == "aborted"
    assert testcase.variables == {"first": True}
    assert aborted_event.command_type == "set"
    assert aborted_event.source_line == 7
    assert aborted_event.outputs["reason"] == "user_stop"
    assert all(
        not (
            event.status == "passed"
            and event.outputs.get("var") == "second"
        )
        for event in streamed_events
    )


def test_runtime_stop_interrupts_long_delay(tmp_path: Path):
    test_file = tmp_path / "stop-delay.yaml"
    test_file.write_text(
        """
testcases:
  - name: stop_delay_case
    steps:
      - delay:
          ms: 500
      - set:
          var: after_delay
          value: true
""".strip(),
        encoding="utf-8",
    )
    control_file = tmp_path / "control.json"
    control_file.write_text('{"state": "running", "breakpoint_lines": []}', encoding="utf-8")
    streamed_events = []
    sleep_calls = []

    async def sleep_fn(seconds: float) -> None:
        sleep_calls.append(seconds)
        if len(sleep_calls) == 1:
            control_file.write_text(
                '{"state": "stopping", "breakpoint_lines": []}',
                encoding="utf-8",
            )
        await asyncio.sleep(0)

    result = asyncio.run(
        run_package_async(
            compile_file(test_file),
            run_id="stop-delay-run",
            sleep_fn=sleep_fn,
            event_callback=streamed_events.append,
            run_control=RuntimeControl(control_file=control_file, poll_interval_s=0.05),
        )
    )

    testcase = result.testcase_results[0]
    delay_aborted_event = next(
        event
        for event in streamed_events
        if event.command_type == "delay" and event.status == "aborted"
    )
    assert result.status == "aborted"
    assert testcase.status == "aborted"
    assert testcase.variables == {}
    assert sleep_calls == [0.05]
    assert delay_aborted_event.outputs["reason"] == "stop_requested"
    assert all(event.command_type != "set" for event in streamed_events)


def test_runtime_runs_for_loop_and_nested_call_with_shared_frame(tmp_path: Path):
    test_file = tmp_path / "for-loop.yaml"
    test_file.write_text(
        """
functions:
  add_channel:
    params: [running_total, channel]
    returns: [next_total]
    steps:
      - set:
          var: next_total
          value: "${running_total + channel}"
testcases:
  - name: for_loop_case
    steps:
      - set:
          var: channel_sum
          value: 0
      - set:
          var: channels
          value: [1, 2, 3]
      - for:
          each: "${channels}"
          as: channel
          do:
            - call:
                function: add_channel
                args:
                  running_total: "${channel_sum}"
                  channel: "${channel}"
                out:
                  next_total: channel_sum
            - log.value:
                name: channel_sum
                value: "${channel_sum}"
      - assert.eq:
          left: "${channel_sum}"
          right: 6
""".strip(),
        encoding="utf-8",
    )

    result = run_package(compile_file(test_file), run_id="run-for-loop")

    testcase = result.testcase_results[0]
    assert result.status == "passed"
    assert testcase.variables["channel_sum"] == 6
    assert testcase.variables["channel"] == 3
    assert [event.command_type for event in testcase.events].count("call") == 3
    assert testcase.events[-1].command_type == "assert.eq"
    assert testcase.events[-1].local_variables["channel_sum"] == 6


def test_runtime_marks_assert_failure_and_stops_remaining_steps(tmp_path: Path):
    test_file = tmp_path / "failure.yaml"
    test_file.write_text(
        """
testcases:
  - name: failure_case
    steps:
      - set:
          var: rpm
          value: 0
      - assert.gt:
          left: "${rpm}"
          right: 0
      - set:
          var: should_not_exist
          value: true
""".strip(),
        encoding="utf-8",
    )

    result = run_package(compile_file(test_file), run_id="run-2")

    testcase = result.testcase_results[0]
    assert result.status == "failed"
    assert testcase.status == "failed"
    assert testcase.variables == {"rpm": 0}
    assert testcase.events[-1].command_type == "assert.gt"
    assert testcase.events[-1].status == "failed"


def test_runtime_dispatches_adapter_command_through_registry(tmp_path: Path):
    test_file = tmp_path / "adapter.yaml"
    test_file.write_text(
        """
testcases:
  - name: adapter_case
    steps:
      - set:
          var: command_text
          value: "OUT 1 ON"
      - serial.write:
          port: psu
          text: "{{ command_text }}"
""".strip(),
        encoding="utf-8",
    )
    registry = AdapterRegistry()
    adapter = RecordingAdapter()
    registry.register("serial", adapter)

    result = run_package(
        compile_file(test_file),
        run_id="run-adapter",
        adapter_registry=registry,
    )

    event = result.testcase_results[0].events[-1]
    assert result.status == "passed"
    assert adapter.calls == [
        ("serial.write", {"port": "psu", "text": "OUT 1 ON"}, "adapter_case", "steps")
    ]
    assert event.outputs["values"] == {"echo": {"port": "psu", "text": "OUT 1 ON"}}
    assert event.outputs["raw_evidence_ref"] == "raw-logs/serial.log"
    assert event.outputs["duration_ms"] == 7


def test_runtime_serial_read_saves_response_into_variable(tmp_path: Path):
    test_file = tmp_path / "serial-read.yaml"
    test_file.write_text(
        """
testcases:
  - name: serial_read_case
    steps:
      - serial.read:
          port: psu
          timeout_ms: 50
          save_as: psu_response
      - assert.eq:
          left: "${psu_response}"
          right: "OK"
""".strip(),
        encoding="utf-8",
    )
    registry = AdapterRegistry()
    registry.register(
        "serial",
        SerialAdapter(
            {"psu": FakeSerialPort(rx_lines=["OK"])},
            evidence_root=tmp_path / "reports" / "serial-read",
        ),
    )

    result = run_package(
        compile_file(test_file),
        run_id="serial-read",
        adapter_registry=registry,
    )

    testcase = result.testcase_results[0]
    assert result.status == "passed"
    assert testcase.variables["psu_response"] == "OK"
    assert testcase.events[0].command_type == "serial.read"
    assert testcase.events[0].outputs["values"]["text"] == "OK"


def test_runtime_runs_canoe_commands_through_adapter(tmp_path: Path):
    test_file = tmp_path / "canoe.yaml"
    test_file.write_text(
        """
testcases:
  - name: canoe_case
    steps:
      - canoe.measurement.start: {}
      - canoe.sysvar.set:
          namespace: Vehicle
          name: Ignition
          value: true
      - canoe.sysvar.read:
          namespace: Vehicle
          name: Ignition
          save_as: ignition_state
      - canoe.signal.read:
          signal: EngineSpeed
          save_as: rpm
      - assert.eq:
          left: "${ignition_state}"
          right: true
      - assert.gt:
          left: "${rpm}"
          right: 0
      - canoe.measurement.stop: {}
""".strip(),
        encoding="utf-8",
    )
    registry = AdapterRegistry()
    registry.register("canoe", CanoeAdapter(signals={"EngineSpeed": 850}))

    result = run_package(
        compile_file(test_file),
        run_id="canoe-runtime",
        adapter_registry=registry,
    )

    testcase = result.testcase_results[0]
    assert result.status == "passed"
    assert testcase.variables["ignition_state"] is True
    assert testcase.variables["rpm"] == 850
    assert testcase.events[0].command_type == "canoe.measurement.start"
    assert testcase.events[-1].outputs["values"]["measurement_running"] is False


def test_runtime_runs_inca_commands_through_adapter(tmp_path: Path):
    test_file = tmp_path / "inca.yaml"
    test_file.write_text(
        """
testcases:
  - name: inca_case
    steps:
      - inca.recording.start:
          name: boot
      - inca.measure.read:
          variable: EngineSpeed
          save_as: rpm
      - inca.calibration.set:
          parameter: IdleSpeedTarget
          value: 850
      - assert.gt:
          left: "${rpm}"
          right: 0
      - inca.recording.stop: {}
""".strip(),
        encoding="utf-8",
    )
    registry = AdapterRegistry()
    registry.register("inca", IncaAdapter(measurements={"EngineSpeed": 900}))

    result = run_package(
        compile_file(test_file),
        run_id="inca-runtime",
        adapter_registry=registry,
    )

    testcase = result.testcase_results[0]
    assert result.status == "passed"
    assert testcase.variables["rpm"] == 900
    assert testcase.events[0].command_type == "inca.recording.start"
    assert testcase.events[-1].outputs["values"]["recording_active"] is False


def test_runtime_runs_trace32_command_with_udp_fallback(tmp_path: Path):
    test_file = tmp_path / "trace32.yaml"
    test_file.write_text(
        """
testcases:
  - name: trace32_case
    steps:
      - trace32.command:
          command: "STATE()"
          save_as: trace32_response
      - assert.eq:
          left: "${trace32_response}"
          right: "STATE:HALTED"
""".strip(),
        encoding="utf-8",
    )
    rcl = FakeTrace32Transport(
        name="rcl",
        result=AdapterResult(success=False, status="failed", message="rcl unavailable"),
    )
    udp = FakeTrace32Transport(
        name="udp",
        result=AdapterResult(
            success=True,
            status="passed",
            message="udp ok",
            values={"value": "STATE:HALTED"},
        ),
    )
    registry = AdapterRegistry()
    registry.register("trace32", Trace32Adapter(rcl_transport=rcl, udp_transport=udp))

    result = run_package(
        compile_file(test_file),
        run_id="trace32-runtime",
        adapter_registry=registry,
    )

    testcase = result.testcase_results[0]
    event = testcase.events[0]
    assert result.status == "passed"
    assert testcase.variables["trace32_response"] == "STATE:HALTED"
    assert event.outputs["values"]["transport"] == "udp"
    assert event.outputs["values"]["fallback_used"] is True
