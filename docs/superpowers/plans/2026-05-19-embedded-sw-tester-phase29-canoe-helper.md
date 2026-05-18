# CANoe/CANalyzer COM Helper Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Strengthen the actual Vector CANoe/CANalyzer COM helper path while preserving the existing in-memory adapter contract for tests and non-hardware development.

**Architecture:** Keep the helper boundary as JSON-line stdio RPC. The runtime sends `CanoeBridgeRequest` with command args and top-level `timeout_ms`; the helper owns COM dispatch, waits for measurement state transitions, and returns `CanoeBridgeResponse` with values and duration metadata.

---

## File Structure

- Modify `src/embsw_tester/adapters/canoe_com_helper.py`: use request timeout for measurement start/stop and report `duration_ms`.
- Modify `src/embsw_tester/adapters/canoe_bridge.py`: carry bridge `duration_ms` into `AdapterResult`.
- Modify `samples/real-tools-smoke.yaml`: include CANoe/CANalyzer helper smoke testcase.
- Modify `tests/test_canoe_com_helper.py`: add timeout wait and timeout failure regression tests.
- Modify `tests/test_canoe_bridge_transport.py`: assert duration propagation.
- Modify `tests/test_tool_profile.py`: ensure real-tools smoke includes CANoe helper coverage.
- Modify `README.md` and `docs/design/embedded-sw-tester-detailed-design.md`: document helper timing and smoke usage.

## Tasks

- [x] Add RED tests for measurement start polling and stop timeout.
- [x] Add RED bridge transport duration propagation test.
- [x] Add RED sample compile assertion for CANoe helper smoke.
- [x] Implement timeout-aware measurement start/stop wait in `CanoeComClient`.
- [x] Add `duration_ms` to `CanoeBridgeResponse` and `AdapterResult`.
- [x] Add CANoe/CANalyzer testcase to guarded real-tools smoke sample.
- [x] Update README/design docs.
- [x] Run focused CANoe and sample tests.
- [x] Run full verification.
- [x] Commit and push.
