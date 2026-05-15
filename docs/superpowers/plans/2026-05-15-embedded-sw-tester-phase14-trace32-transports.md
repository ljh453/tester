# Embedded SW Tester Phase 14 Trace32 Transports Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add concrete Trace32 transport implementations behind the existing RCL-first, UDP-fallback adapter contract.

**Architecture:** Keep `Trace32Adapter` unchanged at the DSL/runtime boundary and add two `Trace32CommandTransport` implementations. `RclTrace32Transport` wraps an injected RCL client object so Windows/Lauterbach-specific package differences stay outside runtime code. `UdpTrace32Transport` uses stdlib `socket` with injectable socket factory for tests and future configuration.

**Tech Stack:** Python 3.9+, standard-library `socket`, pytest.

---

## File Structure

- `src/embsw_tester/adapters/trace32.py`: add `RclTrace32Transport` and `UdpTrace32Transport`.
- `src/embsw_tester/adapters/__init__.py`: export new transports.
- `tests/test_trace32_transport.py`: transport-level tests with fake RCL client and fake UDP socket.
- `README.md`: document RCL wrapper and UDP socket transport usage.
- `docs/design/embedded-sw-tester-detailed-design.md`: update Trace32 transport implementation notes.

### Task 1: Failing Tests

**Files:**
- Create: `tests/test_trace32_transport.py`

- [x] **Step 1: Add RCL wrapper test**

Create a fake RCL client with method `cmd(command)` returning `"VERSION OK"`. Execute `RclTrace32Transport(client=fake).execute_command("VERSION()", 1000)` and assert:

- result succeeds
- result values contain `value: "VERSION OK"`
- client received `VERSION()`

- [x] **Step 2: Add UDP socket success test**

Create a fake socket factory whose socket captures `settimeout`, `sendall`, and returns `b"STATE:HALTED\n"` from `recv`. Execute `UdpTrace32Transport(host="127.0.0.1", port=20000, socket_factory=factory).execute_command("STATE()", 2500)` and assert:

- socket connects to `("127.0.0.1", 20000)`
- timeout is `2.5`
- sent payload is `b"STATE()\n"`
- result value is `"STATE:HALTED"`

- [x] **Step 3: Add UDP socket failure test**

Create a fake socket whose `recv` raises `TimeoutError("timed out")`. Assert result fails and message mentions UDP.

- [x] **Step 4: Add UDP socket creation failure test**

Create a fake socket factory that raises `OSError("socket unavailable")`. Assert result fails and message mentions UDP.

- [x] **Step 5: Run focused tests**

Run:

```bash
.venv/bin/python -m pytest tests/test_trace32_transport.py tests/test_trace32_adapter.py -q
```

Expected: new transport tests fail until implementation exists.

### Task 2: Transport Implementation

**Files:**
- Modify: `src/embsw_tester/adapters/trace32.py`
- Modify: `src/embsw_tester/adapters/__init__.py`

- [x] **Step 1: Implement `RclTrace32Transport`**

Constructor args:

- `client: Any`
- `command_method: str = "cmd"`

Behavior:

- Calls `getattr(client, command_method)(command)`.
- Converts returned `AdapterResult`, mapping, bytes, or any scalar to `AdapterResult`.
- On exception, returns failed `AdapterResult`.

- [x] **Step 2: Implement `UdpTrace32Transport`**

Constructor args:

- `host: str`
- `port: int`
- `terminator: str = "\n"`
- `encoding: str = "utf-8"`
- `response_bytes: int = 4096`
- `socket_factory: Callable[..., Any] = socket.socket`

Behavior:

- Creates UDP socket.
- Sets timeout from `timeout_ms / 1000`.
- Sends command plus terminator.
- Receives one datagram.
- Returns stripped text as `values["value"]`.
- On socket errors, returns failed `AdapterResult`.

- [x] **Step 3: Export transports**

Export `RclTrace32Transport` and `UdpTrace32Transport`.

### Task 3: Docs And Verification

**Files:**
- Modify: `README.md`
- Modify: `docs/design/embedded-sw-tester-detailed-design.md`
- Modify: `docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase14-trace32-transports.md`

- [x] **Step 1: Document transport usage**

Document that RCL remains default at adapter level, while UDP is used as fallback. Show how to instantiate `Trace32Adapter` with `RclTrace32Transport` and `UdpTrace32Transport`.

- [x] **Step 2: Run full pytest**

Run:

```bash
.venv/bin/python -m pytest
```

Expected: all tests pass.

- [x] **Step 3: Run CLI smoke**

Run:

```bash
.venv/bin/embsw-tester run samples/boot-smoke.yaml --run-id trace32-transport-smoke --reports-root reports --json
```

Expected: `status` is `passed` and `diagnostics` is empty.

- [ ] **Step 4: Commit and push**

Commit:

```bash
git add README.md docs/design/embedded-sw-tester-detailed-design.md docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase14-trace32-transports.md src/embsw_tester/adapters/trace32.py src/embsw_tester/adapters/__init__.py tests/test_trace32_transport.py
git commit -m "feat: add trace32 transports"
git push -u origin main
```

## Self-Review

- RCL package specifics remain isolated behind injected clients.
- UDP transport uses injectable sockets, so tests do not require network access.
- Existing RCL-first/UDP-fallback adapter behavior remains unchanged.
