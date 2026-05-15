# Embedded SW Tester Phase 16 INCA Bridge Transport Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a JSON line stdio transport for the 32bit Python INCA helper process and let `IncaAdapter` delegate commands through that bridge when configured.

**Architecture:** Keep the existing INCA command DSL and `IncaAdapter` public contract unchanged. Extend `inca_bridge.py` with an `IncaBridgeTransport` protocol, a `JsonLineIncaBridgeTransport` around a process-like object, and a subprocess factory for future Windows 32bit helper launch. `IncaAdapter` remains usable as an in-memory test adapter, but delegates supported commands to the bridge transport when one is injected.

**Tech Stack:** Python 3.9+, pytest, standard-library `json`, `subprocess`, and `uuid`.

---

## File Structure

- `src/embsw_tester/adapters/inca_bridge.py`: add JSON line bridge transport and subprocess factory.
- `src/embsw_tester/adapters/inca.py`: delegate to bridge transport when configured.
- `src/embsw_tester/adapters/__init__.py`: export bridge transport helpers.
- `src/embsw_tester/dsl/catalog.py`: mark `timeout_ms` as an optional INCA command argument.
- `tests/test_catalog.py`: catalog contract test for INCA timeout args.
- `tests/test_inca_bridge_transport.py`: transport-level JSON line tests.
- `tests/test_inca_adapter.py`: adapter delegation test.
- `README.md`: document INCA bridge process transport usage.
- `docs/design/embedded-sw-tester-detailed-design.md`: update INCA bridge design notes.

### Task 1: Failing Tests

**Files:**
- Create: `tests/test_inca_bridge_transport.py`
- Modify: `tests/test_inca_adapter.py`

- [x] **Step 1: Add JSON line success test**

Create a fake process with writable stdin and readable stdout. Use `request_id_factory=lambda: "req-1"` and stdout response:

```json
{"request_id":"req-1","success":true,"status":"passed","message":"read","values":{"variable":"EngineSpeed","value":900}}
```

Execute:

```python
transport.execute("inca.measure.read", {"variable": "EngineSpeed"}, timeout_ms=2500)
```

Assert:

- stdin receives one JSON line with `request_id`, `command_type`, `args`, and `timeout_ms`
- stdin is flushed
- result succeeds and contains `value: 900`

- [x] **Step 2: Add mismatched response id test**

Configure stdout to return `request_id: "other"`. Assert transport returns failed `AdapterResult` and message mentions unexpected response id.

- [x] **Step 3: Add invalid JSON test**

Configure stdout to return `not-json\n`. Assert transport returns failed `AdapterResult` and message mentions invalid JSON.

- [x] **Step 4: Add subprocess factory test**

Use a fake `popen_factory` that captures command and kwargs, returns a fake process, then call:

```python
create_inca_bridge_process_transport(["C:/Python32/python.exe", "inca_helper.py"], popen_factory=fake)
```

Assert the factory receives `stdin`, `stdout`, and `stderr` pipes, text mode, UTF-8 encoding, and line buffering.

- [x] **Step 5: Add adapter delegation test**

Inject a fake bridge transport into `IncaAdapter`. Execute `inca.measure.read` with `timeout_ms: 2500`. Assert the bridge receives command args without `timeout_ms`, timeout is passed separately, and the adapter result comes from the bridge.

- [x] **Step 6: Add INCA command catalog timeout test**

Assert `timeout_ms` is listed in `COMMAND_SPECS` optional args for `inca.measure.read`, `inca.calibration.set`, `inca.recording.start`, and `inca.recording.stop`.

- [x] **Step 7: Run focused tests and verify RED**

Run:

```bash
.venv/bin/python -m pytest tests/test_inca_bridge_transport.py tests/test_inca_adapter.py -q
```

Expected: failures because the transport and adapter delegation do not exist yet.

### Task 2: Bridge Transport Implementation

**Files:**
- Modify: `src/embsw_tester/adapters/inca_bridge.py`
- Modify: `src/embsw_tester/adapters/__init__.py`

