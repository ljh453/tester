# Embedded SW Tester Phase 22 Mach SENT Slow Frame Plan

**Goal:** Add single slow frame transmission support to the Mach Systems SENT Gateway `sent_usb.command` path.

**Source notes:**

- The Mach Systems specification defines single slow frame buffer write commands as message id `42` for SENT channel 1 and `52` for SENT channel 2.
- A single slow frame payload is 5 bytes: slow message id, 16-bit data in Intel byte order, a control byte containing 6-bit received CRC plus slow/enhanced type bits, and a 6-bit calculated CRC byte.
- The gateway acknowledges slow frame buffer writes with a one-byte response where `1` means OK and `0` means ERR.
- Slow message multiplex buffers use message id `43` / `53`; that path is intentionally left for a later phase.

## Tasks

- [x] Confirm slow frame layout and message ids from the Mach Systems PDF.
- [x] Add RED helper tests for the 5-byte slow frame payload and channel 2 command frame.
- [x] Add RED runtime test for `sent_usb.command action: transmit_slow`.
- [x] Implement `build_sent_slow_frame_payload` and `transmit_slow` message id mapping.
- [x] Expose slow payload hex in runtime command outputs.
- [x] Add slow frame arguments to the command catalog.
- [x] Update sample tool profile, README, and detailed design.
- [x] Run focused pytest.
- [x] Run full pytest.
- [x] Run CLI smoke.
- [ ] Commit and push.
