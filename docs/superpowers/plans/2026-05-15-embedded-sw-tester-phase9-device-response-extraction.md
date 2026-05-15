# Embedded SW Tester Phase 9 Device Response Extraction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let device command profiles extract a meaningful saved value from serial read responses instead of always saving the full raw line.

**Architecture:** Extend `embsw_tester.devices.command_profiles` so a command profile read definition can include an `extract` regex. Runtime still uses the existing `SerialAdapter`; extraction happens after a successful `serial.read` and before `save_as` writes to frame variables. Compiler adds early regex validation for profile-defined extract patterns used by device commands.

**Tech Stack:** Python 3.9+, standard-library `re`, PyYAML, pytest.

---

## File Structure

- `src/embsw_tester/devices/command_profiles.py`: apply optional regex extraction to `serial.read` text.
- `src/embsw_tester/dsl/compiler.py`: validate `read.extract` regex in command profiles referenced by device commands.
- `tests/test_device_command_profiles.py`: cover extracted saved values and invalid regex diagnostics.
- `samples/tool-profiles/lab-serial.tools.yaml`: show an example SENT-USB extraction mapping.
- `README.md`: document `read.extract`.
- `docs/design/embedded-sw-tester-detailed-design.md`: update profile syntax notes.

### Task 1: Failing Tests

**Files:**
- Modify: `tests/test_device_command_profiles.py`

- [x] **Step 1: Add runtime extraction test**

Add a test where `sent_usb.read` receives `VALUE:123`, the profile has `read.extract: "VALUE:(?P<value>\\d+)"`, and `save_as: sent_value` stores `"123"`.

- [x] **Step 2: Add invalid regex diagnostic test**

Add a test where the profile has `read.extract: "VALUE:("` and compile emits `INVALID_RESPONSE_EXTRACTOR`.

- [x] **Step 3: Run focused tests**

Run:

```bash
.venv/bin/python -m pytest tests/test_device_command_profiles.py -q
```

Expected: new tests fail until compiler/runtime extraction support exists.

### Task 2: Runtime Extraction

**Files:**
- Modify: `src/embsw_tester/devices/command_profiles.py`

- [x] **Step 1: Import `re`**

Add `import re`.

- [x] **Step 2: Extract saved value**

After successful `serial.read`, if `read.extract` is present:

- Match it against `read_result.values["text"]`.
- If the regex has a named group `value`, save that group.
- Otherwise, if it has any capturing group, save group 1.
- Otherwise, save the full match.
- Raise `DeviceCommandError` if the pattern does not match.

- [x] **Step 3: Preserve raw response in outputs**

Keep the existing nested serial outputs and add extracted value as top-level `outputs["value"]`.

### Task 3: Compiler Validation

**Files:**
- Modify: `src/embsw_tester/dsl/compiler.py`

- [x] **Step 1: Import `re`**

Add `import re`.

- [x] **Step 2: Validate referenced extract regex**

When `_validate_device_command` finds a command profile definition for a device command, inspect `commands[command.type].read.extract` if present. Compile it with `re.compile`. On `re.error`, emit:

```python
Diagnostic(
    code="INVALID_RESPONSE_EXTRACTOR",
    message=f"Command profile '{profile_name}' command '{command.type}' has invalid read.extract: {exc}.",
    path=command.path,
    source_file=command.source_file,
)
```

### Task 4: Docs And Samples

**Files:**
- Modify: `samples/tool-profiles/lab-serial.tools.yaml`
- Modify: `README.md`
- Modify: `docs/design/embedded-sw-tester-detailed-design.md`

- [x] **Step 1: Update sample profile**

Set:

```yaml
read:
  until: "VALUE"
  extract: "VALUE:(?P<value>.+)"
```

- [x] **Step 2: Update docs**

Document that `extract` is optional and only affects the saved value; raw serial evidence remains intact.

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
.venv/bin/embsw-tester run samples/boot-smoke.yaml --run-id response-extraction-smoke --reports-root reports --json
```

Expected: `status` is `passed` and `diagnostics` is empty.

- [ ] **Step 3: Commit and push**

Commit:

```bash
git add README.md docs/design/embedded-sw-tester-detailed-design.md docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase9-device-response-extraction.md samples/tool-profiles/lab-serial.tools.yaml src/embsw_tester/devices/command_profiles.py src/embsw_tester/dsl/compiler.py tests/test_device_command_profiles.py
git commit -m "feat: add device response extraction"
git push -u origin main
```

## Self-Review

- The feature does not guess the actual Mach Systems SENT-USB protocol.
- Extraction is profile-driven and optional.
- Low-level serial raw logs and nested serial outputs are preserved.
