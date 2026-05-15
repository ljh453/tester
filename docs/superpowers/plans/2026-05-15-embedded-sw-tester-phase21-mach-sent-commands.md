# Embedded SW Tester Phase 21 Mach SENT Gateway Commands Plan

**Goal:** Add send-side Mach Systems SENT Gateway device commands alongside the existing `sent_usb.read` fast-frame receive path.

**Source notes:**

- The Mach Systems specification uses RS-232/USB frames in the form `STX LEN ID DATA CHKSUM ETX`.
- SENT channel configuration is a 7-byte data structure. A channel must be stopped before writing configuration.
- PC-to-gateway message ids used in this phase:
  - `2` / `12`: write SENT channel 1/2 configuration
  - `21` / `31`: start SENT channel 1/2
  - `22` / `32`: stop SENT channel 1/2
  - `41` / `51`: transmit fast frame on SENT channel 1/2
- The gateway acknowledges these commands with a one-byte response where `1` means OK and `0` means ERR.

## Tasks

- [x] Add RED helper tests for channel config, fast TX payload, command frame, and ACK parsing.
- [x] Add RED runtime tests for `sent_usb.command` config/start/transmit flows.
- [x] Implement Mach SENT command builders and ACK parser.
- [x] Add `sent_usb.command` catalog entry and runtime dispatch.
- [x] Update sample tool profile.
- [x] Update README and detailed design.
- [x] Run focused pytest.
- [x] Run full pytest.
- [x] Run CLI smoke.
- [x] Commit and push.
