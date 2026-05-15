# Embedded SW Tester Phase 5 Serial Adapter Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a testable Serial adapter that supports `serial.write` and `serial.read`, captures raw TX/RX evidence, and can be swapped from fake ports to real pyserial-backed ports later.

**Architecture:** Introduce `embsw_tester.adapters.serial` with a small `SerialPort` protocol, `FakeSerialPort` test double, and `SerialAdapter`. The runtime continues to use `AdapterRegistry`; adapter results remain JSON-serializable. `serial.read` can write its response into the current frame through `save_as`.

**Tech Stack:** Python 3.9+, pytest, standard-library dataclasses and pathlib.

---

## File Structure

- `src/embsw_tester/adapters/serial.py`: serial port protocol, fake port, adapter implementation.
- `src/embsw_tester/adapters/__init__.py`: export serial adapter classes.
- `src/embsw_tester/dsl/catalog.py`: add `serial.read` and optional args for serial commands.
- `src/embsw_tester/runtime/runner.py`: map adapter `save_as` values to frame variables.
- `tests/test_serial_adapter.py`: serial adapter unit tests.
- `tests/test_runtime.py`: runtime serial read `save_as` test.
- `README.md`: document serial adapter behavior and current fake/real boundary.

### Task 1: Serial Adapter Unit Behavior

**Files:**
- Create: `tests/test_serial_adapter.py`
- Create: `src/embsw_tester/adapters/serial.py`
- Modify: `src/embsw_tester/adapters/__init__.py`

- [ ] **Step 1: Write failing tests for write/read and raw evidence**

```python
from embsw_tester.adapters import AdapterContext
from embsw_tester.adapters.serial import FakeSerialPort, SerialAdapter


def test_serial_write_records_tx_and_raw_evidence(tmp_path):
    port = FakeSerialPort(rx_lines=[])
    adapter = SerialAdapter({"psu": port}, evidence_root=tmp_path)

    result = adapter.execute(
        "serial.write",
        {"port": "psu", "text": "OUT 1 ON"},
        AdapterContext(run_id="serial-run", testcase="case", phase="steps"),
    )

    assert result.success is True
    assert port.tx_lines == ["OUT 1 ON"]
    assert result.values["tx"] == "OUT 1 ON"
    assert (tmp_path / result.raw_evidence_ref).read_text(encoding="utf-8") == "TX OUT 1 ON\n"
```

- [ ] **Step 2: Run and verify failure**

Run: `.venv/bin/python -m pytest tests/test_serial_adapter.py -q`

Expected: FAIL with `ModuleNotFoundError` for serial adapter.

- [ ] **Step 3: Implement SerialAdapter and FakeSerialPort**

Support `serial.write`, `serial.read`, `timeout_ms`, and evidence files under `raw-logs/serial/<run-id>/<testcase>.log`.

- [ ] **Step 4: Verify serial adapter tests pass**

Run: `.venv/bin/python -m pytest tests/test_serial_adapter.py -q`

Expected: serial adapter tests pass.

### Task 2: Runtime Save-As Integration

**Files:**
- Modify: `tests/test_runtime.py`
- Modify: `src/embsw_tester/dsl/catalog.py`
- Modify: `src/embsw_tester/runtime/runner.py`

- [ ] **Step 1: Write failing runtime test for `serial.read.save_as`**

Create YAML that reads from a fake serial adapter, stores response into `psu_response`, then asserts it equals `"OK"`.

- [ ] **Step 2: Run and verify failure**

Run: `.venv/bin/python -m pytest tests/test_runtime.py -q`

Expected: FAIL until catalog accepts `serial.read` and runtime maps adapter values to variables.

- [ ] **Step 3: Implement catalog and runtime mapping**

Add `serial.read` to catalog with required `port`, optional `timeout_ms`, `until`, `save_as`. When an adapter command has `save_as`, store `adapter_result.values["text"]` if present, else `adapter_result.values["value"]`.

- [ ] **Step 4: Verify runtime tests pass**

Run: `.venv/bin/python -m pytest tests/test_runtime.py -q`

Expected: runtime tests pass.

### Task 3: Documentation And Full Verification

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Update README**

Mention Phase 5 Serial Adapter, `serial.read`, `save_as`, and raw evidence behavior.

- [ ] **Step 2: Run full verification**

Run:

```bash
.venv/bin/python -m pytest
.venv/bin/embsw-tester run samples/boot-smoke.yaml --run-id serial-adapter-smoke --reports-root reports --json
```

Expected: all tests pass and sample still runs with default mock adapter.

## Self-Review

- This plan does not require a physical COM port.
- Real pyserial integration remains a small future `SerialPort` implementation behind the same `SerialAdapter`.
- Evidence paths remain relative so reports can store and move them cleanly.
