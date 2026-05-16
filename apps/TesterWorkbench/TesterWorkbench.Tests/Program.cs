using TesterWorkbench.Core.Engine;
using TesterWorkbench.Core.ViewModels;
using TesterWorkbench.Core.Workspace;

await RunWorkspaceScannerTest();
await RunEngineBridgeTest();
await RunMainWorkbenchViewModelTest();
await RunMainWorkbenchViewModelStreamingTest();
await RunMainWorkbenchViewModelKeepsRunResultWhenRefreshCallbackFailsTest();
await RunMainWorkbenchViewModelShowsLogEventsInConsoleTest();
await RunMainWorkbenchViewModelStreamsLogEventsInConsoleTest();
await RunMainWorkbenchLineNumbersTest();
await RunYamlExecutionBlockRangeTest();
await RunMainWorkbenchAutoFocusSettingTest();
await RunMainWorkbenchEditorZoomSettingTest();
await RunMainWorkbenchThemeModeSettingTest();
await RunWorkbenchThemeResolverTest();
await RunWorkbenchGuiModelBuilderTest();
await RunMainWorkbenchViewModelRefreshesGuiModelTest();
await RunWorkbenchCommandCatalogTest();
await RunWorkbenchYamlCommandInserterTest();
await RunMainWorkbenchViewModelInsertsGuiCommandTest();

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
        new[] { "-m", "embsw_tester.cli", "run", yamlPath, "--json", "--run-id", "gui-run", "--reports-root", Path.Combine(root, "reports"), "--events-jsonl" },
        runner.Calls[1].Arguments,
        "streaming run args");
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
