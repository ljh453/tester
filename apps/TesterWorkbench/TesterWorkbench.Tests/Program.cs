using TesterWorkbench.Core.Engine;
using TesterWorkbench.Core.ViewModels;
using TesterWorkbench.Core.Workspace;

await RunWorkspaceScannerTest();
await RunEngineBridgeTest();
await RunEngineBridgePassesDebugControlArgumentsTest();
await RunMainWorkbenchViewModelTest();
await RunMainWorkbenchViewModelStreamingTest();
await RunMainWorkbenchViewModelDebugControlTest();
await RunMainWorkbenchViewModelUpdatesStatusFromPausedEventTest();
await RunMainWorkbenchViewModelKeepsRunResultWhenRefreshCallbackFailsTest();
await RunMainWorkbenchViewModelShowsLogEventsInConsoleTest();
await RunMainWorkbenchViewModelStreamsLogEventsInConsoleTest();
await RunMainWorkbenchLineNumbersTest();
await RunMainWorkbenchBreakpointEligibilityTest();
await RunYamlExecutionBlockRangeTest();
await RunMainWorkbenchAutoFocusSettingTest();
await RunMainWorkbenchEditorZoomSettingTest();
await RunMainWorkbenchThemeModeSettingTest();
await RunMainWorkbenchViewModelSavesSelectedFileTest();
await RunWorkbenchThemeResolverTest();
await RunWorkbenchGuiModelBuilderTest();
await RunMainWorkbenchViewModelRefreshesGuiModelTest();
await RunWorkbenchCommandCatalogTest();
await RunWorkbenchYamlCommandInserterTest();
await RunWorkbenchYamlCommandMoverTest();
await RunWorkbenchYamlCommandDeleterTest();
await RunMainWorkbenchViewModelInsertsGuiCommandTest();
await RunMainWorkbenchViewModelMovesGuiCommandTest();
await RunMainWorkbenchViewModelDeletesGuiCommandTest();
await RunMainWorkbenchViewModelSelectsGuiCommandRangeTest();
await RunMainWorkbenchViewModelDeletesSelectedGuiCommandsTest();
await RunMainWorkbenchViewModelShowsDragInsertionPreviewTest();

Console.WriteLine("TesterWorkbench core tests passed.");

static Task RunWorkspaceScannerTest()
{
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
    return Task.CompletedTask;
}

static async Task RunEngineBridgeTest()
{
    var runner = new FakeEngineProcessRunner(
        new EngineProcessResult(
            1,
            """
            {
              "diagnostics": [
                {"severity": "error", "code": "YAML_SCHEMA", "message": "Missing testcases"}
              ],
              "testcases": []
            }
            """,
            ""),
        new EngineProcessResult(
            0,
            """
            {
              "run_id": "gui-run",
              "status": "passed",
              "testcase_results": [
                {
                  "name": "boot_smoke",
                  "status": "passed",
                  "variables": {"power_ready": true, "rpm": 1200},
                  "events": [
                    {
                      "testcase": "boot_smoke",
                      "phase": "steps",
                      "command_path": ["testcases", 0, "steps", 1],
                      "command_type": "assert.eq",
                      "status": "passed",
                      "source_file": "/repo/samples/boot-smoke.yaml",
                      "source_line": 12,
                      "local_variables": {"power_ready": true, "rpm": 1200},
                      "outputs": {"passed": true},
                      "error": null
                    },
                    {
                      "testcase": "boot_smoke",
                      "phase": "steps",
                      "command_path": ["testcases", 0, "steps", 2],
                      "command_type": "log.text",
                      "status": "passed",
                      "source_file": "/repo/samples/boot-smoke.yaml",
                      "source_line": 14,
                      "local_variables": {"power_ready": true, "rpm": 1200},
                      "outputs": {"text": "Boot smoke finished"},
                      "error": null
                    }
                  ]
                }
              ],
              "report": {"report_dir": "reports/gui-run"}
            }
            """,
            ""));
    var bridge = new TesterEngineBridge("python", "/repo", runner);

    var compile = await bridge.CompileAsync("/repo/samples/boot-smoke.yaml");
    var run = await bridge.RunAsync("/repo/samples/boot-smoke.yaml", "gui-run", "/repo/reports");

    AssertEqual(1, compile.ExitCode, "compile exit code");
    AssertEqual("YAML_SCHEMA", compile.Diagnostics[0].Code, "compile diagnostic code");
    AssertEqual("Missing testcases", compile.Diagnostics[0].Message, "compile diagnostic message");
    AssertEqual("python", runner.Calls[0].FileName, "compile executable");
    AssertSequence(
        new[] { "-m", "embsw_tester.cli", "compile", "/repo/samples/boot-smoke.yaml", "--json" },
        runner.Calls[0].Arguments,
        "compile args");

    AssertEqual(0, run.ExitCode, "run exit code");
    AssertEqual("passed", run.Status, "run status");
    AssertEqual("reports/gui-run", run.ReportDirectory, "report directory");
    AssertEqual(2, run.Events.Count, "run event count");
    AssertEqual("boot_smoke", run.Events[0].Testcase, "run event testcase");
    AssertEqual("steps", run.Events[0].Phase, "run event phase");
    AssertEqual("assert.eq", run.Events[0].CommandType, "run event command type");
    AssertEqual("passed", run.Events[0].Status, "run event status");
    AssertEqual("testcases/0/steps/1", run.Events[0].CommandPath, "run event command path");
    AssertEqual("/repo/samples/boot-smoke.yaml", run.Events[0].SourceFile, "run event source file");
    AssertEqual(12, run.Events[0].SourceLine, "run event source line");
    AssertTrue(run.Events[0].HasLocalVariables, "run event local variables flag");
    AssertEqual(2, run.Events[0].LocalVariables.Count, "run event local variable count");
    AssertEqual("power_ready", run.Events[0].LocalVariables[0].Name, "run event first local variable name");
    AssertEqual("true", run.Events[0].LocalVariables[0].Value, "run event first local variable value");
    AssertEqual("passed: true", run.Events[0].Detail, "run event detail");
    AssertEqual("Boot smoke finished", run.Events[1].Detail, "log event detail");
    AssertEqual("[boot_smoke/steps L14] Boot smoke finished", run.Events[1].LogText, "log event console text");
    AssertEqual(2, run.Variables.Count, "run variable count");
    AssertEqual("power_ready", run.Variables[0].Name, "first variable name");
    AssertEqual("true", run.Variables[0].Value, "first variable value");
    AssertEqual("rpm", run.Variables[1].Name, "second variable name");
    AssertEqual("1200", run.Variables[1].Value, "second variable value");
    AssertSequence(
        new[] { "-m", "embsw_tester.cli", "run", "/repo/samples/boot-smoke.yaml", "--json", "--run-id", "gui-run", "--reports-root", "/repo/reports" },
        runner.Calls[1].Arguments,
        "run args");
}

static async Task RunEngineBridgePassesDebugControlArgumentsTest()
{
    var runner = new FakeEngineProcessRunner(
        new EngineProcessResult(
            0,
            """
            {
              "run_id": "gui-run",
              "status": "passed",
              "testcase_results": [],
              "report": {"report_dir": "reports/gui-run"}
            }
            """,
            ""));
    var bridge = new TesterEngineBridge("python", "/repo", runner);

    await bridge.RunAsync(
        "/repo/tests/debug.yaml",
        "gui-run",
        "/repo/reports",
        controlFile: "/repo/reports/gui-run/control.json",
        breakpointLines: new[] { 7, 4, 4 });

    AssertSequence(
        new[]
        {
            "-m",
            "embsw_tester.cli",
            "run",
            "/repo/tests/debug.yaml",
            "--json",
            "--run-id",
            "gui-run",
            "--reports-root",
            "/repo/reports",
            "--control-file",
            "/repo/reports/gui-run/control.json",
            "--breakpoint-line",
            "4",
            "--breakpoint-line",
            "7"
        },
        runner.Calls[0].Arguments,
        "debug run args");
}

