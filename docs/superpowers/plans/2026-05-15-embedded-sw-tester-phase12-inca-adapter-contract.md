# Embedded SW Tester Phase 12 INCA Adapter Contract Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a testable INCA command catalog, in-memory adapter contract, and 32bit helper RPC message schema before implementing Windows-only INCA COM integration.

**Architecture:** Introduce an in-memory `IncaAdapter` behind the existing adapter interface and an `inca_bridge` module containing serializable request/response dataclasses for the future 32bit Python helper process. Runtime already routes adapter-category commands through `AdapterRegistry`, so this phase adds command catalog entries and adapter behavior for measurement read, calibration set, and recording start/stop.

**Tech Stack:** Python 3.9+, pytest.

---

## File Structure

- `src/embsw_tester/adapters/inca.py`: in-memory INCA adapter contract.
- `src/embsw_tester/adapters/inca_bridge.py`: 32bit helper request/response schema.
- `src/embsw_tester/adapters/__init__.py`: export `IncaAdapter`, `IncaBridgeRequest`, `IncaBridgeResponse`.
- `src/embsw_tester/dsl/catalog.py`: add `inca.measure.read`, `inca.calibration.set`, `inca.recording.start`, `inca.recording.stop`.
- `tests/test_inca_adapter.py`: direct adapter behavior tests.
- `tests/test_inca_bridge.py`: RPC schema serialization tests.
- `tests/test_runtime.py`: runtime integration through registry and `save_as`.
- `README.md`: document INCA command support and 32bit helper boundary.
- `docs/design/embedded-sw-tester-detailed-design.md`: update adapter contract section.

### Task 1: Failing Tests

**Files:**
- Create: `tests/test_inca_adapter.py`
- Create: `tests/test_inca_bridge.py`
- Modify: `tests/test_runtime.py`

- [x] **Step 1: Add INCA adapter tests**

Create tests that:

- read `EngineSpeed` from `IncaAdapter(measurements={"EngineSpeed": 900})`
- set calibration `IdleSpeedTarget` to `850`
- start and stop recording with `name: boot`

- [x] **Step 2: Add helper RPC schema tests**

Create a request:

```python
IncaBridgeRequest(
    request_id="req-1",
    command_type="inca.measure.read",
    args={"variable": "EngineSpeed"},
    timeout_ms=2500,
)
```

Assert `to_dict()` and `from_dict()` round-trip. Create a response and assert `to_adapter_result()` preserves `success`, `status`, `message`, and `values`.

- [x] **Step 3: Add runtime integration test**

Compile YAML that starts recording, reads `EngineSpeed` with `save_as: rpm`, sets calibration, asserts `rpm > 0`, and stops recording.

- [x] **Step 4: Run focused tests**

Run:

```bash
.venv/bin/python -m pytest tests/test_inca_adapter.py tests/test_inca_bridge.py tests/test_runtime.py -q
```

Expected: new tests fail until catalog, adapter, and bridge schema exist.

### Task 2: Catalog And Adapter

**Files:**
- Create: `src/embsw_tester/adapters/inca.py`
- Modify: `src/embsw_tester/adapters/__init__.py`
- Modify: `src/embsw_tester/dsl/catalog.py`

- [x] **Step 1: Add command specs**

Add these adapter-category command specs with `adapter="inca"`:

- `inca.measure.read`: required `variable`, optional `save_as`
- `inca.calibration.set`: required `parameter`, `value`
- `inca.recording.start`: optional `name`, `output_dir`
- `inca.recording.stop`: no required args

- [x] **Step 2: Implement `IncaAdapter`**

Implement in-memory state:

- `measurements: Dict[str, Any]`
- `calibrations: Dict[str, Any]`
- `recording_active: bool`
- `recording_name: Optional[str]`

Behavior:

- measure read returns configured measurement value or failed `AdapterResult`.
- calibration set stores parameter value.
- recording start/stop toggles state and records optional name/output_dir.

- [x] **Step 3: Export adapter**

Export `IncaAdapter` from `embsw_tester.adapters`.

### Task 3: 32bit Helper RPC Schema

**Files:**
- Create: `src/embsw_tester/adapters/inca_bridge.py`
- Modify: `src/embsw_tester/adapters/__init__.py`

- [x] **Step 1: Implement request schema**

Create frozen dataclass `IncaBridgeRequest` with:

- `request_id: str`
- `command_type: str`
- `args: Dict[str, Any]`
- `timeout_ms: int = 1000`

Provide `to_dict()` and `from_dict()`.

- [x] **Step 2: Implement response schema**

Create frozen dataclass `IncaBridgeResponse` with:

- `request_id: str`
- `success: bool`
- `status: str`
- `message: str = ""`
- `values: Dict[str, Any] = field(default_factory=dict)`
- `error: Optional[str] = None`

Provide `to_dict()`, `from_dict()`, and `to_adapter_result()`.

### Task 4: Docs And Verification

**Files:**
- Modify: `README.md`
- Modify: `docs/design/embedded-sw-tester-detailed-design.md`
- Modify: `docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase12-inca-adapter-contract.md`

- [x] **Step 1: Document INCA support**

State that current support is an in-memory/test adapter contract and serializable helper RPC schema. Actual INCA COM calls remain a Windows 32bit helper implementation task.

- [x] **Step 2: Run full pytest**

Run:

```bash
.venv/bin/python -m pytest
```

Expected: all tests pass.

- [x] **Step 3: Run CLI smoke**

Run:

```bash
.venv/bin/embsw-tester run samples/boot-smoke.yaml --run-id inca-contract-smoke --reports-root reports --json
```

Expected: `status` is `passed` and `diagnostics` is empty.

- [ ] **Step 4: Commit and push**

Commit:

```bash
git add README.md docs/design/embedded-sw-tester-detailed-design.md docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase12-inca-adapter-contract.md src/embsw_tester/adapters/inca.py src/embsw_tester/adapters/inca_bridge.py src/embsw_tester/adapters/__init__.py src/embsw_tester/dsl/catalog.py tests/test_inca_adapter.py tests/test_inca_bridge.py tests/test_runtime.py
git commit -m "feat: add inca adapter contract"
git push -u origin main
```

## Self-Review

- This phase does not require INCA or COM to be installed.
- The 32bit helper boundary is explicit and serializable.
- Runtime and reports use the same adapter event path as Serial and CANoe.
