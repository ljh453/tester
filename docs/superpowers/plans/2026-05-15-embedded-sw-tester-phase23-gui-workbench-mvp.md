# GUI Workbench MVP Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first Windows GUI workbench shell around the existing Python engine.

**Architecture:** Add a testable `.NET` core library for workspace scanning, engine subprocess command construction, JSON result parsing, and the main workbench view model. Add a thin WPF project that binds to the core library. Keep the Python engine as a process boundary for now.

**Tech Stack:** C# `net8.0` core/test projects, C# `net8.0-windows` WPF shell, existing Python CLI, no external NuGet packages.

---

## File Structure

- Create `apps/TesterWorkbench/TesterWorkbench.Core/TesterWorkbench.Core.csproj`: testable core library.
- Create `apps/TesterWorkbench/TesterWorkbench.Core/Workspace/WorkspaceNode.cs`: immutable workspace tree node.
- Create `apps/TesterWorkbench/TesterWorkbench.Core/Workspace/WorkspaceScanner.cs`: scans workspace folders for YAML and report files.
- Create `apps/TesterWorkbench/TesterWorkbench.Core/Engine/EngineProcessResult.cs`: subprocess result DTO.
- Create `apps/TesterWorkbench/TesterWorkbench.Core/Engine/IEngineProcessRunner.cs`: process runner abstraction.
- Create `apps/TesterWorkbench/TesterWorkbench.Core/Engine/ProcessEngineRunner.cs`: real process runner.
- Create `apps/TesterWorkbench/TesterWorkbench.Core/Engine/TesterEngineBridge.cs`: builds compile/run commands and parses JSON output.
- Create `apps/TesterWorkbench/TesterWorkbench.Core/ViewModels/MainWorkbenchViewModel.cs`: UI state and commands without WPF dependencies.
- Create `apps/TesterWorkbench/TesterWorkbench.Tests/TesterWorkbench.Tests.csproj`: no-NuGet console test harness.
- Create `apps/TesterWorkbench/TesterWorkbench.Tests/Program.cs`: focused core tests.
- Create `apps/TesterWorkbench/TesterWorkbench/TesterWorkbench.csproj`: WPF shell project.
- Create `apps/TesterWorkbench/TesterWorkbench/App.xaml`, `App.xaml.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs`: fixed split workbench shell.
- Modify `README.md`: document GUI project and verification commands.
- Modify `docs/design/embedded-sw-tester-detailed-design.md`: record Phase 23 GUI MVP implementation shape.

### Task 1: Project Skeleton and RED Test Harness

**Files:**
- Create: `apps/TesterWorkbench/TesterWorkbench.Core/TesterWorkbench.Core.csproj`
- Create: `apps/TesterWorkbench/TesterWorkbench.Tests/TesterWorkbench.Tests.csproj`
- Create: `apps/TesterWorkbench/TesterWorkbench.Tests/Program.cs`

- [x] **Step 1: Write the failing test harness**

Create `Program.cs` with tests that reference `WorkspaceScanner`, which does not exist yet:

```csharp
using TesterWorkbench.Core.Workspace;

var root = TestPaths.CreateWorkspace(
    ("tests/boot-smoke.yaml", "testcases: []"),
    ("libs/common.yaml", "functions: {}"),
    ("tool-profiles/lab.tools.yaml", "serial: {}"),
    ("reports/run-1/summary.html", "<html></html>"),
    (".hidden/ignore.yaml", "ignored: true"));

var scanner = new WorkspaceScanner();
var tree = scanner.Scan(root);

AssertEqual("workspace", tree.Name, "root name");
AssertTrue(tree.Children.Any(child => child.Name == "tests"), "tests folder exists");
AssertTrue(tree.Flatten().Any(node => node.RelativePath == "tests/boot-smoke.yaml"), "test YAML is included");
AssertFalse(tree.Flatten().Any(node => node.RelativePath == ".hidden/ignore.yaml"), "hidden folder is ignored");

Console.WriteLine("TesterWorkbench core tests passed.");
```

- [x] **Step 2: Run the test harness and verify RED**

Run:

```bash
env DOTNET_CLI_HOME=/Users/jhlee/Documents/Tester_55/.dotnet-home DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 dotnet run --project apps/TesterWorkbench/TesterWorkbench.Tests/TesterWorkbench.Tests.csproj
```

Expected: build fails because `TesterWorkbench.Core.Workspace` is not defined.

### Task 2: Workspace Scanner

**Files:**
- Create: `apps/TesterWorkbench/TesterWorkbench.Core/Workspace/WorkspaceNode.cs`
- Create: `apps/TesterWorkbench/TesterWorkbench.Core/Workspace/WorkspaceScanner.cs`
- Modify: `apps/TesterWorkbench/TesterWorkbench.Tests/Program.cs`

- [x] **Step 1: Implement minimal scanner**

Create `WorkspaceNode` with `Name`, `FullPath`, `RelativePath`, `Kind`, `Children`, and `Flatten()`. Create `WorkspaceScanner.Scan(string workspacePath)` that recursively includes directories and files ending in `.yaml`, `.yml`, `.json`, `.html`, ignores hidden folders, `.venv`, `.git`, `.pytest_cache`, `.dotnet-home`, and `__pycache__`, and returns children sorted directories first then name.

- [x] **Step 2: Run tests and verify GREEN**

Run the same `dotnet run --project ...Tests.csproj` command.