static async Task RunMainWorkbenchViewModelTest()
{
    var root = TestPaths.CreateWorkspace(
        ("tests/boot-smoke.yaml", "testcases: []"));
    var yamlPath = Path.Combine(root, "tests", "boot-smoke.yaml");
    var runner = new FakeEngineProcessRunner(
        new EngineProcessResult(
            1,
            """
            {
              "diagnostics": [
                {"severity": "error", "code": "YAML_SCHEMA", "message": "Missing testcases"}
              ],
              "testcases": []
            }
            """,
            ""),
        new EngineProcessResult(
            0,
            """
            {
              "run_id": "gui-run",
              "status": "passed",
              "testcase_results": [
                {
                  "name": "boot_smoke",
                  "status": "passed",
                  "variables": {"power_ready": true},
                  "events": [
                    {
                      "testcase": "boot_smoke",
                      "phase": "steps",
                      "command_path": ["testcases", 0, "steps", 0],
                      "command_type": "call",
                      "status": "passed",
                      "source_file": "/repo/tests/boot-smoke.yaml",
                      "source_line": 4,
                      "local_variables": {"rpm": 900},
                      "error": null
                    },
                    {
                      "testcase": "boot_smoke",
                      "phase": "steps",
                      "command_path": ["testcases", 0, "steps", 1],
                      "command_type": "assert.eq",
                      "status": "passed",
                      "source_file": "/repo/tests/boot-smoke.yaml",
                      "source_line": 8,
                      "local_variables": {"power_ready": true},
                      "error": null
                    }
                  ]
                }
              ],
              "report": {"report_dir": "reports/gui-run"}
            }
            """,
            ""));
    var viewModel = new MainWorkbenchViewModel(
        new WorkspaceScanner(),
        new TesterEngineBridge("python", root, runner));

    await viewModel.OpenWorkspaceAsync(root);
    await viewModel.OpenFileAsync(yamlPath);
    await viewModel.CompileAsync();
    await viewModel.RunAsync("gui-run");

    AssertTrue(viewModel.WorkspaceRoot is not null, "workspace root is set");
    AssertEqual(yamlPath, viewModel.SelectedFilePath, "selected file path");
    AssertEqual("testcases: []", viewModel.EditorText, "editor text");
    AssertEqual("YAML_SCHEMA", viewModel.Problems[0].Code, "problem code");
    AssertEqual("passed", viewModel.RunStatus, "run status");
    AssertEqual("reports/gui-run", viewModel.ReportDirectory, "report directory");
    AssertEqual(2, viewModel.ExecutionTrace.Count, "view model trace count");
    AssertEqual(8, viewModel.CurrentLineNumber, "view model current line after run");
    AssertEqual("call", viewModel.ExecutionTrace[0].CommandType, "view model trace command");
    AssertEqual(1, viewModel.Variables.Count, "view model variable count");
    AssertEqual("power_ready", viewModel.Variables[0].Name, "view model variable name");
    AssertEqual("true", viewModel.Variables[0].Value, "view model variable value");

    viewModel.SelectExecutionTraceEvent(viewModel.ExecutionTrace[0]);

    AssertEqual(4, viewModel.CurrentLineNumber, "selected trace current line");
    AssertEqual("Line 4 - call", viewModel.CurrentLocationText, "selected trace current location text");
    AssertEqual(1, viewModel.Variables.Count, "selected trace variable count");
    AssertEqual("rpm", viewModel.Variables[0].Name, "selected trace variable name");
    AssertEqual("900", viewModel.Variables[0].Value, "selected trace variable value");
}

static async Task RunMainWorkbenchViewModelStreamingTest()
{
    var root = TestPaths.CreateWorkspace(
        ("tests/stream.yaml", "testcases:\n  - name: stream_case\n    steps: []"));
    var yamlPath = Path.Combine(root, "tests", "stream.yaml");
    var firstRunningEventJson =
        """
        {
          "testcase": "stream_case",
          "phase": "steps",
          "command_path": ["testcases", 0, "steps", 0],
          "command_type": "set",
          "status": "running",
          "source_file": "/repo/tests/stream.yaml",
          "source_line": 4,
          "local_variables": {},
          "error": null
        }
        """;
    var firstPassedEventJson =
        """
        {
          "testcase": "stream_case",
          "phase": "steps",
          "command_path": ["testcases", 0, "steps", 0],
          "command_type": "set",
          "status": "passed",
          "source_file": "/repo/tests/stream.yaml",
          "source_line": 4,
          "local_variables": {"rpm": 700},
          "error": null
        }
        """;
    var secondRunningEventJson =
        """
        {
          "testcase": "stream_case",
          "phase": "steps",
          "command_path": ["testcases", 0, "steps", 1],
          "command_type": "assert.eq",
          "status": "running",
          "source_file": "/repo/tests/stream.yaml",
          "source_line": 7,
          "local_variables": {"rpm": 700},
          "error": null
        }
        """;
    var secondPassedEventJson =
        """
        {
          "testcase": "stream_case",
          "phase": "steps",
          "command_path": ["testcases", 0, "steps", 1],
          "command_type": "assert.eq",
          "status": "passed",
          "source_file": "/repo/tests/stream.yaml",
          "source_line": 7,
          "local_variables": {"rpm": 700, "rpm_ok": true},
          "error": null
        }
        """;
    var runner = new FakeEngineProcessRunner(
        (
            new EngineProcessResult(
                0,
                """
                {
                  "diagnostics": [],
                  "testcases": []
                }
                """,
                ""),
            Array.Empty<string>()
        ),
        (
            new EngineProcessResult(
                0,
                """
                {
                  "run_id": "gui-run",
                  "status": "passed",
                  "testcase_results": [
                    {
                      "name": "stream_case",
                      "status": "passed",
                      "variables": {"rpm": 700, "rpm_ok": true},
                      "events": []
                    }
                  ],
                  "report": {"report_dir": "reports/gui-run"}
                }
                """,
                ""),
            new[]
            {
                firstRunningEventJson,
                firstPassedEventJson,
                secondRunningEventJson,
                secondPassedEventJson
            }
        ));
    var viewModel = new MainWorkbenchViewModel(
        new WorkspaceScanner(),
        new TesterEngineBridge("python", root, runner));
    var callbackCount = 0;

    await viewModel.OpenWorkspaceAsync(root);
    await viewModel.OpenFileAsync(yamlPath);
    await viewModel.CompileAsync();
    await viewModel.RunAsync("gui-run", () => callbackCount++);

    AssertEqual(5, callbackCount, "streaming callback count");
    AssertEqual(2, viewModel.ExecutionTrace.Count, "streaming trace count");
    AssertEqual(7, viewModel.CurrentLineNumber, "streaming current line");
    AssertEqual(2, viewModel.Variables.Count, "streaming variables count");
    AssertEqual("rpm_ok", viewModel.Variables[1].Name, "streaming second variable name");
    AssertEqual("passed", viewModel.ExecutionTrace[0].Status, "streaming replaced first running event");
    AssertSequence(
        new[]
        {
            "-m",
            "embsw_tester.cli",
            "run",
            yamlPath,
            "--json",
            "--run-id",
            "gui-run",
            "--reports-root",
            Path.Combine(root, "reports"),
            "--events-jsonl",
            "--control-file",
            Path.Combine(root, "reports", "gui-run", "control.json")
        },
        runner.Calls[1].Arguments,
        "streaming run args");
}

