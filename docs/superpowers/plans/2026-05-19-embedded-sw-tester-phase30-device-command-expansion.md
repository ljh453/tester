# Phase 30: Device Command Expansion

**Goal:** Expand device-oriented commands without hard-coding unverified bench-specific behavior.

## Scope

- [x] Add Mach Systems SENT slow message multiplex buffer transmit support.
- [x] Keep VuPower command profile aligned with the currently implemented USB-to-Serial line protocol.
- [x] Add a safe Trace32 command sequence primitive for flash/memory/symbol candidate workflows.
- [x] Document Trace32 flash/memory/symbol candidates as verification targets rather than finalized DSL commands.

## Mach Systems SENT Gateway

The Mach Systems SENT Gateway specification defines slow message TX buffer commands for channel 1 and 2 as message ids `43` and `53`. The 4-byte payload is:

- byte 0 bit 0-4: buffer index
- byte 0 bit 5: buffer enabled
- byte 0 bit 6: enhanced config bit
- byte 0 bit 7: reserved
- byte 1: slow message id
- byte 2-3: 16-bit data, little-endian

Implemented DSL usage:

```yaml
- sent_usb.command:
    device: sent_usb
    action: transmit_slow_buffer
    channel: 1
    buffer_index: 0
    enabled: true
    slow_message_id: 19
    data: 17767
    enhanced_format: true
```

Disabling a buffer only requires `buffer_index` and `enabled: false`.

## Trace32 Candidate Commands

Trace32 flash, memory, and symbol behavior depends heavily on target scripts and project conventions. The executable primitive for Phase 30 is therefore `trace32.command_sequence`; higher-level commands should be promoted only after hardware smoke validation.

Candidate command families to validate:

- Flash setup/load: project-specific `DO` scripts, `FLASH.*` commands, or loader scripts.
- Memory read/write: `Data.List`, `Data.Set`, and target-specific address spaces.
- Symbol lookup/value: symbol address/value commands used by the local Trace32 PRACTICE scripts.
- Run control: `SYStem.Up`, `Break`, `Go`, `Step`, and state query commands.

Example:

```yaml
- trace32.command_sequence:
    commands:
      - "SYStem.Up"
      - "Data.List D:0x1000++0x10"
    fallback: true
    timeout_ms: 3000
    save_as: trace32_responses
```

## VuPower K

No new unverified command names were added in this phase. The current profile remains line-based and covers `APPL`, `SOUR:VOLT`, `SOUR:CURR`, `OUTP:STAT`, measurement queries, identify, reset, and system error query. The next VuPower step should be a bench capture confirming exact line ending, echo behavior, and response text for each action.

## Verification

- `tests/test_mach_sent_gateway.py`
- `tests/test_device_command_profiles.py`
- `tests/test_trace32_adapter.py`
- `tests/test_runtime.py::test_runtime_runs_trace32_command_sequence`
- `tests/test_catalog.py`
