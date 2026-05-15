using TesterWorkbench.Core.Engine;
using TesterWorkbench.Core.ViewModels;
using TesterWorkbench.Core.Workspace;

await RunWorkspaceScannerTest();
await RunEngineBridgeTest();
await RunMainWorkbenchViewModelTest();

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
                {"name": "boot_smoke", "status": "passed", "variables": {"power_ready": true}}
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
              "testcase_results": [],
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
    private readonly Queue<EngineProcessResult> _results;

    public FakeEngineProcessRunner(params EngineProcessResult[] results)
    {
        _results = new Queue<EngineProcessResult>(results);
    }

    public List<EngineProcessCall> Calls { get; } = new();

    public Task<EngineProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        Calls.Add(new EngineProcessCall(fileName, arguments, workingDirectory));
        return Task.FromResult(_results.Dequeue());
    }
}

sealed record EngineProcessCall(
    string FileName,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory);