static async Task RunMainWorkbenchViewModelDebugControlTest()
{
    var root = TestPaths.CreateWorkspace(
        ("tests/debug.yaml",
            """
            testcases:
              - name: debug_case
                steps:
                  - set:
                      var: rpm
                      value: 700
            """));
    var yamlPath = Path.Combine(root, "tests", "debug.yaml");
    var runner = new FakeEngineProcessRunner(
        new EngineProcessResult(
            0,
            """
            {
              "run_id": "debug-run",
              "status": "passed",
              "testcase_results": [],
              "report": {"report_dir": "reports/debug-run"}
            }
            """,
            ""));
    var viewModel = new MainWorkbenchViewModel(
        new WorkspaceScanner(),
        new TesterEngineBridge("python", root, runner));

    await viewModel.OpenWorkspaceAsync(root);
    await viewModel.OpenFileAsync(yamlPath);
    viewModel.ToggleBreakpointAtLine(4);
    await viewModel.RunAsync("debug-run");

    AssertTrue(viewModel.BreakpointLineNumbers.Contains(4), "view model keeps breakpoint line");
    AssertTrue(
        viewModel.EditorLineNumbersText.Contains(
            $"{MainWorkbenchViewModel.ActiveBreakpointMarker} 4",
            StringComparison.Ordinal),
        "line numbers show breakpoint marker");
    AssertEqual("L4", viewModel.BreakpointsText, "breakpoints summary");
    AssertTrue(
        runner.Calls[0].Arguments.Contains("--control-file"),
        "debug run includes control file");
    AssertTrue(
        runner.Calls[0].Arguments.Contains("--breakpoint-line")
            && runner.Calls[0].Arguments.Contains("4"),
        "debug run includes breakpoint line");

    viewModel.ToggleBreakpointAtLine(4);

    AssertFalse(viewModel.BreakpointLineNumbers.Contains(4), "view model removes breakpoint line");
}

static async Task RunMainWorkbenchViewModelUpdatesStatusFromPausedEventTest()
{
    var root = TestPaths.CreateWorkspace(
        ("tests/pause.yaml", "testcases:\n  - name: pause_case\n    steps: []"));
    var yamlPath = Path.Combine(root, "tests", "pause.yaml");
    var pausedEventJson =
        """
        {
          "testcase": "pause_case",
          "phase": "steps",
          "command_path": ["testcases", 0, "steps", 0],
          "command_type": "set",
          "status": "paused",
          "source_file": "/repo/tests/pause.yaml",
          "source_line": 4,
          "local_variables": {"rpm": 700},
          "outputs": {"reason": "breakpoint"},
          "error": null
        }
        """;
    var runningEventJson =
        """
        {
          "testcase": "pause_case",
          "phase": "steps",
          "command_path": ["testcases", 0, "steps", 0],
          "command_type": "set",
          "status": "running",
          "source_file": "/repo/tests/pause.yaml",
          "source_line": 4,
          "local_variables": {"rpm": 700},
          "error": null
        }
        """;
    var runner = new FakeEngineProcessRunner(
        (
            new EngineProcessResult(
                0,
                """
                {
                  "run_id": "pause-run",
                  "status": "passed",
                  "testcase_results": [],
                  "report": {"report_dir": "reports/pause-run"}
                }
                """,
                ""),
            new[] { pausedEventJson, runningEventJson }
        ));
    var viewModel = new MainWorkbenchViewModel(
        new WorkspaceScanner(),
        new TesterEngineBridge("python", root, runner));
    var statusSnapshots = new List<string>();

    await viewModel.OpenFileAsync(yamlPath);
    await viewModel.RunAsync("pause-run", () => statusSnapshots.Add(viewModel.RunStatus));

    AssertTrue(statusSnapshots.Contains("Paused"), "view model reports paused status from event");
    AssertTrue(statusSnapshots.Contains("Running"), "view model reports running status after resume event");
}

static async Task RunMainWorkbenchViewModelKeepsRunResultWhenRefreshCallbackFailsTest()
{
    var root = TestPaths.CreateWorkspace(
        ("tests/refresh-error.yaml", "testcases:\n  - name: refresh_error_case\n    steps: []"));
    var yamlPath = Path.Combine(root, "tests", "refresh-error.yaml");
    var eventJson =
        """
        {
          "testcase": "refresh_error_case",
          "phase": "steps",
          "command_path": ["testcases", 0, "steps", 0],
          "command_type": "set",
          "status": "passed",
          "source_file": "/repo/tests/refresh-error.yaml",
          "source_line": 4,
          "local_variables": {"rpm": 700},
          "error": null
        }
        """;
    var runner = new FakeEngineProcessRunner(
        (
            new EngineProcessResult(
                0,
                """
                {
                  "run_id": "refresh-error-run",
                  "status": "passed",
                  "testcase_results": [
                    {
                      "name": "refresh_error_case",
                      "status": "passed",
                      "variables": {"rpm": 700},
                      "events": []
                    }
                  ],
                  "report": {"report_dir": "reports/refresh-error-run"}
                }
                """,
                ""),
            new[] { eventJson }
        ));
    var viewModel = new MainWorkbenchViewModel(
        new WorkspaceScanner(),
        new TesterEngineBridge("python", root, runner));

    await viewModel.OpenFileAsync(yamlPath);
    await viewModel.RunAsync(
        "refresh-error-run",
        () => throw new InvalidOperationException("UI refresh failed."));

    AssertEqual("passed", viewModel.RunStatus, "refresh callback failure run status");
    AssertEqual(1, viewModel.ExecutionTrace.Count, "refresh callback failure trace count");
    AssertEqual(1, viewModel.Variables.Count, "refresh callback failure variables count");
    AssertEqual("rpm", viewModel.Variables[0].Name, "refresh callback failure variable name");
}

static async Task RunMainWorkbenchViewModelShowsLogEventsInConsoleTest()
{
    var root = TestPaths.CreateWorkspace(
        ("tests/logs.yaml", "testcases:\n  - name: log_case\n    steps: []"));
    var yamlPath = Path.Combine(root, "tests", "logs.yaml");
    var runner = new FakeEngineProcessRunner(
        new EngineProcessResult(
            0,
            """
            {
              "run_id": "log-run",
              "status": "passed",
              "testcase_results": [
                {
                  "name": "log_case",
                  "status": "passed",
                  "variables": {"rpm": 700},
                  "events": [
                    {
                      "testcase": "log_case",
                      "phase": "steps",
                      "command_path": ["testcases", 0, "steps", 0],
                      "command_type": "log.text",
                      "status": "passed",
                      "source_file": "/repo/tests/logs.yaml",
                      "source_line": 4,
                      "local_variables": {"rpm": 700},
                      "outputs": {"text": "Engine warmed up"},
                      "error": null
                    },
                    {
                      "testcase": "log_case",
                      "phase": "steps",
                      "command_path": ["testcases", 0, "steps", 1],
                      "command_type": "log.value",
                      "status": "passed",
                      "source_file": "/repo/tests/logs.yaml",
                      "source_line": 6,
                      "local_variables": {"rpm": 700},
                      "outputs": {"name": "rpm", "value": 700},
                      "error": null
                    }
                  ]
                }
              ],
              "report": {"report_dir": "reports/log-run"}
            }
            """,
            ""));
    var viewModel = new MainWorkbenchViewModel(
        new WorkspaceScanner(),
        new TesterEngineBridge("python", root, runner));

    await viewModel.OpenFileAsync(yamlPath);
    await viewModel.RunAsync("log-run");

    AssertTrue(viewModel.ConsoleText.Contains("[log_case/steps L4] Engine warmed up"), "console includes log.text");
    AssertTrue(viewModel.ConsoleText.Contains("[log_case/steps L6] rpm = 700"), "console includes log.value");
    AssertEqual("Engine warmed up", viewModel.ExecutionTrace[0].Detail, "log text trace detail");
    AssertEqual("rpm = 700", viewModel.ExecutionTrace[1].Detail, "log value trace detail");
}

