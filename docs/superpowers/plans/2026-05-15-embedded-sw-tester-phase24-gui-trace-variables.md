# GUI Trace and Variables Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Populate the GUI Execution Trace and Variables tabs from Python run JSON.

**Architecture:** Extend the existing `TesterEngineBridge` run result parser with lightweight table models for command events and testcase variables. Expose those models through `MainWorkbenchViewModel`, then bind WPF DataGrid controls to the ViewModel collections.

**Tech Stack:** C# `net8.0`, WPF `net8.0-windows`, existing Python run JSON schema, existing no-NuGet console test harness.

---

## File Structure

- Modify `apps/TesterWorkbench/TesterWorkbench.Core/Engine/EngineProcessResult.cs`: add `EngineRunEvent` and `EngineVariableValue` records, extend `EngineRunResult`.
- Modify `apps/TesterWorkbench/TesterWorkbench.Core/Engine/TesterEngineBridge.cs`: parse `testcase_results[].events[]` and `testcase_results[].variables`.
- Modify `apps/TesterWorkbench/TesterWorkbench.Core/ViewModels/MainWorkbenchViewModel.cs`: expose `ExecutionTrace` and `Variables`.
- Modify `apps/TesterWorkbench/TesterWorkbench.Tests/Program.cs`: add RED assertions for parsed run events and variables.
- Modify `apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml`: replace placeholder text with DataGrids.
- Modify `apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml.cs`: bind the DataGrids in `RefreshView`.
- Modify `README.md`, `docs/design/embedded-sw-tester-detailed-design.md`, and GUI spec docs.

## Tasks

- [x] Add RED test assertions for `EngineRunResult.Events` and `EngineRunResult.Variables`.
- [x] Run GUI core test harness and confirm failure on missing properties.
- [x] Implement event and variable records plus run JSON parsing.
- [x] Add RED ViewModel assertions for `ExecutionTrace` and `Variables`.
- [x] Implement ViewModel passthrough.
- [x] Replace WPF Execution Trace and Variables placeholders with DataGrids.
- [x] Update README/design/spec/plan docs.
- [x] Run GUI core test harness.
- [x] Run WPF build.
- [x] Run Python pytest.
- [x] Commit and push.
