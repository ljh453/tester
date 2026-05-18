# Real Tools Smoke Workspace Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a real-lab smoke workspace template that ties Serial, Trace32, INCA, and helper execution paths into one tool profile while preventing accidental execution on machines without connected hardware.

**Architecture:** Keep compile/edit flows permissive. Add a tool profile `execution.requires_real_hardware` guard that is enforced by CLI `run` before any runtime or adapter registry is created. The user must pass `--allow-real-hardware` or set the configured environment variable before the run can start.

---

## File Structure

- Modify `src/embsw_tester/tools/profile.py`: normalize `execution` profile guard fields.
- Modify `src/embsw_tester/cli.py`: add `--allow-real-hardware` and guarded run diagnostics.
- Add `samples/tool-profiles/lab.tools.yaml`: integrated lab hardware template.
- Add `samples/real-tools-smoke.yaml`: real-lab smoke YAML using Serial, Trace32, and INCA commands.
- Modify `tests/test_tool_profile.py`: cover execution guard normalization and sample compile.
- Modify `tests/test_cli.py`: cover guarded run failure and confirmation env success.
- Modify `README.md` and `docs/design/embedded-sw-tester-detailed-design.md`: document the safety gate and usage.

## Tasks

- [x] Add RED tests for `execution.requires_real_hardware` profile normalization.
- [x] Add RED CLI tests for guarded real-hardware run.
- [x] Add RED sample compile test for `samples/real-tools-smoke.yaml`.
- [x] Normalize `execution.requires_real_hardware` and `execution.allow_env`.
- [x] Add `--allow-real-hardware` CLI flag and `REAL_HARDWARE_CONFIRMATION_REQUIRED` diagnostic.
- [x] Add integrated `samples/tool-profiles/lab.tools.yaml`.
- [x] Add `samples/real-tools-smoke.yaml`.
- [x] Update README/design docs.
- [x] Run focused tests.
- [x] Run full verification.
- [x] Commit and push.