static async Task RunMainWorkbenchViewModelStreamsLogEventsInConsoleTest()
{
    var root = TestPaths.CreateWorkspace(
        ("tests/log-stream.yaml", "testcases:\n  - name: log_stream_case\n    steps: []"));
    var yamlPath = Path.Combine(root, "tests", "log-stream.yaml");
    var logEventJson =
        """
        {
          "testcase": "log_stream_case",
          "phase": "steps",
          "command_path": ["testcases", 0, "steps", 0],
          "command_type": "log.text",
          "status": "passed",
          "source_file": "/repo/tests/log-stream.yaml",
          "source_line": 4,
          "local_variables": {"step": "before-final-result"},
          "outputs": {"text": "streamed while running"},
          "error": null
        }
        """;
    var runner = new FakeEngineProcessRunner(
        (
            new EngineProcessResult(
                0,
                """
                {
                  "run_id": "log-stream-run",
                  "status": "passed",
                  "testcase_results": [
                    {
                      "name": "log_stream_case",
                      "status": "passed",
                      "variables": {},
                      "events": []
                    }
                  ],
                  "report": {"report_dir": "reports/log-stream-run"}
                }
                """,
                ""),
            new[] { logEventJson }
        ));
    var viewModel = new MainWorkbenchViewModel(
        new WorkspaceScanner(),
        new TesterEngineBridge("python", root, runner));
    var consoleSnapshots = new List<string>();

    await viewModel.OpenFileAsync(yamlPath);
    await viewModel.RunAsync("log-stream-run", () => consoleSnapshots.Add(viewModel.ConsoleText));

    AssertTrue(
        consoleSnapshots.Any(snapshot => snapshot.Contains("[log_stream_case/steps L4] streamed while running")),
        "console includes streamed log before run completion");
    AssertEqual(
        1,
        consoleSnapshots.Count(snapshot => snapshot.Contains("[log_stream_case/steps L4] streamed while running")),
        "streamed log appears once in callbacks");
}

static async Task RunMainWorkbenchLineNumbersTest()
{
    var root = TestPaths.CreateWorkspace(
        ("tests/line-numbers.yaml", "testcases:\n  - name: line_case\n    steps: []"));
    var yamlPath = Path.Combine(root, "tests", "line-numbers.yaml");
    var viewModel = new MainWorkbenchViewModel(
        new WorkspaceScanner(),
        new TesterEngineBridge(
            "python",
            root,
            new FakeEngineProcessRunner(Array.Empty<EngineProcessResult>())));

    await viewModel.OpenFileAsync(yamlPath);

    AssertEqual(
        string.Join(Environment.NewLine, "1", "2", "3"),
        viewModel.EditorLineNumbersText,
        "editor line numbers");
}

static async Task RunMainWorkbenchBreakpointEligibilityTest()
{
    var root = TestPaths.CreateWorkspace(
        ("tests/breakpoint-eligible.yaml",
            """
            testcases:
              - name: breakpoint_eligible_case
                steps:
                  - delay:
                      ms: 1000
                  - log.text:
                      text: "done"
            """));
    var yamlPath = Path.Combine(root, "tests", "breakpoint-eligible.yaml");
    var viewModel = new MainWorkbenchViewModel(
        new WorkspaceScanner(),
        new TesterEngineBridge(
            "python",
            root,
            new FakeEngineProcessRunner(Array.Empty<EngineProcessResult>())));

    await viewModel.OpenFileAsync(yamlPath);

    AssertTrue(
        viewModel.EditorLineNumbersText.Contains($"{MainWorkbenchViewModel.AvailableBreakpointMarker} 4"),
        "command line shows breakpoint box");
    AssertFalse(
        viewModel.EditorLineNumbersText.Contains($"{MainWorkbenchViewModel.AvailableBreakpointMarker} 5"),
        "argument line does not show breakpoint box");

    viewModel.ToggleBreakpointAtLine(5);

    AssertFalse(viewModel.BreakpointLineNumbers.Contains(5), "argument line cannot become breakpoint");

    viewModel.ToggleBreakpointAtLine(4);

    AssertTrue(viewModel.BreakpointLineNumbers.Contains(4), "command line can become breakpoint");
    AssertTrue(
        viewModel.EditorLineNumbersText.Contains($"{MainWorkbenchViewModel.ActiveBreakpointMarker} 4"),
        "active command line shows active breakpoint marker");
}

static Task RunYamlExecutionBlockRangeTest()
{
    var yamlText =
        """
        steps:
          - delay:
              ms: 1000
          - log.text:
              text: done
        """;

    var delayRange = YamlExecutionBlockRange.Find(yamlText, sourceLineNumber: 2);

    AssertTrue(delayRange is not null, "delay block range is found");
    AssertEqual(1, delayRange!.StartLineIndex, "delay block starts at command line");
    AssertEqual(2, delayRange.EndLineIndex, "delay block includes argument line");

    var logRange = YamlExecutionBlockRange.Find(yamlText, sourceLineNumber: 4);

    AssertTrue(logRange is not null, "log block range is found");
    AssertEqual(3, logRange!.StartLineIndex, "log block starts at command line");
    AssertEqual(4, logRange.EndLineIndex, "log block includes text line");
    return Task.CompletedTask;
}

static Task RunMainWorkbenchAutoFocusSettingTest()
{
    var viewModel = new MainWorkbenchViewModel(
        new WorkspaceScanner(),
        new TesterEngineBridge(
            "python",
            "/repo",
            new FakeEngineProcessRunner(Array.Empty<EngineProcessResult>())));

    AssertTrue(viewModel.AutoFocusExecutionLine, "auto focus execution line defaults on");
    viewModel.SetAutoFocusExecutionLine(false);
    AssertFalse(viewModel.AutoFocusExecutionLine, "auto focus execution line can be disabled");
    viewModel.SetAutoFocusExecutionLine(true);
    AssertTrue(viewModel.AutoFocusExecutionLine, "auto focus execution line can be enabled");
    return Task.CompletedTask;
}

static Task RunMainWorkbenchEditorZoomSettingTest()
{
    var viewModel = new MainWorkbenchViewModel(
        new WorkspaceScanner(),
        new TesterEngineBridge(
            "python",
            "/repo",
            new FakeEngineProcessRunner(Array.Empty<EngineProcessResult>())));

    AssertEqual(13.0, viewModel.EditorFontSize, "editor font size defaults to 13");
    viewModel.ZoomEditorIn();
    AssertEqual(14.0, viewModel.EditorFontSize, "editor zoom in increases font size");
    viewModel.ZoomEditorOut();
    AssertEqual(13.0, viewModel.EditorFontSize, "editor zoom out decreases font size");

    for (var i = 0; i < 20; i++)
    {
        viewModel.ZoomEditorOut();
    }
    AssertEqual(8.0, viewModel.EditorFontSize, "editor zoom out clamps to minimum");

    for (var i = 0; i < 40; i++)
    {
        viewModel.ZoomEditorIn();
    }
    AssertEqual(32.0, viewModel.EditorFontSize, "editor zoom in clamps to maximum");
    return Task.CompletedTask;
}

