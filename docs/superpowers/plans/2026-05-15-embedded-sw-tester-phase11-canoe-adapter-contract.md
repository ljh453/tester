# Embedded SW Tester Phase 11 CANoe Adapter Contract Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a testable CANoe/CANalyzer command catalog and adapter contract before implementing Windows-only Vector COM integration.

**Architecture:** Introduce an in-memory `CanoeAdapter` behind the existing adapter interface. Runtime already routes adapter-category commands through `AdapterRegistry`, so this phase adds command catalog entries and adapter behavior for measurement control, system variables, and signal reads. Real CANoe/CANalyzer automation can later replace the same adapter boundary without changing DSL semantics.

**Tech Stack:** Python 3.9+, pytest.

---

## File Structure

- `src/embsw_tester/adapters/canoe.py`: in-memory CANoe/CANalyzer adapter contract.
- `src/embsw_tester/adapters/__init__.py`: export `CanoeAdapter`.
- `src/embsw_tester/dsl/catalog.py`: add `canoe.measurement.start`, `canoe.measurement.stop`, `canoe.sysvar.set`, `canoe.sysvar.read`, `canoe.signal.read`.
- `tests/test_canoe_adapter.py`: direct adapter behavior tests.
- `tests/test_runtime.py`: runtime integration through registry and `save_as`.
- `README.md`: document CANoe/CANalyzer command support and Windows COM boundary.
- `docs/design/embedded-sw-tester-detailed-design.md`: update adapter contract section.

### Task 1: Failing Tests

**Files:**
- Create: `tests/test_canoe_adapter.py`
- Modify: `tests/test_runtime.py`

- [x] **Step 1: Add adapter measurement and sysvar tests**

Create tests that:

- start measurement and assert `measurement_running is True`
- set `Vehicle::Ignition` to `True`
- read `Vehicle::Ignition` and assert value is `True`
- stop measurement and assert `measurement_running is False`

- [x] **Step 2: Add signal read test**

Create a `CanoeAdapter(signals={"EngineSpeed": 850})`, execute `canoe.signal.read`, and assert returned `value` is `850`.

- [x] **Step 3: Add runtime integration test**

Compile YAML that starts measurement, sets a sysvar, reads it with `save_as`, reads a signal with `save_as`, asserts both values, and stops measurement.

- [x] **Step 4: Run focused tests**

Run:

```bash
.venv/bin/python -m pytest tests/test_canoe_adapter.py tests/test_runtime.py -q
```

Expected: new tests fail until catalog and adapter exist.

### Task 2: Catalog And Adapter

**Files:**
- Create: `src/embsw_tester/adapters/canoe.py`
- Modify: `src/embsw_tester/adapters/__init__.py`
- Modify: `src/embsw_tester/dsl/catalog.py`

- [x] **Step 1: Add command specs**

Add these adapter-category command specs with `adapter="canoe"`:

- `canoe.measurement.start`: optional `configuration`
- `canoe.measurement.stop`: no required args
- `canoe.sysvar.set`: required `namespace`, `name`, `value`
- `canoe.sysvar.read`: required `namespace`, `name`, optional `save_as`
- `canoe.signal.read`: required `signal`, optional `bus`, `channel`, `save_as`

- [x] **Step 2: Implement `CanoeAdapter`**

Implement in-memory state:

- `measurement_running: bool`
- `system_variables: Dict[str, Any]`
- `signals: Dict[str, Any]`

Behavior:

- start/stop toggles measurement state.
- sysvar set stores key `namespace::name`.
- sysvar read returns stored value or failed `AdapterResult`.
- signal read returns configured signal value or failed `AdapterResult`.

- [x] **Step 3: Export adapter**

Export `CanoeAdapter` from `embsw_tester.adapters`.

### Task 3: Docs And Verification

**Files:**
- Modify: `README.md`
- Modify: `docs/design/embedded-sw-tester-detailed-design.md`
- Modify: `docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase11-canoe-adapter-contract.md`

- [x] **Step 1: Document CANoe/CANalyzer support**

State that current support is an in-memory/test adapter contract and that real Windows COM integration comes later.

- [x] **Step 2: Run full pytest**

Run:

```bash
.venv/bin/python -m pytest
```

Expected: all tests pass.

- [x] **Step 3: Run CLI smoke**

Run:

```bash
.venv/bin/embsw-tester run samples/boot-smoke.yaml --run-id canoe-contract-smoke --reports-root reports --json
```

Expected: `status` is `passed` and `diagnostics` is empty.

- [ ] **Step 4: Commit and push**

Commit:

```bash
git add README.md docs/design/embedded-sw-tester-detailed-design.md docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase11-canoe-adapter-contract.md src/embsw_tester/adapters/canoe.py src/embsw_tester/adapters/__init__.py src/embsw_tester/dsl/catalog.py tests/test_canoe_adapter.py tests/test_runtime.py
git commit -m "feat: add canoe adapter contract"
git push -u origin main
```

## Self-Review

- This phase does not require Vector CANoe/CANalyzer to be installed.
- The DSL command names match the product design direction.
- Runtime and reports use the same adapter event path as serial, so later COM integration can preserve the event schema.
