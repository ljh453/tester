# Embedded SW Tester Phase 8 Device Command Profiles Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a device command profile layer so semantic commands such as `sent_usb.read` can be mapped to profile-defined serial TX/RX sequences, while pending power supply protocols produce clear compile diagnostics.

**Architecture:** Extend command catalog with device-category commands. Add `embsw_tester.devices.command_profiles` to resolve a command's logical device, command profile, and serial sequence from the tool profile snapshot. Runtime dispatches device commands through the existing serial adapter registry. Compiler performs semantic checks for pending command profiles before execution.

**Tech Stack:** Python 3.9+, PyYAML, pytest.

---

## File Structure

- `src/embsw_tester/devices/__init__.py`: exports device command helpers.
- `src/embsw_tester/devices/command_profiles.py`: command profile resolver and executor.
- `src/embsw_tester/dsl/catalog.py`: add `sent_usb.read` and `power_supply.command`.
- `src/embsw_tester/dsl/compiler.py`: add pending command profile diagnostics.
- `src/embsw_tester/runtime/runner.py`: route device-category commands through command profiles.
- `tests/test_device_command_profiles.py`: semantic command runtime and pending profile diagnostics.
- `samples/tool-profiles/lab-serial.tools.yaml`: include an example SENT-USB command profile mapping.
- `README.md`: document semantic device command profile behavior.

### Task 1: Device Command Tests

**Files:**
- Create: `tests/test_device_command_profiles.py`

- [x] **Step 1: Write failing SENT-USB runtime test**

Create a profile with:

```yaml
serial:
  devices:
    sent_usb:
      device_type: mach_systems_sent_usb
      port: COM4
      command_profile: sent_usb_line
command_profiles:
  sent_usb_line:
    commands:
      sent_usb.read:
        write: "READ SENT {{ channel }}"
        read:
          until: "VALUE"
```

Run YAML:

```yaml
- sent_usb.read:
    device: sent_usb
    channel: 1
    save_as: sent_value
```

Assert the runtime writes `READ SENT 1`, reads `VALUE:123`, and stores `sent_value`.

- [x] **Step 2: Write failing pending power supply diagnostic test**

Compile YAML with `power_supply.command` against a `psu` device using `command_profile: pending` and assert diagnostic code `PENDING_COMMAND_PROFILE`.

- [x] **Step 3: Run and verify failure**

Run: `.venv/bin/python -m pytest tests/test_device_command_profiles.py -q`

Expected: FAIL until catalog/compiler/runtime support exists.

### Task 2: Implementation

**Files:**
- Modify/Create files listed above.

- [x] **Step 1: Add catalog entries**

Add `sent_usb.read` and `power_supply.command` as `category="device"` commands.

- [x] **Step 2: Add compiler diagnostics**

Find device-category commands, resolve their `device` against `tool_profile_snapshot.serial.devices`, and emit:

- `UNKNOWN_DEVICE` if missing.
- `PENDING_COMMAND_PROFILE` if `command_profile` is `pending`.
- `UNKNOWN_COMMAND_PROFILE` if profile is referenced but not declared for non-pending commands.

- [x] **Step 3: Add runtime execution**

Resolve command profile, render `write` template using command args, call `serial.write`, optionally call `serial.read`, and apply `save_as`.

- [x] **Step 4: Verify tests pass**

Run: `.venv/bin/python -m pytest tests/test_device_command_profiles.py -q`

Expected: tests pass.

### Task 3: Docs And Full Verification

**Files:**
- Modify: `README.md`
- Modify: `samples/tool-profiles/lab-serial.tools.yaml`

- [x] **Step 1: Document SENT-USB profile mapping**

State that the sample command profile is a configurable placeholder and should be adjusted to the confirmed Mach Systems command syntax.

- [x] **Step 2: Run full verification**

Run:

```bash
.venv/bin/python -m pytest
.venv/bin/embsw-tester run samples/boot-smoke.yaml --run-id device-profile-smoke --reports-root reports --json
```

Expected: all tests pass and existing sample still runs.

## Self-Review

- This plan does not guess the power supply command syntax.
- SENT-USB command text is profile-driven, not hard-coded.
- Existing low-level `serial.write/read` remains available.