static Task RunMainWorkbenchThemeModeSettingTest()
{
    var viewModel = new MainWorkbenchViewModel(
        new WorkspaceScanner(),
        new TesterEngineBridge(
            "python",
            "/repo",
            new FakeEngineProcessRunner(Array.Empty<EngineProcessResult>())));

    AssertEqual(WorkbenchThemeMode.Dark, viewModel.ThemeMode, "theme mode defaults to dark");
    viewModel.SetThemeMode(WorkbenchThemeMode.Light);
    AssertEqual(WorkbenchThemeMode.Light, viewModel.ThemeMode, "theme mode can be light");
    viewModel.SetThemeMode(WorkbenchThemeMode.System);
    AssertEqual(WorkbenchThemeMode.System, viewModel.ThemeMode, "theme mode can be system");
    viewModel.SetThemeMode(WorkbenchThemeMode.Dark);
    AssertEqual(WorkbenchThemeMode.Dark, viewModel.ThemeMode, "theme mode can be dark");
    return Task.CompletedTask;
}

static async Task RunMainWorkbenchViewModelSavesSelectedFileTest()
{
    var root = TestPaths.CreateWorkspace(
        ("tests/save-demo.yaml", "testcases: []"));
    var yamlPath = Path.Combine(root, "tests", "save-demo.yaml");
    var viewModel = new MainWorkbenchViewModel(
        new WorkspaceScanner(),
        new TesterEngineBridge(
            "python",
            root,
            new FakeEngineProcessRunner(Array.Empty<EngineProcessResult>())));

    await viewModel.OpenFileAsync(yamlPath);

    AssertFalse(viewModel.IsDirty, "opened file starts clean");
    AssertEqual("Saved.", viewModel.SaveStatusText, "opened file save status");

    viewModel.UpdateEditorText(
        """
        testcases:
          - name: saved_case
            steps:
              - log.text:
                  text: "saved"
        """);

    AssertTrue(viewModel.IsDirty, "editor changes mark file dirty");
    AssertTrue(viewModel.SelectedFileDisplayText.EndsWith(" *", StringComparison.Ordinal), "dirty file display has marker");

    await viewModel.SaveAsync();

    AssertFalse(viewModel.IsDirty, "saved file is clean");
    AssertEqual(viewModel.EditorText, await File.ReadAllTextAsync(yamlPath), "save writes editor text to disk");
    AssertTrue(viewModel.SaveStatusText.StartsWith("Saved ", StringComparison.Ordinal), "save status reports saved file");
    AssertFalse(viewModel.SelectedFileDisplayText.EndsWith(" *", StringComparison.Ordinal), "clean file display removes marker");
}

static Task RunWorkbenchThemeResolverTest()
{
    AssertEqual(
        ResolvedWorkbenchTheme.Light,
        WorkbenchThemeResolver.Resolve(WorkbenchThemeMode.Light, systemPrefersDarkTheme: true),
        "light theme resolves to light");
    AssertEqual(
        ResolvedWorkbenchTheme.Dark,
        WorkbenchThemeResolver.Resolve(WorkbenchThemeMode.Dark, systemPrefersDarkTheme: false),
        "dark theme resolves to dark");
    AssertEqual(
        ResolvedWorkbenchTheme.Light,
        WorkbenchThemeResolver.Resolve(WorkbenchThemeMode.System, systemPrefersDarkTheme: false),
        "system light preference resolves to light");
    AssertEqual(
        ResolvedWorkbenchTheme.Dark,
        WorkbenchThemeResolver.Resolve(WorkbenchThemeMode.System, systemPrefersDarkTheme: true),
        "system dark preference resolves to dark");
    return Task.CompletedTask;
}

static Task RunWorkbenchGuiModelBuilderTest()
{
    var model = WorkbenchGuiModelBuilder.Build(
        """
        testcases:
          - name: gui_case
            description: GUI editor sample
            tags: [gui, sample]
            on_step_failure: continue
            preconditions:
              - log.text:
                  text: "prepare"
            steps:
              - set:
                  var: channels
                  value: [1, 2]
              - for:
                  each: "${channels}"
                  as: channel
                  do:
                    - call:
                        function: add_channel
                    - delay:
                        ms: 1000
              - assert.eq:
                  left: "${ok}"
                  right: true
            postconditions:
              - log.value:
                  name: summary
                  value: "${summary}"
        """);

    AssertEqual(1, model.Testcases.Count, "gui model testcase count");
    var testcase = model.Testcases[0];
    AssertEqual("gui_case", testcase.Name, "gui model testcase name");
    AssertEqual("GUI editor sample", testcase.Description, "gui model testcase description");
    AssertEqual("gui, sample", testcase.TagsText, "gui model tags");
    AssertEqual("continue", testcase.FailurePolicy, "gui model failure policy");
    AssertEqual(3, testcase.Phases.Count, "gui model phase count");

    var steps = testcase.Phases.Single(phase => phase.Name == "Steps");
    AssertEqual(3, steps.Blocks.Count, "gui model root step count");
    AssertEqual("set", steps.Blocks[0].CommandType, "gui model first command type");
    AssertEqual("channels = [1, 2]", steps.Blocks[0].Summary, "gui model set summary");
    AssertEqual("for", steps.Blocks[1].CommandType, "gui model second command type");
    AssertEqual("each ${channels} as channel", steps.Blocks[1].Summary, "gui model for summary");
    AssertEqual(2, steps.Blocks[1].Children.Count, "gui model nested command count");
    AssertEqual("call", steps.Blocks[1].Children[0].CommandType, "gui model nested call type");
    AssertEqual("add_channel(...)", steps.Blocks[1].Children[0].Summary, "gui model call summary");
    AssertEqual("delay", steps.Blocks[1].Children[1].CommandType, "gui model nested delay type");
    AssertEqual("1000 ms", steps.Blocks[1].Children[1].Summary, "gui model delay summary");
    AssertEqual("2", steps.Blocks[1].DisplayIndex, "gui model root display index");
    AssertEqual("2.2", steps.Blocks[1].Children[1].DisplayIndex, "gui model child display index");
    AssertEqual("L13-L20", steps.Blocks[1].LineRangeText, "gui model line range");

    return Task.CompletedTask;
}

static Task RunMainWorkbenchViewModelRefreshesGuiModelTest()
{
    var viewModel = new MainWorkbenchViewModel(
        new WorkspaceScanner(),
        new TesterEngineBridge(
            "python",
            TestPaths.CreateWorkspace(),
            new FakeEngineProcessRunner(Array.Empty<EngineProcessResult>())));

    viewModel.UpdateEditorText(
        """
        testcases:
          - name: initial_case
            steps:
              - delay:
                  ms: 100
        """);

    AssertEqual("initial_case", viewModel.SelectedGuiTestcase?.Name, "initial GUI testcase name");
    AssertEqual("delay", viewModel.SelectedGuiTestcase?.Phases[1].Blocks[0].CommandType, "initial GUI command type");

    viewModel.UpdateEditorText(
        """
        testcases:
          - name: updated_case
            steps:
              - log.text:
                  text: "updated"
        """);

    AssertEqual("updated_case", viewModel.SelectedGuiTestcase?.Name, "updated GUI testcase name");
    AssertEqual("log.text", viewModel.SelectedGuiTestcase?.Phases[1].Blocks[0].CommandType, "updated GUI command type");

    return Task.CompletedTask;
}