Expected: tests pass and print `TesterWorkbench core tests passed.`

### Task 3: Engine Bridge

**Files:**
- Create: `apps/TesterWorkbench/TesterWorkbench.Core/Engine/EngineProcessResult.cs`
- Create: `apps/TesterWorkbench/TesterWorkbench.Core/Engine/IEngineProcessRunner.cs`
- Create: `apps/TesterWorkbench/TesterWorkbench.Core/Engine/ProcessEngineRunner.cs`
- Create: `apps/TesterWorkbench/TesterWorkbench.Core/Engine/TesterEngineBridge.cs`
- Modify: `apps/TesterWorkbench/TesterWorkbench.Tests/Program.cs`

- [x] **Step 1: Add RED tests for compile and run command handling**

Add a fake runner test that asserts compile uses `python -m embsw_tester.cli compile <yaml> --json`, run uses `python -m embsw_tester.cli run <yaml> --json --run-id <id> --reports-root <root>`, diagnostics are parsed from compile JSON, and report path is parsed from run JSON.

- [x] **Step 2: Run tests and verify RED**

Expected: build fails because engine bridge types do not exist.

- [x] **Step 3: Implement engine bridge**

Implement `TesterEngineBridge.CompileAsync` and `TesterEngineBridge.RunAsync`. Use `System.Text.Json` to parse stdout. Return strongly typed results with `ExitCode`, `Diagnostics`, `Status`, `ReportDirectory`, `StandardError`, and `RawJson`.

- [x] **Step 4: Run tests and verify GREEN**

Expected: test harness passes.

### Task 4: Main Workbench ViewModel

**Files:**
- Create: `apps/TesterWorkbench/TesterWorkbench.Core/ViewModels/MainWorkbenchViewModel.cs`
- Modify: `apps/TesterWorkbench/TesterWorkbench.Tests/Program.cs`

- [x] **Step 1: Add RED ViewModel test**

Test that opening a workspace populates `WorkspaceRoot`, opening a YAML file sets `SelectedFilePath` and `EditorText`, compile populates `Problems`, and run sets `RunStatus` and `ReportDirectory`.

- [x] **Step 2: Run tests and verify RED**

Expected: build fails because `MainWorkbenchViewModel` does not exist.

- [x] **Step 3: Implement ViewModel**

Implement a dependency-injected ViewModel that accepts `WorkspaceScanner` and `TesterEngineBridge`. Keep it WPF-free and expose async methods `OpenWorkspaceAsync`, `OpenFileAsync`, `CompileAsync`, and `RunAsync`.

- [x] **Step 4: Run tests and verify GREEN**

Expected: test harness passes.

### Task 5: WPF Shell

**Files:**
- Create: `apps/TesterWorkbench/TesterWorkbench/TesterWorkbench.csproj`
- Create: `apps/TesterWorkbench/TesterWorkbench/App.xaml`
- Create: `apps/TesterWorkbench/TesterWorkbench/App.xaml.cs`
- Create: `apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml`
- Create: `apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml.cs`

- [x] **Step 1: Add WPF project**

Create a `net8.0-windows` WPF project with `UseWPF`, `EnableWindowsTargeting`, and a project reference to `TesterWorkbench.Core`.

- [x] **Step 2: Add fixed workbench layout**

Create a fixed split layout: toolbar, left Project Explorer, center YAML editor, right properties placeholder, bottom Problems/Console/Execution Trace/Variables/Report tabs.

- [x] **Step 3: Wire minimal actions**

Implement buttons for open workspace, compile, and run. Use `MainWorkbenchViewModel`; use standard dialogs and display report path/status.

- [x] **Step 4: Build what the host can build**

Run:

```bash
env DOTNET_CLI_HOME=/Users/jhlee/Documents/Tester_55/.dotnet-home DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 dotnet build apps/TesterWorkbench/TesterWorkbench.Core/TesterWorkbench.Core.csproj
env DOTNET_CLI_HOME=/Users/jhlee/Documents/Tester_55/.dotnet-home DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 dotnet run --project apps/TesterWorkbench/TesterWorkbench.Tests/TesterWorkbench.Tests.csproj
```

On macOS, WPF shell build may fail if the Windows Desktop reference pack is unavailable. If it fails, record that limitation in the final response and keep the WPF project source committed for Windows verification.

### Task 6: Docs, Verification, Commit, Push

**Files:**
- Modify: `README.md`
- Modify: `docs/design/embedded-sw-tester-detailed-design.md`
- Modify: `docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase23-gui-workbench-mvp.md`

- [x] **Step 1: Update docs**

Document GUI project layout, macOS verification commands, and Windows run command.

- [x] **Step 2: Run verification**

Run:

```bash
env DOTNET_CLI_HOME=/Users/jhlee/Documents/Tester_55/.dotnet-home DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 dotnet run --project apps/TesterWorkbench/TesterWorkbench.Tests/TesterWorkbench.Tests.csproj
.venv/bin/python -m pytest -q
```

- [x] **Step 3: Commit and push**

Commit:

```bash
git add .gitignore README.md docs/design/embedded-sw-tester-detailed-design.md docs/superpowers/plans/2026-05-15-embedded-sw-tester-phase23-gui-workbench-mvp.md apps/TesterWorkbench
git commit -m "feat: add gui workbench mvp skeleton"
git push -u origin main
```
