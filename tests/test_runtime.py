from pathlib import Path

from embsw_tester.adapters import AdapterContext, AdapterRegistry, AdapterResult
from embsw_tester.adapters.canoe import CanoeAdapter
from embsw_tester.adapters.inca import IncaAdapter
from embsw_tester.adapters.serial import FakeSerialPort, SerialAdapter
from embsw_tester.adapters.trace32 import FakeTrace32Transport, Trace32Adapter
from embsw_tester.dsl.compiler import compile_file
from embsw_tester.runtime import run_package


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
