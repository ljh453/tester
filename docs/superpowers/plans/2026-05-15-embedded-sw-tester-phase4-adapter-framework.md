# Embedded SW Tester Phase 4 Adapter Framework Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace inline adapter mock handling with a common adapter contract and registry so external tool integrations can be added behind stable runtime boundaries.

**Architecture:** Extend command catalog entries with an `adapter` field. Add `embsw_tester.adapters` with `Adapter`, `AdapterContext`, `AdapterResult`, `AdapterRegistry`, and a default `MockAdapter`. Runtime receives an adapter registry, resolves adapter-category commands through catalog metadata, and converts adapter results into command event outputs.

**Tech Stack:** Python 3.9+, pytest, standard-library dataclasses.

---

## File Structure

- `src/embsw_tester/adapters/__init__.py`: exports adapter framework APIs.
- `src/embsw_tester/adapters/base.py`: `Adapter`, `AdapterContext`, `AdapterResult` contract.
- `src/embsw_tester/adapters/registry.py`: adapter lookup and default registry factory.
- `src/embsw_tester/adapters/mock.py`: deterministic mock adapter for hardware-free execution.
- `src/embsw_tester/dsl/catalog.py`: add `adapter` metadata to adapter command specs.
- `src/embsw_tester/runtime/runner.py`: route adapter commands through registry.
- `tests/test_adapters.py`: adapter framework behavior tests.
- `tests/test_runtime.py`: runtime adapter injection tests.
- `README.md`: update current phase and next work.

### Task 1: Adapter Framework Contract

**Files:**
- Create: `tests/test_adapters.py`
- Create: `src/embsw_tester/adapters/__init__.py`
- Create: `src/embsw_tester/adapters/base.py`
- Create: `src/embsw_tester/adapters/registry.py`
- Create: `src/embsw_tester/adapters/mock.py`

- [ ] **Step 1: Write failing tests for registry and mock adapter**

```python
from embsw_tester.adapters import AdapterContext, AdapterRegistry, MockAdapter


def test_mock_adapter_returns_structured_result():
    adapter = MockAdapter("serial")
    result = adapter.execute(
        "serial.write",
        {"port": "psu", "text": "OUT 1 ON"},
        AdapterContext(run_id="run-adapter", testcase="case", phase="steps"),
    )

    assert result.success is True
    assert result.status == "passed"
    assert result.values["mode"] == "mock"
    assert result.values["command_type"] == "serial.write"


def test_adapter_registry_resolves_registered_adapter():
    registry = AdapterRegistry()
    adapter = MockAdapter("serial")
    registry.register("serial", adapter)

    assert registry.get("serial") is adapter
```

- [ ] **Step 2: Run and verify failure**

Run: `.venv/bin/python -m pytest tests/test_adapters.py -q`

Expected: FAIL with `ModuleNotFoundError: No module named 'embsw_tester.adapters'`.

- [ ] **Step 3: Implement framework files**

Add the dataclasses, protocol-style base class, registry, and mock adapter.

- [ ] **Step 4: Verify adapter tests pass**

Run: `.venv/bin/python -m pytest tests/test_adapters.py -q`

Expected: adapter tests pass.

### Task 2: Runtime Adapter Dispatch

**Files:**
- Modify: `src/embsw_tester/dsl/catalog.py`
- Modify: `src/embsw_tester/runtime/runner.py`
- Modify: `tests/test_runtime.py`

- [ ] **Step 1: Write failing runtime test for injected adapter**

```python
class RecordingAdapter:
    def __init__(self):
        self.calls = []

    def execute(self, command_type, args, context):
        self.calls.append((command_type, args, context.testcase))
        return AdapterResult(success=True, status="passed", values={"echo": args})


def test_runtime_dispatches_adapter_command_through_registry(tmp_path):
    # Build YAML with serial.write, register RecordingAdapter("serial"),
    # run package, and assert event outputs include {"echo": ...}.
```

- [ ] **Step 2: Run and verify failure**

Run: `.venv/bin/python -m pytest tests/test_runtime.py -q`

Expected: FAIL until runtime accepts and uses `AdapterRegistry`.

- [ ] **Step 3: Implement runtime dispatch**

Add `adapter_registry` parameter to `run_package`, create default registry when absent, and route adapter commands through `COMMAND_SPECS[command.type].adapter`.

- [ ] **Step 4: Verify runtime tests pass**

Run: `.venv/bin/python -m pytest tests/test_runtime.py -q`

Expected: runtime tests pass.

### Task 3: Documentation And Full Verification

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Update README**

Mention Phase 4 Adapter Framework, mock adapter execution, and the next Serial real adapter step.

- [ ] **Step 2: Run full verification**

Run:

```bash
.venv/bin/python -m pytest
.venv/bin/embsw-tester run samples/boot-smoke.yaml --run-id adapter-smoke --reports-root reports --json
```

Expected: all tests pass and sample still runs with default mock adapter.

## Self-Review

- This plan introduces adapter boundaries only; it does not implement pyserial or real hardware I/O.
- Adapter output remains JSON-serializable so reports and IDE events can consume it without special cases.
- Existing CLI behavior should stay compatible because default registry provides mock adapters.