static Task RunWorkbenchCommandCatalogTest()
{
    var commandTypes = WorkbenchCommandCatalog.AllCommands
        .Select(command => command.CommandType)
        .ToArray();

    AssertEqual(26, commandTypes.Length, "GUI command catalog command count");
    AssertTrue(commandTypes.Contains("set"), "GUI command catalog includes set");
    AssertTrue(commandTypes.Contains("assert.eq"), "GUI command catalog includes assert.eq");
    AssertTrue(commandTypes.Contains("serial.write_bytes"), "GUI command catalog includes serial.write_bytes");
    AssertTrue(commandTypes.Contains("sent_usb.command"), "GUI command catalog includes sent_usb.command");
    AssertTrue(commandTypes.Contains("power_supply.command"), "GUI command catalog includes power_supply.command");
    AssertTrue(commandTypes.Contains("inca.measure.read"), "GUI command catalog includes inca.measure.read");
    AssertTrue(commandTypes.Contains("canoe.sysvar.set"), "GUI command catalog includes canoe.sysvar.set");
    AssertTrue(commandTypes.Contains("trace32.command"), "GUI command catalog includes trace32.command");
    AssertTrue(
        WorkbenchCommandCatalog.Groups.All(group => group.Commands.Count > 0),
        "GUI command catalog groups are populated");
    return Task.CompletedTask;
}

static Task RunWorkbenchYamlCommandInserterTest()
{
    var yaml =
        """
        testcases:
          - name: insert_case
            preconditions:
              - log.text:
                  text: "prepare"
            steps:
              - set:
                  var: rpm
                  value: 700
            postconditions: []
        """;
    var model = WorkbenchGuiModelBuilder.Build(yaml);
    var testcase = model.Testcases[0];
    var steps = testcase.Phases.Single(phase => phase.YamlName == "steps");
    var delay = WorkbenchCommandCatalog.Find("delay")!;

    var insertedAfterSet = WorkbenchYamlCommandInserter.Insert(
        yaml,
        testcase,
        steps,
        delay,
        steps.Blocks[0]);

    var normalizedAfterSet = insertedAfterSet.Text.Replace("\r\n", "\n", StringComparison.Ordinal);
    AssertTrue(
        normalizedAfterSet.Contains("      - delay:\n          ms: 1000", StringComparison.Ordinal),
        "YAML command insertion adds command after dropped block");
    AssertEqual(10, insertedAfterSet.InsertedLineNumber, "YAML command insertion line number");

    var postconditions = WorkbenchGuiModelBuilder.Build(insertedAfterSet.Text)
        .Testcases[0]
        .Phases.Single(phase => phase.YamlName == "postconditions");
    var logValue = WorkbenchCommandCatalog.Find("log.value")!;

    var insertedIntoInlinePhase = WorkbenchYamlCommandInserter.Insert(
        insertedAfterSet.Text,
        testcase,
        postconditions,
        logValue);

    var normalizedInlinePhase = insertedIntoInlinePhase.Text.Replace("\r\n", "\n", StringComparison.Ordinal);
    AssertTrue(
        normalizedInlinePhase.Contains(
            "    postconditions:\n      - log.value:\n          name: value_name\n          value: \"${value}\"",
            StringComparison.Ordinal),
        "YAML command insertion expands inline empty phase");

    var topInserted = WorkbenchYamlCommandInserter.Insert(
        insertedIntoInlinePhase.Text,
        testcase,
        WorkbenchCommandCatalog.Find("log.text")!,
        new WorkbenchCommandInsertionTarget(
            steps,
            WorkbenchCommandInsertPlacement.BeforeFirstInPhase));
    var normalizedTopInserted = topInserted.Text.Replace("\r\n", "\n", StringComparison.Ordinal);
    AssertTrue(
        normalizedTopInserted.Contains(
            "    steps:\n      - log.text:\n          text: \"message\"\n      - set:",
            StringComparison.Ordinal),
        "YAML command insertion supports first command in phase");
    AssertEqual(7, topInserted.InsertedLineNumber, "YAML top insertion line number");

    var loopYaml =
        """
        testcases:
          - name: loop_case
            steps:
              - for:
                  each: "${channels}"
                  as: channel
                  do:
                    - log.text:
                        text: "inner"
        """;
    var loopModel = WorkbenchGuiModelBuilder.Build(loopYaml);
    var loopSteps = loopModel.Testcases[0].Phases.Single(phase => phase.YamlName == "steps");
    var outerLoop = loopSteps.Blocks[0];
    var nestedForInserted = WorkbenchYamlCommandInserter.Insert(
        loopYaml,
        loopModel.Testcases[0],
        WorkbenchCommandCatalog.Find("for")!,
        new WorkbenchCommandInsertionTarget(
            loopSteps,
            WorkbenchCommandInsertPlacement.InsideCommand,
            outerLoop));
    var normalizedNestedFor = nestedForInserted.Text.Replace("\r\n", "\n", StringComparison.Ordinal);
    AssertTrue(
        normalizedNestedFor.Contains(
            "            - log.text:\n                text: \"inner\"\n            - for:\n                each: \"${items}\"",
            StringComparison.Ordinal),
        "YAML command insertion supports nested for loop body");

    var emptyLoopYaml =
        """
        testcases:
          - name: empty_loop_case
            steps:
              - for:
                  each: "${channels}"
                  as: channel
              - log.text:
                  text: "outer"
        """;
    var emptyLoopModel = WorkbenchGuiModelBuilder.Build(emptyLoopYaml);
    var emptyLoopTestcase = emptyLoopModel.Testcases[0];
    var emptyLoopSteps = emptyLoopTestcase.Phases.Single(phase => phase.YamlName == "steps");
    var emptyLoop = emptyLoopSteps.Blocks[0];

    var insertedIntoEmptyLoop = WorkbenchYamlCommandInserter.Insert(
        emptyLoopYaml,
        emptyLoopTestcase,
        WorkbenchCommandCatalog.Find("log.text")!,
        new WorkbenchCommandInsertionTarget(
            emptyLoopSteps,
            WorkbenchCommandInsertPlacement.InsideCommand,
            emptyLoop));
    var normalizedEmptyLoop = insertedIntoEmptyLoop.Text.Replace("\r\n", "\n", StringComparison.Ordinal);
    AssertTrue(
        normalizedEmptyLoop.Contains(
            "          as: channel\n          do:\n            - log.text:\n                text: \"message\"\n      - log.text:",
            StringComparison.Ordinal),
        "YAML command insertion creates a missing do block before inserting into an empty for loop");
    return Task.CompletedTask;
}

static Task RunMainWorkbenchViewModelInsertsGuiCommandTest()
{
    var viewModel = new MainWorkbenchViewModel(
        new WorkspaceScanner(),
        new TesterEngineBridge(
            "python",
            TestPaths.CreateWorkspace(),
            new FakeEngineProcessRunner(Array.Empty<EngineProcessResult>())));
    viewModel.UpdateEditorText(
        """
        testcases:
          - name: gui_insert_case
            steps:
              - set:
                  var: rpm
                  value: 700
        """);
    var steps = viewModel.SelectedGuiTestcase!.Phases.Single(phase => phase.YamlName == "steps");
    var setBlock = steps.Blocks[0];
    var command = WorkbenchCommandCatalog.Find("assert.eq")!;

    viewModel.InsertGuiCommand(command, steps, setBlock);

    AssertTrue(viewModel.EditorText.Contains("  - assert.eq:"), "view model inserts command into editor text");
    AssertEqual("assert.eq", viewModel.SelectedGuiCommand?.CommandType, "view model selects inserted command");
    AssertEqual(7, viewModel.CurrentLineNumber, "view model focuses inserted command line");
    AssertEqual("Line 7 - assert.eq", viewModel.CurrentLocationText, "view model current location after insert");
    return Task.CompletedTask;
}

