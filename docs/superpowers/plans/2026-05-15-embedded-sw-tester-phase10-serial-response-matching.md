# Embedded SW Tester Phase 10 Serial Response Matching Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add regex-based serial response matching so tests can validate device replies more precisely than substring `until`.

**Architecture:** Extend `SerialAdapter` so `serial.read` accepts optional `match` regex. Extend device command profiles so `read.match` is rendered from command args and passed to `serial.read`. Compiler validates static `read.match` regex patterns in referenced command profiles when they do not contain template expressions.

**Tech Stack:** Python 3.9+, standard-library `re`, PyYAML, pytest.

---

## File Structure

- `src/embsw_tester/adapters/serial.py`: support `match` regex in `serial.read`.
- `src/embsw_tester/devices/command_profiles.py`: pass profile `read.match` through to `serial.read`.
- `src/embsw_tester/dsl/catalog.py`: add `match` to `serial.read` optional args.
- `src/embsw_tester/dsl/compiler.py`: validate static profile `read.match` regex.
- `tests/test_serial_adapter.py`: cover regex match success and failure at adapter level.
- `tests/test_device_command_profiles.py`: cover profile `read.match` forwarding and invalid match diagnostics.
- `samples/tool-profiles/lab-serial.tools.yaml`: show `read.match`.
- `README.md`: document `read.match` vs `until`.
- `docs/design/embedded-sw-tester-detailed-design.md`: update matching policy.

### Task 1: Failing Tests

**Files:**
- Modify: `tests/test_serial_adapter.py`
- Modify: `tests/test_device_command_profiles.py`

- [x] **Step 1: Add serial adapter match success test**

Use `FakeSerialPort(rx_lines=["VALUE:123"])` and call:

```python
adapter.execute(
    "serial.read",
    {"port": "sent_usb", "match": r"VALUE:\d+"},
    AdapterContext(run_id="serial-run", testcase="case", phase="steps"),
)
```

Assert success and `result.values["match"] == r"VALUE:\d+"`.

- [x] **Step 2: Add serial adapter match failure test**

Use `FakeSerialPort(rx_lines=["ERROR"])` with the same `match` and assert `result.success is False`.

- [x] **Step 3: Add device profile match test**

Use a `sent_usb.read` profile with:

```yaml
read:
  match: 'VALUE:{{ channel }}:\d+'
  extract: 'VALUE:{{ channel }}:(?P<value>\d+)'
```

Run with `channel: 1`, fake response `VALUE:1:123`, and assert saved value is `"123"`.

- [x] **Step 4: Add invalid static match diagnostic test**

Use `read.match: "VALUE:("` and assert diagnostic code `INVALID_RESPONSE_MATCHER`.

- [x] **Step 5: Run focused tests**

Run:

```bash
.venv/bin/python -m pytest tests/test_serial_adapter.py tests/test_device_command_profiles.py -q
```

Expected: new tests fail until implementation exists.

### Task 2: Serial Adapter Implementation

**Files:**
- Modify: `src/embsw_tester/adapters/serial.py`
- Modify: `src/embsw_tester/dsl/catalog.py`

- [x] **Step 1: Add `match` catalog metadata**

Add `match` to `serial.read` optional args.

- [x] **Step 2: Check regex match in `_execute_read`**

After reading text and checking `until`, if `args.get("match")` is not `None`, run `re.search(pattern, text)`.

Return failed `AdapterResult` when:

- regex is invalid
- regex does not match the response

On success, include `"match": pattern` in `values`.

### Task 3: Device Profile Match Forwarding And Validation

**Files:**
- Modify: `src/embsw_tester/devices/command_profiles.py`
- Modify: `src/embsw_tester/dsl/compiler.py`

- [x] **Step 1: Forward rendered `read.match`**

In `_serial_read_args`, resolve `match = args.get("match", read_definition.get("match"))`. If present, render it with `render_template` and pass it as `read_args["match"]`.

- [x] **Step 2: Validate static profile match regex**

In compiler validation, inspect `read.match`. If it is present and does not contain `{{`, compile it with `re.compile`. On `re.error`, emit `INVALID_RESPONSE_MATCHER`.

### Task 4: Docs And Samples

**Files:**
- Modify: `samples/tool-profiles/lab-serial.tools.yaml`
- Modify: `README.md`
- Modify: `docs/design/embedded-sw-tester-detailed-design.md`

- [x] **Step 1: Update sample profile**

Add:

```yaml
read:
  until: "VALUE"
  match: "VALUE:.+"
  extract: "VALUE:(?P<value>.+)"
```

- [x] **Step 2: Document matching rules**

State:

- `until` is substring containment.
- `match` is regex search.
- If both are provided, both must pass.
- `extract` only controls saved value, not pass/fail except when it does not match after read succeeds.

### Task 5: Verification And Commit

- [x] **Step 1: Run full pytest**

Run:

```bash
.venv/bin/python -m pytest
```

Expected: all tests pass.

- [x] **Step 2: Run CLI smoke**

Run:

```bash
.venv/bin/embsw-tester run samples/boot-smoke.yaml --run-id response-matching-smoke --reports-root reports --json
```

Expected: `status` is `passed` and `diagnostics` is empty.

- [ ] **Step 3: Commit and push**

Commit:

```bash
git add README.md docs/design/embedded-sw-tester-detailed-design.md docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase10-serial-response-matching.md samples/tool-profiles/lab-serial.tools.yaml src/embsw_tester/adapters/serial.py src/embsw_tester/devices/command_profiles.py src/embsw_tester/dsl/catalog.py src/embsw_tester/dsl/compiler.py tests/test_serial_adapter.py tests/test_device_command_profiles.py
git commit -m "feat: add serial response matching"
git push -u origin main
```

## Self-Review

- This phase does not require real hardware.
- Existing `until` behavior remains supported.
- Regex `match` is separate from `extract`, keeping pass/fail matching and saved-value extraction understandable.
