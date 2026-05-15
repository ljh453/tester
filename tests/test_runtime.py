from pathlib import Path

from embsw_tester.dsl.compiler import compile_file
from embsw_tester.runtime import run_package


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