static Task RunWorkbenchYamlCommandMoverTest()
{
    var yaml =
        """
        testcases:
          - name: move_case
            steps:
              - set:
                  var: rpm
                  value: 700
              - delay:
                  ms: 1000
              - log.text:
                  text: "done"
        """;
    var model = WorkbenchGuiModelBuilder.Build(yaml);
    var testcase = model.Testcases[0];
    var steps = testcase.Phases.Single(phase => phase.YamlName == "steps");
    var logBlock = steps.Blocks[2];

    var movedToTop = WorkbenchYamlCommandMover.Move(
        yaml,
        testcase,
        logBlock,
        new WorkbenchCommandInsertionTarget(
            steps,
            WorkbenchCommandInsertPlacement.BeforeFirstInPhase));
    var normalizedTopMove = movedToTop.Text.Replace("\r\n", "\n", StringComparison.Ordinal);

    AssertTrue(
        normalizedTopMove.Contains(
            "    steps:\n      - log.text:\n          text: \"done\"\n      - set:",
            StringComparison.Ordinal),
        "YAML command mover supports moving block to top of phase");

    var loopYaml =
        """
        testcases:
          - name: nested_move_case
            steps:
              - for:
                  each: "${channels}"
                  as: channel
                  do:
                    - delay:
                        ms: 100
              - log.text:
                  text: "outer"
        """;
    var loopModel = WorkbenchGuiModelBuilder.Build(loopYaml);
    var loopTestcase = loopModel.Testcases[0];
    var loopSteps = loopTestcase.Phases.Single(phase => phase.YamlName == "steps");
    var outerFor = loopSteps.Blocks[0];
    var outerLog = loopSteps.Blocks[1];

    var movedInsideFor = WorkbenchYamlCommandMover.Move(
        loopYaml,
        loopTestcase,
        outerLog,
        new WorkbenchCommandInsertionTarget(
            loopSteps,
            WorkbenchCommandInsertPlacement.InsideCommand,
            outerFor));
    var normalizedInsideMove = movedInsideFor.Text.Replace("\r\n", "\n", StringComparison.Ordinal);

    AssertTrue(
        normalizedInsideMove.Contains(
            "            - delay:\n                ms: 100\n            - log.text:\n                text: \"outer\"",
            StringComparison.Ordinal),
        "YAML command mover supports moving block inside for loop");

    var emptyLoopYaml =
        """
        testcases:
          - name: empty_loop_move_case
            steps:
              - for:
                  each: "${channels}"
                  as: channel
              - call:
                  function: power_on
        """;
    var emptyLoopModel = WorkbenchGuiModelBuilder.Build(emptyLoopYaml);
    var emptyLoopTestcase = emptyLoopModel.Testcases[0];
    var emptyLoopSteps = emptyLoopTestcase.Phases.Single(phase => phase.YamlName == "steps");
    var emptyLoopFor = emptyLoopSteps.Blocks[0];
    var outerCall = emptyLoopSteps.Blocks[1];

    var movedInsideEmptyFor = WorkbenchYamlCommandMover.Move(
        emptyLoopYaml,
        emptyLoopTestcase,
        outerCall,
        new WorkbenchCommandInsertionTarget(
            emptyLoopSteps,
            WorkbenchCommandInsertPlacement.InsideCommand,
            emptyLoopFor));
    var normalizedEmptyLoopMove = movedInsideEmptyFor.Text.Replace("\r\n", "\n", StringComparison.Ordinal);

    AssertTrue(
        normalizedEmptyLoopMove.Contains(
            "          as: channel\n          do:\n            - call:\n                function: power_on",
            StringComparison.Ordinal),
        "YAML command mover creates missing do block when moving into an empty for loop");
    AssertFalse(
        normalizedEmptyLoopMove.Contains("      - call:\n          function: power_on", StringComparison.Ordinal),
        "YAML command mover removes the outer command after moving into an empty for loop");
    return Task.CompletedTask;
}

static Task RunWorkbenchYamlCommandDeleterTest()
{
    var yaml =
        """
        testcases:
          - name: delete_case
            steps:
              - set:
                  var: rpm
                  value: 700
              - delay:
                  ms: 1000
              - log.text:
                  text: "done"
        """;
    var model = WorkbenchGuiModelBuilder.Build(yaml);
    var testcase = model.Testcases[0];
    var delayBlock = testcase.Phases.Single(phase => phase.YamlName == "steps").Blocks[1];

    var result = WorkbenchYamlCommandDeleter.Delete(yaml, delayBlock);
    var normalized = result.Text.Replace("\r\n", "\n", StringComparison.Ordinal);

    AssertFalse(normalized.Contains("      - delay:", StringComparison.Ordinal), "YAML deleter removes selected command");
    AssertFalse(normalized.Contains("          ms: 1000", StringComparison.Ordinal), "YAML deleter removes command arguments");
    AssertTrue(normalized.Contains("      - set:", StringComparison.Ordinal), "YAML deleter keeps previous command");
    AssertTrue(normalized.Contains("      - log.text:", StringComparison.Ordinal), "YAML deleter keeps next command");
    AssertEqual(7, result.DeletedLineNumber, "YAML deleter reports deleted line");
    return Task.CompletedTask;
}

static Task RunMainWorkbenchViewModelMovesGuiCommandTest()
{
    var viewModel = new MainWorkbenchViewModel(
        new WorkspaceScanner(),
        new TesterEngineBridge(
            "python",
            TestPaths.CreateWorkspace(),
            new FakeEngineProcessRunner(Array.Empty<EngineProcessResult>())));
    viewModel.UpdateEditorText(
        """
        testcases:
          - name: gui_move_case
            steps:
              - set:
                  var: rpm
                  value: 700
              - log.text:
                  text: "done"
        """);
    var steps = viewModel.SelectedGuiTestcase!.Phases.Single(phase => phase.YamlName == "steps");
    var logBlock = steps.Blocks[1];

    viewModel.MoveGuiCommand(
        logBlock,
        new WorkbenchCommandInsertionTarget(
            steps,
            WorkbenchCommandInsertPlacement.BeforeFirstInPhase));

    AssertTrue(
        viewModel.EditorText.Contains("    steps:\n      - log.text:", StringComparison.Ordinal),
        "view model moves command in editor text");
    AssertEqual("log.text", viewModel.SelectedGuiCommand?.CommandType, "view model selects moved command");
    AssertEqual(4, viewModel.CurrentLineNumber, "view model focuses moved command line");
    return Task.CompletedTask;
}

static Task RunMainWorkbenchViewModelDeletesGuiCommandTest()
{
    var viewModel = new MainWorkbenchViewModel(
        new WorkspaceScanner(),
        new TesterEngineBridge(
            "python",
            TestPaths.CreateWorkspace(),
            new FakeEngineProcessRunner(Array.Empty<EngineProcessResult>())));
    viewModel.UpdateEditorText(
        """
        testcases:
          - name: gui_delete_case
            steps:
              - set:
                  var: rpm
                  value: 700
              - delay:
                  ms: 1000
              - log.text:
                  text: "done"
        """);
    var steps = viewModel.SelectedGuiTestcase!.Phases.Single(phase => phase.YamlName == "steps");
    var delayBlock = steps.Blocks[1];

    viewModel.DeleteGuiCommand(delayBlock);

    AssertFalse(viewModel.EditorText.Contains("  - delay:", StringComparison.Ordinal), "view model deletes command text");
    AssertEqual(2, viewModel.SelectedGuiTestcase!.Phases.Single(phase => phase.YamlName == "steps").Blocks.Count, "view model command count after delete");
    AssertEqual("log.text", viewModel.SelectedGuiCommand?.CommandType, "view model selects next command after delete");
    AssertEqual(7, viewModel.CurrentLineNumber, "view model focuses next command line after delete");
    return Task.CompletedTask;
}

