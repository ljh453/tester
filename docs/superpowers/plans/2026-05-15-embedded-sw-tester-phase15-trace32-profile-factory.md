# Embedded SW Tester Phase 15 Trace32 Profile Factory Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Connect Trace32 RCL/UDP transports to the tool profile factory so tests and future Windows smoke runs can build a configured Trace32 adapter from YAML profile settings.

**Architecture:** Keep `Trace32Adapter` and transport execution behavior unchanged. Add a focused `trace32_factory` module that translates normalized `trace32` profile data into `RclTrace32Transport` and `UdpTrace32Transport`, then wire it into the existing adapter registry factory. RCL clients are created either by an injected test factory or by an import-path factory declared in the profile.

**Tech Stack:** Python 3.9+, pytest, standard-library `importlib` and `socket`.

---

## File Structure

- `src/embsw_tester/tools/profile.py`: normalize and validate the `trace32` section.
- `src/embsw_tester/adapters/trace32_factory.py`: create `Trace32Adapter` from profile data.
- `src/embsw_tester/adapters/serial_factory.py`: register Trace32 adapter when the profile contains Trace32 settings.
- `src/embsw_tester/adapters/__init__.py`: export the new Trace32 factory function.
- `tests/test_tool_profile.py`: profile normalization tests.
- `tests/test_trace32_factory.py`: Trace32 factory and registry wiring tests.
- `README.md`: document Trace32 profile settings.
- `docs/design/embedded-sw-tester-detailed-design.md`: update Trace32 adapter profile notes.

### Task 1: Failing Tests

**Files:**
- Modify: `tests/test_tool_profile.py`
- Create: `tests/test_trace32_factory.py`

- [x] **Step 1: Add Trace32 profile normalization test**

Add a YAML profile containing:

```yaml
trace32:
  rcl:
    enabled: true
    client_factory: lab_trace32:create_client
    command_method: command
    client_args:
      node: TRACE32-A
  udp:
    enabled: true
    host: 127.0.0.1
    port: 20000
    terminator: "\n"
    response_bytes: 2048
```

Assert the normalized snapshot keeps the RCL factory path, normalizes `enabled` to `True`, keeps `command_method`, preserves `client_args`, converts UDP `port` and `response_bytes` to integers, and keeps the UDP host.

- [x] **Step 2: Add Trace32 adapter factory test**

Create a fake RCL client whose configured command method returns a failed mapping and a fake UDP socket returning `b"STATE:HALTED\n"`. Build the adapter with `create_trace32_adapter_from_profile(...)`, execute `trace32.command`, and assert RCL is attempted first, UDP fallback succeeds, and the RCL factory received normalized config.

- [x] **Step 3: Add import-path RCL factory test**

Create a temporary Python module with a `create_client(**kwargs)` function. Point `trace32.rcl.client_factory` to `module:create_client`, build the adapter without an injected factory, execute `trace32.command`, and assert the imported client returns the configured response.

- [x] **Step 4: Add registry wiring test**

Call `create_adapter_registry_from_tool_profile(...)` with Trace32 settings and an injected RCL client factory. Assert `registry.get("trace32")` executes through the configured Trace32 adapter instead of the default mock adapter.

- [x] **Step 5: Run focused tests and verify RED**

Run:

```bash
.venv/bin/python -m pytest tests/test_tool_profile.py tests/test_trace32_factory.py -q
```

Expected: failures because the Trace32 factory and profile normalization do not exist yet.

### Task 2: Profile Normalization

**Files:**
- Modify: `src/embsw_tester/tools/profile.py`

- [x] **Step 1: Normalize `trace32.rcl`**

Accept only mapping values. Normalize:

- `enabled`: bool, default `True`
- `client_factory`: optional string
- `command_method`: string, default `cmd`
- `client_args`: mapping, default `{}`

- [x] **Step 2: Normalize `trace32.udp`**

Accept only mapping values. Normalize:

- `enabled`: bool, default `True`
- `host`: required string when enabled
- `port`: required integer when enabled
- `terminator`: string, default `"\n"`
- `encoding`: string, default `"utf-8"`
- `response_bytes`: integer, default `4096`

### Task 3: Trace32 Factory

**Files:**
- Create: `src/embsw_tester/adapters/trace32_factory.py`
- Modify: `src/embsw_tester/adapters/serial_factory.py`
- Modify: `src/embsw_tester/adapters/__init__.py`

- [x] **Step 1: Implement `create_trace32_adapter_from_profile`**

Implement:

```python
def create_trace32_adapter_from_profile(
    tool_profile_snapshot,
    rcl_client_factory=None,
    udp_socket_factory=None,
) -> Trace32Adapter:
    ...
```

The function builds `RclTrace32Transport` when an injected factory or `client_factory` import path is configured, and builds `UdpTrace32Transport` when UDP profile settings are enabled.

- [x] **Step 2: Implement import-path factory loading**

Support `module:attribute` factory paths. Call the loaded factory with `**client_args`.

- [x] **Step 3: Wire registry factory**

Extend `create_adapter_registry_from_tool_profile(...)` with optional `trace32_rcl_client_factory` and `trace32_udp_socket_factory`. Register a Trace32 adapter when the profile contains a `trace32` section.

- [x] **Step 4: Export the factory**

Export `create_trace32_adapter_from_profile` from `embsw_tester.adapters`.

### Task 4: Docs And Verification

**Files:**
- Modify: `README.md`
- Modify: `docs/design/embedded-sw-tester-detailed-design.md`
- Modify: `docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase15-trace32-profile-factory.md`

- [x] **Step 1: Document Trace32 profile settings**

Add a YAML snippet showing `trace32.rcl` and `trace32.udp`, and describe that actual RCL package differences are handled by an injected or import-path client factory.

- [x] **Step 2: Run focused tests**

Run:

```bash
.venv/bin/python -m pytest tests/test_tool_profile.py tests/test_trace32_factory.py tests/test_trace32_transport.py tests/test_trace32_adapter.py -q
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
.venv/bin/embsw-tester run samples/boot-smoke.yaml --run-id trace32-profile-factory-smoke --reports-root reports --json
```

Expected: `status` is `passed` and `diagnostics` is empty.

- [ ] **Step 5: Commit and push**

Commit:

```bash
git add README.md docs/design/embedded-sw-tester-detailed-design.md docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase15-trace32-profile-factory.md src/embsw_tester/tools/profile.py src/embsw_tester/adapters/trace32_factory.py src/embsw_tester/adapters/serial_factory.py src/embsw_tester/adapters/__init__.py tests/test_tool_profile.py tests/test_trace32_factory.py
git commit -m "feat: add trace32 profile factory"
git push -u origin main
```

## Self-Review

- The CLI smoke remains mock-safe unless a caller explicitly supplies a profile-built registry.
- RCL package specifics stay outside the runtime and transport contract.
- UDP socket creation remains injectable for tests and future Windows smoke work.
