# Embedded SW Tester Phase 2 Runtime Core Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Execute compiled YAML test packages with a minimal Python runtime that produces testcase results and structured command events.

**Architecture:** Add a focused `embsw_tester.runtime` package that consumes `ResolvedPackage` from the DSL compiler. The runtime owns command dispatch, expression evaluation, function call frames, local variables, assert failures, and JSON-serializable run results; the CLI gains a `run` command that compiles then executes a YAML file.

**Tech Stack:** Python 3.9+, PyYAML, pytest, standard-library dataclasses and argparse.

---

## File Structure

- `src/embsw_tester/runtime/__init__.py`: exports `run_package`.
- `src/embsw_tester/runtime/models.py`: runtime event, testcase result, and run result dataclasses.
- `src/embsw_tester/runtime/expressions.py`: restricted expression and string template evaluator.
- `src/embsw_tester/runtime/runner.py`: command dispatch, frame handling, failure policy, and result creation.
- `src/embsw_tester/cli.py`: add `run <yaml_file> --json`.
- `src/embsw_tester/dsl/catalog.py`: add runtime-supported `assert.gt`.
- `tests/test_runtime.py`: runtime behavior tests.
- `tests/test_cli.py`: CLI run behavior test.
- `README.md`: document runtime command.

### Task 1: Runtime Success Path

**Files:**
- Create: `tests/test_runtime.py`
- Create: `src/embsw_tester/runtime/models.py`
- Create: `src/embsw_tester/runtime/expressions.py`
- Create: `src/embsw_tester/runtime/runner.py`
- Create: `src/embsw_tester/runtime/__init__.py`

- [ ] **Step 1: Write failing test for function call scope and returns**

```python
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
```

- [ ] **Step 2: Run the test and verify it fails because runtime does not exist**

Run: `.venv/bin/python -m pytest tests/test_runtime.py -q`

Expected: FAIL with `ModuleNotFoundError: No module named 'embsw_tester.runtime'`.

- [ ] **Step 3: Implement minimal runtime success path**

Implement dataclasses, expression evaluation for `${name}`, `set`, `call`, and `assert.eq`.

- [ ] **Step 4: Verify success path passes**

Run: `.venv/bin/python -m pytest tests/test_runtime.py -q`

Expected: PASS for the success-path test.

### Task 2: Assert Failure Policy

**Files:**
- Modify: `tests/test_runtime.py`
- Modify: `src/embsw_tester/runtime/runner.py`

- [ ] **Step 1: Write failing test for assert failure stopping steps**

```python
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
```

- [ ] **Step 2: Run and verify the new test fails**

Run: `.venv/bin/python -m pytest tests/test_runtime.py -q`

Expected: FAIL until `assert.gt` and stop-on-failure are implemented.

- [ ] **Step 3: Implement `assert.gt` and failure stop policy**

Add `assert.gt` to the command catalog and runtime dispatcher.

- [ ] **Step 4: Verify runtime tests pass**

Run: `.venv/bin/python -m pytest tests/test_runtime.py -q`

Expected: runtime tests pass.

### Task 3: CLI Run Command

**Files:**
- Modify: `tests/test_cli.py`
- Modify: `src/embsw_tester/cli.py`
- Modify: `README.md`

- [ ] **Step 1: Write failing CLI test for `run`**

```python
def test_cli_run_outputs_run_result_json():
    # Invoke `python -m embsw_tester.cli run samples/boot-smoke.yaml --json`
    # and assert payload["status"] == "passed".
```

- [ ] **Step 2: Run and verify failure**

Run: `.venv/bin/python -m pytest tests/test_cli.py -q`

Expected: FAIL until `run` subcommand exists.

- [ ] **Step 3: Implement CLI run**

Compile file, run package, print `RunResult.to_dict()`, and return nonzero for failed/aborted runs.

- [ ] **Step 4: Update README**

Document `.venv/bin/embsw-tester run samples/boot-smoke.yaml --json`.

- [ ] **Step 5: Verify full suite**

Run: `.venv/bin/python -m pytest`

Expected: all tests pass.

## Self-Review

- The plan covers the Runtime Core slice named in README and the design document.
- Real Trace32, CANoe, INCA, Serial adapter execution remains deferred.
- The initial adapter command behavior is a mock event for adapter-category commands, enough to run existing samples without hardware.
