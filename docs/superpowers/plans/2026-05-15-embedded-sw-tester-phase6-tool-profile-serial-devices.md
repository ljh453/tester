# Embedded SW Tester Phase 6 Tool Profile Serial Devices Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add tool profile parsing for serial device targets so YAML files can reference stable logical device names for the power supply and Mach Systems SENT-USB interface before protocol details are finalized.

**Architecture:** Add a small `embsw_tester.tools` package that loads profile YAML and validates serial device declarations. The DSL compiler accepts a top-level `tool_profile` reference, loads it relative to the executable YAML file, and stores a normalized `tool_profile_snapshot` in `ResolvedPackage` and report artifacts.

**Tech Stack:** Python 3.9+, PyYAML, pytest.

---

## File Structure

- `src/embsw_tester/tools/__init__.py`: exports profile loader APIs.
- `src/embsw_tester/tools/profile.py`: serial device config dataclass and profile loader/normalizer.
- `src/embsw_tester/dsl/models.py`: add `tool_profile_snapshot` to `ResolvedPackage`.
- `src/embsw_tester/dsl/compiler.py`: load top-level `tool_profile`.
- `tests/test_tool_profile.py`: profile parser and compiler snapshot tests.
- `samples/tool-profiles/lab-serial.tools.yaml`: sample power supply and SENT-USB serial profile.
- `samples/boot-smoke.yaml`: reference the sample profile.
- `README.md`: document serial targets and profile format.

### Task 1: Tool Profile Parser

**Files:**
- Create: `tests/test_tool_profile.py`
- Create: `src/embsw_tester/tools/__init__.py`
- Create: `src/embsw_tester/tools/profile.py`

- [ ] **Step 1: Write failing parser test**

```python
from pathlib import Path

from embsw_tester.tools.profile import load_tool_profile


def test_load_tool_profile_normalizes_serial_devices(tmp_path: Path):
    profile_file = tmp_path / "lab.tools.yaml"
    profile_file.write_text(
        '''
serial:
  devices:
    psu:
      device_type: power_supply
      port: COM3
      baudrate: 9600
      command_profile: pending
    sent_usb:
      device_type: mach_systems_sent_usb
      port: COM4
      baudrate: 115200
      command_profile: sent_usb_line
'''.strip(),
        encoding="utf-8",
    )

    profile = load_tool_profile(profile_file)

    assert profile["serial"]["devices"]["psu"]["device_type"] == "power_supply"
    assert profile["serial"]["devices"]["sent_usb"]["device_type"] == "mach_systems_sent_usb"
```

- [ ] **Step 2: Run and verify failure**

Run: `.venv/bin/python -m pytest tests/test_tool_profile.py -q`

Expected: FAIL with `ModuleNotFoundError: No module named 'embsw_tester.tools'`.

- [ ] **Step 3: Implement profile parser**

Normalize serial devices while preserving extension-friendly metadata.

- [ ] **Step 4: Verify parser test passes**

Run: `.venv/bin/python -m pytest tests/test_tool_profile.py -q`

Expected: parser test passes.

### Task 2: Compiler Snapshot Integration

**Files:**
- Modify: `tests/test_tool_profile.py`
- Modify: `src/embsw_tester/dsl/models.py`
- Modify: `src/embsw_tester/dsl/compiler.py`

- [ ] **Step 1: Write failing compiler test**

Create an executable YAML with `tool_profile: lab.tools.yaml`, compile it, and assert `package.tool_profile_snapshot` contains `psu` and `sent_usb`.

- [ ] **Step 2: Run and verify failure**

Run: `.venv/bin/python -m pytest tests/test_tool_profile.py -q`

Expected: FAIL until compiler loads profile snapshots.

- [ ] **Step 3: Implement compiler integration**

Resolve `tool_profile` relative to the YAML file, load it, and store the normalized snapshot in `ResolvedPackage`.

- [ ] **Step 4: Verify tests pass**

Run: `.venv/bin/python -m pytest tests/test_tool_profile.py -q`

Expected: profile tests pass.

### Task 3: Samples, README, Full Verification

**Files:**
- Create: `samples/tool-profiles/lab-serial.tools.yaml`
- Modify: `samples/boot-smoke.yaml`
- Modify: `README.md`

- [ ] **Step 1: Add sample profile**

Declare:

- `psu` as `power_supply`, command format pending.
- `sent_usb` as `mach_systems_sent_usb`.

- [ ] **Step 2: Update sample YAML**

Reference `tool_profile: tool-profiles/lab-serial.tools.yaml`.

- [ ] **Step 3: Update README**

Document both target devices and note that protocol-specific commands are intentionally deferred.

- [ ] **Step 4: Run full verification**

Run:

```bash
.venv/bin/python -m pytest
.venv/bin/embsw-tester compile samples/boot-smoke.yaml --json
.venv/bin/embsw-tester run samples/boot-smoke.yaml --run-id tool-profile-smoke --reports-root reports --json
```

Expected: tests pass and compile output contains the tool profile snapshot.

## Self-Review

- This plan does not invent power supply command syntax.
- This plan does not implement Mach Systems SENT-USB protocol details.
- Logical device names stay stable so future protocol adapters can evolve behind the profile boundary.
