# Embedded SW Tester Phase 17 INCA Profile Factory Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Configure the INCA 32bit helper process command from tool profiles and build a profile-backed `IncaAdapter` automatically through the adapter registry factory.

**Architecture:** Follow the existing Trace32 profile factory pattern. Normalize an `inca.helper` profile section in `tools/profile.py`, create a focused `inca_factory.py` module that turns that normalized config into `IncaAdapter(bridge_transport=JsonLineIncaBridgeTransport(...))`, and wire it into `create_adapter_registry_from_tool_profile`. The helper process protocol stays in `inca_bridge.py`.

**Tech Stack:** Python 3.9+, pytest, standard-library subprocess.

---

## File Structure

- `src/embsw_tester/tools/profile.py`: normalize and validate `inca.helper`.
- `src/embsw_tester/adapters/inca_factory.py`: create profile-backed INCA adapters.
- `src/embsw_tester/adapters/serial_factory.py`: register INCA adapter when profile contains helper settings.
- `src/embsw_tester/adapters/__init__.py`: export INCA factory.
- `tests/test_tool_profile.py`: INCA helper profile normalization test.
- `tests/test_inca_factory.py`: adapter factory and registry wiring tests.
- `README.md`: document INCA tool profile settings.
- `docs/design/embedded-sw-tester-detailed-design.md`: update INCA profile notes.

### Task 1: Failing Tests

**Files:**
- Modify: `tests/test_tool_profile.py`
- Create: `tests/test_inca_factory.py`

- [x] **Step 1: Add INCA helper profile normalization test**

Add a YAML profile:

```yaml
inca:
  helper:
    enabled: true
    command:
      - C:/Python32/python.exe
      - C:/tools/inca_helper.py
```

Assert `enabled` is `True` and `command` is normalized to a list of strings.

- [x] **Step 2: Add INCA adapter factory test**

Use `create_inca_adapter_from_profile(...)` with a fake `popen_factory` that returns a fake JSON line process. Execute `inca.measure.read` and assert the adapter sends a request to the helper process and returns the helper response.

- [x] **Step 3: Add registry wiring test**

Call `create_adapter_registry_from_tool_profile(...)` with `inca.helper.command` and a fake `inca_popen_factory`. Assert `registry.get("inca")` executes through the helper-backed adapter instead of the default mock adapter.

- [x] **Step 4: Run focused tests and verify RED**

Run:

```bash
.venv/bin/python -m pytest tests/test_tool_profile.py tests/test_inca_factory.py -q
```

Expected: failures because INCA profile normalization and factory do not exist yet.

### Task 2: Profile Normalization

**Files:**
- Modify: `src/embsw_tester/tools/profile.py`

- [x] **Step 1: Normalize `inca.helper`**

Accept only mapping values. Normalize:

- `enabled`: bool, default `True`
- `command`: required non-empty sequence of strings when enabled

- [x] **Step 2: Preserve unknown INCA keys**

Copy any non-`helper` keys under `inca` into the normalized snapshot so future profile fields can be added without losing data.

### Task 3: INCA Factory

**Files:**
- Create: `src/embsw_tester/adapters/inca_factory.py`
- Modify: `src/embsw_tester/adapters/serial_factory.py`
- Modify: `src/embsw_tester/adapters/__init__.py`

- [x] **Step 1: Implement `create_inca_adapter_from_profile`**

Implement:

```python
def create_inca_adapter_from_profile(
    tool_profile_snapshot,
    popen_factory=subprocess.Popen,
    request_id_factory=lambda: str(uuid.uuid4()),
) -> IncaAdapter:
    ...
```

When `inca.helper.enabled` is true, create a process transport with `create_inca_bridge_process_transport(command, ...)` and return `IncaAdapter(bridge_transport=transport)`.

- [x] **Step 2: Implement `has_inca_profile`**

Return `True` when `tool_profile_snapshot["inca"]` is a mapping.

- [x] **Step 3: Wire registry factory**

Extend `create_adapter_registry_from_tool_profile(...)` with optional `inca_popen_factory` and `inca_request_id_factory`. Register an INCA adapter when the profile contains an `inca` section.

- [x] **Step 4: Export the factory**

Export `create_inca_adapter_from_profile` from `embsw_tester.adapters`.

### Task 4: Docs And Verification

**Files:**
- Modify: `README.md`
- Modify: `docs/design/embedded-sw-tester-detailed-design.md`
- Modify: `docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase17-inca-profile-factory.md`

- [x] **Step 1: Document INCA helper profile settings**

Add a YAML snippet showing:

```yaml
inca:
  helper:
    enabled: true
    command:
      - C:/Python32/python.exe
      - C:/tools/inca_helper.py
```

- [x] **Step 2: Run focused tests**

Run:

```bash
.venv/bin/python -m pytest tests/test_tool_profile.py tests/test_inca_factory.py tests/test_inca_bridge_transport.py tests/test_inca_adapter.py -q
```

Expected: all focused tests pass.

- [x] **Step 3: Run full pytest**

Run:

```bash
.venv/bin/python -m pytest
```

Expected: all tests pass.

- [x] **Step 4: Run CLI smoke**

Run:

```bash
.venv/bin/embsw-tester run samples/boot-smoke.yaml --run-id inca-profile-factory-smoke --reports-root reports --json
```

Expected: `status` is `passed` and `diagnostics` is empty.

- [ ] **Step 5: Commit and push**

Commit:

```bash
git add README.md docs/design/embedded-sw-tester-detailed-design.md docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase17-inca-profile-factory.md src/embsw_tester/tools/profile.py src/embsw_tester/adapters/inca_factory.py src/embsw_tester/adapters/serial_factory.py src/embsw_tester/adapters/__init__.py tests/test_tool_profile.py tests/test_inca_factory.py
git commit -m "feat: add inca profile factory"
git push -u origin main
```

## Self-Review

- The default CLI path stays mock-safe unless profile-backed registry construction is used.
- 32bit helper process launch remains in the composition/factory layer.
- JSON line bridge protocol remains unchanged.
