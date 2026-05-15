# Embedded SW Tester Phase 20 VuPower K Power Supply Protocol Plan

**Goal:** Implement `power_supply.command` for VuPower K Series USB-to-Serial power supplies using the Korean USB manual Ver3.2.

**Source notes:**

- The manual states that USB communication is USB-to-Serial.
- Commands are case-insensitive and are sent one at a time.
- Command and first parameter are separated by a space; parameters are separated by commas.
- Command termination is `<new line>` / line feed.
- Initial command scope:
  - `SOUR:VOLT`, `SOUR:CURR`, `APPL`
  - `OUTP:STAT`, `OUTP:STAT?`
  - `MEAS:VOLT?`, `MEAS:VOLTA?`, `MEAS:CURR?`, `MEAS:CURRA?`
  - `*IDN?`, `*RST`, `SYST:ERR?`

## Tasks

- [x] Add RED tests for VuPower command formatting and response parsing.
- [x] Add RED runtime tests for `power_supply.command` apply/output/measurement flows.
- [x] Implement `embsw_tester.devices.vupower_k` formatter/parser.
- [x] Add `protocol: vupower_k_usb` branch to device command execution.
- [x] Update command catalog, sample tool profile, and common power sequence.
- [x] Update README and detailed design.
- [x] Run focused pytest.
- [x] Run full pytest.
- [x] Run CLI smoke.
- [ ] Commit and push.