static Task RunMainWorkbenchViewModelSelectsGuiCommandRangeTest()
{
    var viewModel = new MainWorkbenchViewModel(
        new WorkspaceScanner(),
        new TesterEngineBridge(
            "python",
            TestPaths.CreateWorkspace(),
            new FakeEngineProcessRunner(Array.Empty<EngineProcessResult>())));
    viewModel.UpdateEditorText(
        """
        testcases:
          - name: gui_range_select_case
            steps:
              - set:
                  var: rpm
                  value: 700
              - delay:
                  ms: 1000
              - log.text:
                  text: "done"
              - assert.eq:
                  left: "${rpm}"
                  right: 700
        """);
    var steps = viewModel.SelectedGuiTestcase!.Phases.Single(phase => phase.YamlName == "steps");

    viewModel.SelectGuiCommandForBulkAction(steps.Blocks[1], replaceSelection: true);
    viewModel.SelectGuiCommandRangeForBulkAction(steps.Blocks[3]);

    AssertEqual(3, viewModel.SelectedGuiCommandCount, "range selection includes dragged-over commands");
    AssertFalse(steps.Blocks[0].IsSelectedForBulkAction, "range selection excludes command before anchor");
    AssertTrue(steps.Blocks[1].IsSelectedForBulkAction, "range selection includes anchor command");
    AssertTrue(steps.Blocks[2].IsSelectedForBulkAction, "range selection includes middle command");
    AssertTrue(steps.Blocks[3].IsSelectedForBulkAction, "range selection includes target command");
    AssertEqual("assert.eq", viewModel.SelectedGuiCommand?.CommandType, "range selection focuses target command");
    return Task.CompletedTask;
}

static Task RunMainWorkbenchViewModelDeletesSelectedGuiCommandsTest()
{
    var viewModel = new MainWorkbenchViewModel(
        new WorkspaceScanner(),
        new TesterEngineBridge(
            "python",
            TestPaths.CreateWorkspace(),
            new FakeEngineProcessRunner(Array.Empty<EngineProcessResult>())));
    viewModel.UpdateEditorText(
        """
        testcases:
          - name: gui_multi_delete_case
            steps:
              - set:
                  var: rpm
                  value: 700
              - delay:
                  ms: 1000
              - log.text:
                  text: "done"
              - assert.eq:
                  left: "${rpm}"
                  right: 700
        """);
    var steps = viewModel.SelectedGuiTestcase!.Phases.Single(phase => phase.YamlName == "steps");
    var delayBlock = steps.Blocks[1];
    var logBlock = steps.Blocks[2];

    viewModel.SelectGuiCommandForBulkAction(delayBlock, replaceSelection: true);
    viewModel.SelectGuiCommandForBulkAction(logBlock, replaceSelection: false);
    viewModel.DeleteSelectedGuiCommands();

    AssertFalse(viewModel.EditorText.Contains("  - delay:", StringComparison.Ordinal), "multi delete removes delay");
    AssertFalse(viewModel.EditorText.Contains("  - log.text:", StringComparison.Ordinal), "multi delete removes log");
    AssertTrue(viewModel.EditorText.Contains("  - set:", StringComparison.Ordinal), "multi delete keeps unselected set");
    AssertTrue(viewModel.EditorText.Contains("  - assert.eq:", StringComparison.Ordinal), "multi delete keeps unselected assert");
    AssertEqual(2, viewModel.SelectedGuiTestcase!.Phases.Single(phase => phase.YamlName == "steps").Blocks.Count, "multi delete command count");
    AssertEqual("assert.eq", viewModel.SelectedGuiCommand?.CommandType, "multi delete selects next remaining command");
    AssertEqual(7, viewModel.CurrentLineNumber, "multi delete focuses next remaining command line");
    AssertEqual(0, viewModel.SelectedGuiCommandCount, "multi delete clears bulk selection");
    return Task.CompletedTask;
}

static Task RunMainWorkbenchViewModelShowsDragInsertionPreviewTest()
{
    var viewModel = new MainWorkbenchViewModel(
        new WorkspaceScanner(),
        new TesterEngineBridge(
            "python",
            TestPaths.CreateWorkspace(),
            new FakeEngineProcessRunner(Array.Empty<EngineProcessResult>())));
    viewModel.UpdateEditorText(
        """
        testcases:
          - name: gui_preview_case
            steps:
              - set:
                  var: rpm
                  value: 700
        """);
    var steps = viewModel.SelectedGuiTestcase!.Phases.Single(phase => phase.YamlName == "steps");
    var setBlock = steps.Blocks[0];
    var command = WorkbenchCommandCatalog.Find("delay")!;

    viewModel.ShowGuiCommandInsertionPreview(command, steps, setBlock);

    AssertTrue(setBlock.IsDragInsertionTarget, "drag preview marks hovered command block");
    AssertTrue(
        setBlock.DragInsertionText.Contains("delay", StringComparison.Ordinal),
        "drag preview block text includes command type");
    AssertFalse(steps.IsDragInsertionTarget, "drag preview does not mark phase when block is target");

    viewModel.ShowGuiCommandInsertionPreview(command, steps);

    AssertFalse(setBlock.IsDragInsertionTarget, "drag preview clears previous command block");
    AssertTrue(steps.IsDragInsertionTarget, "drag preview marks phase append target");
    AssertTrue(
        steps.DragInsertionText.Contains("Steps", StringComparison.Ordinal),
        "drag preview phase text includes phase name");

    viewModel.ClearGuiCommandInsertionPreview();

    AssertFalse(steps.IsDragInsertionTarget, "drag preview clears phase target");
    AssertEqual("", steps.DragInsertionText, "drag preview clears phase text");
    return Task.CompletedTask;
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
    }
}

static void AssertTrue(bool condition, string label)
{
    if (!condition)
    {
        throw new InvalidOperationException($"{label}: expected true.");
    }
}

static void AssertFalse(bool condition, string label)
{
    if (condition)
    {
        throw new InvalidOperationException($"{label}: expected false.");
    }
}

static void AssertSequence(IReadOnlyList<string> expected, IReadOnlyList<string> actual, string label)
{
    if (expected.Count != actual.Count)
    {
        throw new InvalidOperationException($"{label}: expected {expected.Count} items, got {actual.Count}.");
    }

    for (var index = 0; index < expected.Count; index++)
    {
        AssertEqual(expected[index], actual[index], $"{label}[{index}]");
    }
}

static class TestPaths
{
    public static string CreateWorkspace(params (string RelativePath, string Content)[] files)
    {
        var root = Path.Combine(Path.GetTempPath(), "tester-workbench-tests", Guid.NewGuid().ToString("N"), "workspace");
        Directory.CreateDirectory(root);
        foreach (var file in files)
        {
            var path = Path.Combine(root, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, file.Content);
        }
        return root;
    }
}

sealed class FakeEngineProcessRunner : IEngineProcessRunner
{
    private readonly Queue<(EngineProcessResult Result, IReadOnlyList<string> EventJsonLines)> _results;

    public FakeEngineProcessRunner(params EngineProcessResult[] results)
    {
        _results = new Queue<(EngineProcessResult, IReadOnlyList<string>)>(
            results.Select(result => (result, (IReadOnlyList<string>)Array.Empty<string>())));
    }

    public FakeEngineProcessRunner(params (EngineProcessResult Result, IReadOnlyList<string> EventJsonLines)[] results)
    {
        _results = new Queue<(EngineProcessResult, IReadOnlyList<string>)>(results);
    }

    public List<EngineProcessCall> Calls { get; } = new();

    public Task<EngineProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken = default,
        Action<string>? onEventJsonLine = null)
    {
        Calls.Add(new EngineProcessCall(fileName, arguments, workingDirectory));
        var result = _results.Dequeue();
        foreach (var eventJsonLine in result.EventJsonLines)
        {
            onEventJsonLine?.Invoke(eventJsonLine);
        }

        return Task.FromResult(result.Result);
    }
}

sealed record EngineProcessCall(
    string FileName,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory);
