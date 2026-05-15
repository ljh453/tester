# Embedded SW Tester Phase 19 Mach SENT Gateway Protocol Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the placeholder Mach Systems SENT-USB text command profile with a binary SENT Gateway protocol reader for `sent_usb.read`.

**Architecture:** Add a focused `mach_sent_gateway` device helper for STX/LEN/ID/DATA/CHKSUM/ETX frames and SENT fast frame parsing. Extend `SerialAdapter` with byte read/write primitives that still expose JSON-safe hex values. Keep the generic profile-template path intact, and add a `protocol: mach_sent_gateway` path for `sent_usb.read`.

**Tech Stack:** Python 3.9+, pytest, Mach Systems SENT Gateway Communication Protocol Specification latest PDF.

---

## Source Notes

Protocol facts used from the Mach Systems PDF:

- RS-232/USB example frame for Read Serial Number: `02 01 5A 5B 03`.
- Gateway response example: `02 05 5A FF FF FF FE 5A 03`.
- PC-to-device messages include message ids 1/11 for channel config, 21/31 start, 22/32 stop, 41/51 transmit fast frame, and 90 read serial number.
- Gateway-to-PC messages include 100/200 for SENT channel 1/2 fast frames, 101/201 for slow frames, 102/202 for fast errors, 103/203 for slow errors, 250 boot up, and 255 protocol error.
- SENT fast frame payload follows the SENT Data Frame layout: status nibble, data nibble count, data nibbles, received CRC, and calculated CRC.

## File Structure

- `src/embsw_tester/devices/mach_sent_gateway.py`: binary frame encode/decode and SENT fast frame parser.
- `src/embsw_tester/devices/command_profiles.py`: add `protocol: mach_sent_gateway` execution path for `sent_usb.read`.
- `src/embsw_tester/adapters/serial.py`: add byte read/write support.
- `src/embsw_tester/dsl/catalog.py`: expose low-level byte serial commands as adapter commands.
- `samples/tool-profiles/lab-serial.tools.yaml`: change SENT-USB command profile to binary gateway protocol.
- `tests/test_mach_sent_gateway.py`: protocol helper tests.
- `tests/test_serial_adapter.py`: byte serial adapter tests.
- `tests/test_device_command_profiles.py`: runtime `sent_usb.read` protocol test.
- `README.md`: document the Mach Systems source and profile format.
- `docs/design/embedded-sw-tester-detailed-design.md`: update SENT-USB protocol notes.

### Task 1: Failing Tests

**Files:**
- Create: `tests/test_mach_sent_gateway.py`
- Modify: `tests/test_serial_adapter.py`
- Modify: `tests/test_device_command_profiles.py`

- [x] **Step 1: Add gateway frame helper tests**

Assert:

- `encode_gateway_frame(0x5A)` returns bytes from hex `02015A5B03`.
- Parsing `02055AFFFFFFFE5A03` returns message id `0x5A` and data bytes `FF FF FF FE`.
- Parsing a frame with a bad checksum raises `MachSentGatewayError`.

- [x] **Step 2: Add SENT fast frame parser test**

Build a gateway frame with message id `100` and SENT fast payload:

```text
63 21 43 65 BA
```

Assert:

- channel is `1`
- status nibble is `3`
- data nibble count is `6`
- data nibbles are `[1, 2, 3, 4, 5, 6]`
- crc is `10`
- crc_calculated is `11`

- [x] **Step 3: Add serial byte adapter test**

Use `FakeSerialPort(rx_bytes=[bytes.fromhex("02015A5B03")])`, execute `serial.read_bytes` with count `5`, and assert `data_hex == "02015A5B03"`.

- [x] **Step 4: Add runtime SENT-USB protocol test**

Create a tool profile where `sent_usb` uses `command_profile: mach_sent_gateway` and command definition:

```yaml
sent_usb.read:
  protocol: mach_sent_gateway
```

Feed a SENT channel 1 fast gateway frame through `FakeSerialPort(rx_bytes=[frame])`. Execute `sent_usb.read` with `channel: 1` and assert saved value contains parsed data nibbles.

- [x] **Step 5: Run focused tests and verify RED**

Run:

```bash
.venv/bin/python -m pytest tests/test_mach_sent_gateway.py tests/test_serial_adapter.py tests/test_device_command_profiles.py -q
```

Expected: failures because helper module, byte serial commands, and protocol path do not exist yet.

### Task 2: Protocol Helpers

**Files:**
- Create: `src/embsw_tester/devices/mach_sent_gateway.py`

- [x] **Step 1: Implement gateway frame helpers**

Implement:

```python
STX = 0x02
ETX = 0x03

def encode_gateway_frame(message_id: int, data: bytes = b"") -> bytes: ...
def parse_gateway_frame(frame: bytes) -> GatewayFrame: ...
```

Checksum is `(length + message_id + sum(data)) & 0xFF`, where `length == 1 + len(data)`.

- [x] **Step 2: Implement SENT fast frame parser**

Implement:

```python
def parse_sent_fast_frame(frame: GatewayFrame) -> dict: ...
```

Support message id `100` for channel 1 and `200` for channel 2.

### Task 3: Serial Byte Commands

**Files:**
- Modify: `src/embsw_tester/adapters/serial.py`
- Modify: `src/embsw_tester/dsl/catalog.py`

- [x] **Step 1: Extend `SerialPort` protocol and implementations**

Add:

```python
write_bytes(self, payload: bytes) -> int
read_bytes(self, count: int, timeout_ms: int) -> bytes
```

Implement for `PySerialPort` and `FakeSerialPort`.

- [x] **Step 2: Add `serial.write_bytes` and `serial.read_bytes` commands**

`serial.write_bytes` accepts `payload_hex`, writes bytes, and records `TX_HEX`.
`serial.read_bytes` accepts `count`, reads exactly that many bytes, and records `RX_HEX`.

### Task 4: Device Protocol Wiring

**Files:**
- Modify: `src/embsw_tester/devices/command_profiles.py`
- Modify: `samples/tool-profiles/lab-serial.tools.yaml`

- [x] **Step 1: Branch on `protocol: mach_sent_gateway`**

When a device command definition has `protocol: mach_sent_gateway`, route `sent_usb.read` to a binary frame reader.

- [x] **Step 2: Read and parse SENT fast frame**

Read STX, LEN, and the remaining bytes using `serial.read_bytes`. Parse the gateway frame and match expected message id from `channel`.

- [x] **Step 3: Return parsed frame output**

Return output fields:

- `protocol`
- `channel`
- `frame`
- `value`
- nested low-level serial byte read steps

- [x] **Step 4: Update sample profile**

Set `sent_usb.command_profile` to `mach_sent_gateway` and define `sent_usb.read.protocol: mach_sent_gateway`.

### Task 5: Docs And Verification

**Files:**
- Modify: `README.md`
- Modify: `docs/design/embedded-sw-tester-detailed-design.md`
- Modify: `docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase19-mach-sent-gateway-protocol.md`

- [x] **Step 1: Document protocol source and profile**

Mention the Mach Systems PDF and the implemented initial scope: SENT fast frame receive only.

- [x] **Step 2: Run focused tests**

Run:

```bash
.venv/bin/python -m pytest tests/test_mach_sent_gateway.py tests/test_serial_adapter.py tests/test_device_command_profiles.py -q
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
.venv/bin/embsw-tester run samples/boot-smoke.yaml --run-id mach-sent-gateway-smoke --reports-root reports --json
```

Expected: default mock-backed run still returns `status: passed` and `diagnostics: []`.

- [ ] **Step 5: Commit and push**

Commit:

```bash
git add README.md docs/design/embedded-sw-tester-detailed-design.md docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase19-mach-sent-gateway-protocol.md samples/tool-profiles/lab-serial.tools.yaml src/embsw_tester/adapters/serial.py src/embsw_tester/devices/mach_sent_gateway.py src/embsw_tester/devices/command_profiles.py src/embsw_tester/dsl/catalog.py tests/test_mach_sent_gateway.py tests/test_serial_adapter.py tests/test_device_command_profiles.py
git commit -m "feat: add mach sent gateway protocol"
git push -u origin main
```

## Self-Review

- The implementation covers receive-side SENT fast frames only.
- Existing text-based command profile execution remains compatible.
- Binary bytes never leak into JSON outputs; hex strings and parsed dicts are used.
