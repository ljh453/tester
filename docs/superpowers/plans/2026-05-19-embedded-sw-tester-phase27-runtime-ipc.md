# Runtime IPC Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Keep the existing CLI subprocess architecture while making run event streaming, pause, breakpoint, stop, delay, Console, Trace, and Variables behavior deterministic during live execution.

**Architecture:** Python continues to emit one final run JSON document on stdout. Live command events are emitted as stderr JSONL lines prefixed by `__EMBSW_EVENT__ `. The WPF workbench owns a per-run `control.json` file containing cooperative runtime state and active breakpoint lines.

**Runtime Contract:**

- `running`: command execution started.
- `paused`: command execution is waiting before the command body because of breakpoint or user pause.
- `passed`: command completed successfully.
- `failed`: command completed with assertion, DSL, device, or adapter failure.
- `aborted`: user stop request ended execution.

---

## File Structure

- Modify `src/embsw_tester/runtime/runner.py`: add stop-aware runtime control and delay polling.
- Modify `tests/test_runtime.py`: add stop and long-delay interruption regression tests.
- Modify `apps/TesterWorkbench/TesterWorkbench.Core/ViewModels/MainWorkbenchViewModel.cs`: expose Stop command and aborted streaming status.
- Modify `apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml`: add toolbar Stop button.
- Modify `apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml.cs`: wire Stop button to ViewModel.
- Modify `apps/TesterWorkbench/TesterWorkbench.Tests/Program.cs`: add Workbench stop/control-file tests.
- Modify `docs/design/embedded-sw-tester-detailed-design.md`: document the actual subprocess/event/control-file IPC contract.
- Modify `README.md`: update implemented phase list.

## Tasks

- [x] Add RED runtime tests for stop before next command and stop during long delay.
- [x] Add RED Workbench test for Stop writing `stopping` to `control.json`.
- [x] Add RED XAML assertion for Stop toolbar button.
- [x] Add Python runtime `aborted` event/state handling.
- [x] Poll control state during long `delay` commands.
- [x] Stop remaining testcase execution after first aborted testcase.
- [x] Add Workbench Stop command and toolbar handler.
- [x] Surface streamed `aborted` status in the Workbench run status.
- [x] Document stdout/stderr event JSONL/control-file IPC contract.
- [x] Run Python runtime tests.
- [x] Run Workbench core tests.
- [x] Run full Python pytest.
- [x] Run WPF build.
- [x] Commit and push.