- [x] **Step 1: Define `IncaBridgeTransport` protocol**

Add:

```python
class IncaBridgeTransport(Protocol):
    def execute(self, command_type: str, args: Mapping[str, Any], timeout_ms: int) -> AdapterResult:
        ...
```

- [x] **Step 2: Implement `JsonLineIncaBridgeTransport`**

Behavior:

- Builds `IncaBridgeRequest`
- Writes `json.dumps(request.to_dict(), ensure_ascii=False) + "\n"` to `process.stdin`
- Flushes stdin
- Reads one line from `process.stdout.readline()`
- Parses `IncaBridgeResponse`
- Verifies response `request_id`
- Converts response to `AdapterResult`
- Converts IO/JSON/protocol errors to failed `AdapterResult`

- [x] **Step 3: Implement process factory**

Add:

```python
def create_inca_bridge_process_transport(command, popen_factory=subprocess.Popen):
    ...
```

Use `stdin=subprocess.PIPE`, `stdout=subprocess.PIPE`, `stderr=subprocess.PIPE`, `text=True`, `encoding="utf-8"`, `bufsize=1`.

- [x] **Step 4: Export bridge transport helpers**

- [x] **Step 5: Add INCA command timeout catalog metadata**

Add `timeout_ms` as an optional argument for `inca.measure.read`, `inca.calibration.set`, `inca.recording.start`, and `inca.recording.stop`.

Export `IncaBridgeTransport`, `JsonLineIncaBridgeTransport`, and `create_inca_bridge_process_transport`.

### Task 3: Adapter Delegation

**Files:**
- Modify: `src/embsw_tester/adapters/inca.py`

- [x] **Step 1: Accept optional bridge transport**

Add constructor parameter:

```python
bridge_transport: Optional[IncaBridgeTransport] = None
```

- [x] **Step 2: Delegate supported commands**

For `inca.measure.read`, `inca.calibration.set`, `inca.recording.start`, and `inca.recording.stop`, if `bridge_transport` is configured:

- Extract `timeout_ms = int(args.get("timeout_ms", 1000))`
- Remove `timeout_ms` from bridge args
- Return `bridge_transport.execute(command_type, bridge_args, timeout_ms)`

- [x] **Step 3: Keep in-memory behavior unchanged**

If no bridge transport is configured, existing measurement/calibration/recording behavior remains unchanged.

### Task 4: Docs And Verification

**Files:**
- Modify: `README.md`
- Modify: `docs/design/embedded-sw-tester-detailed-design.md`
- Modify: `docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase16-inca-bridge-transport.md`

- [x] **Step 1: Document INCA bridge usage**

Show how to create a bridge transport with:

```python
transport = create_inca_bridge_process_transport([
    r"C:\Python32\python.exe",
    r"C:\tools\inca_helper.py",
])
registry.register("inca", IncaAdapter(bridge_transport=transport))
```

- [x] **Step 2: Run focused tests**

Run:

```bash
.venv/bin/python -m pytest tests/test_inca_bridge.py tests/test_inca_bridge_transport.py tests/test_inca_adapter.py -q
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
.venv/bin/embsw-tester run samples/boot-smoke.yaml --run-id inca-bridge-transport-smoke --reports-root reports --json
```

Expected: `status` is `passed` and `diagnostics` is empty.

- [ ] **Step 5: Commit and push**

Commit:

```bash
git add README.md docs/design/embedded-sw-tester-detailed-design.md docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase16-inca-bridge-transport.md src/embsw_tester/adapters/inca_bridge.py src/embsw_tester/adapters/inca.py src/embsw_tester/adapters/__init__.py src/embsw_tester/dsl/catalog.py tests/test_catalog.py tests/test_inca_bridge_transport.py tests/test_inca_adapter.py
git commit -m "feat: add inca bridge transport"
git push -u origin main
```

## Self-Review

- The adapter remains usable without a helper process.
- 32bit/COM details stay behind the helper process boundary.
- The transport is testable with fake process objects and does not require Windows or INCA.
