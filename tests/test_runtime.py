from pathlib import Path

from embsw_tester.adapters import AdapterContext, AdapterRegistry, AdapterResult
from embsw_tester.adapters.serial import FakeSerialPort, SerialAdapter
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
