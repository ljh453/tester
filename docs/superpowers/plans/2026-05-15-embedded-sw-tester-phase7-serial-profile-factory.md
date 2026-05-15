# Embedded SW Tester Phase 7 Serial Profile Factory Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a factory that converts `tool_profile_snapshot.serial.devices` into a configured `SerialAdapter`, while keeping the CLI default in mock mode for hardware-free runs.

**Architecture:** Add serial port settings and a lazy `PySerialPort` implementation behind the existing `SerialPort` protocol. Add `embsw_tester.adapters.serial_factory` to create a `SerialAdapter` or an `AdapterRegistry` from normalized tool profile data. Tests inject `FakeSerialPort` through a `port_factory` so no physical COM port or pyserial install is required.

**Tech Stack:** Python 3.9+, pytest, optional pyserial at runtime only.

---

## File Structure

- `src/embsw_tester/adapters/serial.py`: add `SerialPortSettings` and optional `PySerialPort`.
- `src/embsw_tester/adapters/serial_factory.py`: profile-to-adapter and profile-to-registry factories.
- `src/embsw_tester/adapters/__init__.py`: export new serial factory APIs.
- `tests/test_serial_factory.py`: factory tests using injected fake ports.
- `README.md`: document profile-based adapter construction.

### Task 1: Serial Factory

**Files:**
- Create: `tests/test_serial_factory.py`
- Create: `src/embsw_tester/adapters/serial_factory.py`
- Modify: `src/embsw_tester/adapters/serial.py`
- Modify: `src/embsw_tester/adapters/__init__.py`

- [ ] **Step 1: Write failing factory tests**

```python
from embsw_tester.adapters.serial import FakeSerialPort
from embsw_tester.adapters.serial_factory import create_serial_adapter_from_profile


def test_create_serial_adapter_from_profile_builds_ports_from_profile(tmp_path):
    profile = {
        "serial": {
            "devices": {
                "psu": {"device_type": "power_supply", "port": "COM3", "baudrate": 9600},
                "sent_usb": {"device_type": "mach_systems_sent_usb", "port": "COM4", "baudrate": 115200},
            }
        }
    }
    created = {}

    def port_factory(settings):
        created[settings.logical_name] = settings
        return FakeSerialPort(rx_lines=["OK"])

    adapter = create_serial_adapter_from_profile(profile, tmp_path, port_factory)

    assert created["psu"].system_port == "COM3"
    assert created["sent_usb"].baudrate == 115200
    assert adapter.execute("serial.read", {"port": "sent_usb"}, context).values["text"] == "OK"
```

- [ ] **Step 2: Run and verify failure**

Run: `.venv/bin/python -m pytest tests/test_serial_factory.py -q`

Expected: FAIL with missing `serial_factory`.

- [ ] **Step 3: Implement settings, optional pyserial port, and factories**

Create `SerialPortSettings`, `PySerialPort`, `create_serial_adapter_from_profile`, and `create_adapter_registry_from_tool_profile`.

- [ ] **Step 4: Verify factory tests pass**

Run: `.venv/bin/python -m pytest tests/test_serial_factory.py -q`

Expected: factory tests pass without pyserial.

### Task 2: Documentation And Full Verification

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Update README**

Document that CLI default remains mock, and real serial runs should build an adapter registry from the tool profile snapshot.

- [ ] **Step 2: Run full verification**

Run:

```bash
.venv/bin/python -m pytest
.venv/bin/embsw-tester run samples/boot-smoke.yaml --run-id serial-factory-smoke --reports-root reports --json
```

Expected: all tests pass; CLI smoke still uses default mock adapter.

## Self-Review

- This plan does not require physical COM ports.
- pyserial is imported lazily only when `PySerialPort` is instantiated.
- Power supply command syntax remains pending and is not guessed.
