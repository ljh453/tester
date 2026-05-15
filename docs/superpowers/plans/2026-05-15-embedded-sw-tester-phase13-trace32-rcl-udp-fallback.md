# Embedded SW Tester Phase 13 Trace32 RCL UDP Fallback Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a testable Trace32 adapter contract that uses RCL by default and falls back to UDP command transport when RCL is unavailable or fails.

**Architecture:** Introduce `Trace32Adapter` behind the existing adapter interface. The adapter receives injectable command transports, tries the RCL transport first by default, and falls back to UDP when configured. Real Lauterbach RCL and UDP implementations can later satisfy the same transport protocol without changing DSL/runtime semantics.

**Tech Stack:** Python 3.9+, pytest.

---

## File Structure

- `src/embsw_tester/adapters/trace32.py`: adapter, transport protocol, and fake transport for tests.
- `src/embsw_tester/adapters/__init__.py`: export `Trace32Adapter`, `Trace32CommandTransport`, `FakeTrace32Transport`.
- `src/embsw_tester/dsl/catalog.py`: add `trace32.command`.
- `tests/test_trace32_adapter.py`: direct adapter behavior tests for RCL default and UDP fallback.
- `tests/test_runtime.py`: runtime integration through registry and `save_as`.
- `README.md`: document Trace32 RCL default and UDP fallback boundary.
- `docs/design/embedded-sw-tester-detailed-design.md`: update adapter contract section.

### Task 1: Failing Tests

**Files:**
- Create: `tests/test_trace32_adapter.py`
- Modify: `tests/test_runtime.py`

- [x] **Step 1: Add RCL default test**

Create a `Trace32Adapter` with a successful fake RCL transport and a successful fake UDP transport. Execute `trace32.command` with `command: VERSION()` and assert:

- result succeeds
- `values["transport"] == "rcl"`
- `values["fallback_used"] is False`
- UDP fake transport was not called

- [x] **Step 2: Add UDP fallback test**

Create a failing fake RCL transport and a successful fake UDP transport. Execute `trace32.command` and assert:

- result succeeds
- `values["transport"] == "udp"`
- `values["fallback_used"] is True`
- both transports were called in order

- [x] **Step 3: Add runtime integration test**

Compile YAML with `trace32.command`, `save_as: trace32_response`, and assert runtime saves the command value from the fallback result.

- [x] **Step 4: Run focused tests**

Run:

```bash
.venv/bin/python -m pytest tests/test_trace32_adapter.py tests/test_runtime.py -q
```

Expected: new tests fail until catalog and adapter exist.

### Task 2: Catalog And Adapter

**Files:**
- Create: `src/embsw_tester/adapters/trace32.py`
- Modify: `src/embsw_tester/adapters/__init__.py`
- Modify: `src/embsw_tester/dsl/catalog.py`

- [x] **Step 1: Add command spec**

Add `trace32.command` as adapter-category command with:

- required: `command`
- optional: `timeout_ms`, `transport`, `fallback`, `save_as`
- adapter: `trace32`

- [x] **Step 2: Implement transport protocol**

Create `Trace32CommandTransport` protocol:

```python
class Trace32CommandTransport(Protocol):
    name: str
    def execute_command(self, command: str, timeout_ms: int) -> AdapterResult:
        ...
```

- [x] **Step 3: Implement `Trace32Adapter`**

Behavior:

- Default transport selection is `rcl`.
- `transport: udp` uses UDP only.
- `transport: rcl` or omitted tries RCL first.
- `fallback` defaults to `True`.
- If RCL fails and fallback is enabled, try UDP.
- Return values include `command`, `transport`, `fallback_used`, `attempts`, and `value` if provided by the winning transport.

- [x] **Step 4: Implement `FakeTrace32Transport`**

Fake transport should capture commands and return a configured `AdapterResult`.

### Task 3: Docs And Verification

**Files:**
- Modify: `README.md`
- Modify: `docs/design/embedded-sw-tester-detailed-design.md`
- Modify: `docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase13-trace32-rcl-udp-fallback.md`

- [x] **Step 1: Document Trace32 support**

State that current support is a transport-level contract. Default is RCL, and UDP command transport is fallback.

- [x] **Step 2: Run full pytest**

Run:

```bash
.venv/bin/python -m pytest
```

Expected: all tests pass.

- [x] **Step 3: Run CLI smoke**

Run:

```bash
.venv/bin/embsw-tester run samples/boot-smoke.yaml --run-id trace32-contract-smoke --reports-root reports --json
```

Expected: `status` is `passed` and `diagnostics` is empty.

- [ ] **Step 4: Commit and push**

Commit:

```bash
git add README.md docs/design/embedded-sw-tester-detailed-design.md docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase13-trace32-rcl-udp-fallback.md src/embsw_tester/adapters/trace32.py src/embsw_tester/adapters/__init__.py src/embsw_tester/dsl/catalog.py tests/test_trace32_adapter.py tests/test_runtime.py
git commit -m "feat: add trace32 rcl udp fallback"
git push -u origin main
```

## Self-Review

- This phase does not require Trace32 to be installed.
- RCL is the default path.
- UDP is a fallback transport, not a separate DSL command surface.
- Runtime and reports use the same adapter event path as Serial, CANoe, and INCA.
