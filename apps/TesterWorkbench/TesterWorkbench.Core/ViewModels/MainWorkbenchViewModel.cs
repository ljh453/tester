using TesterWorkbench.Core.Engine;
using TesterWorkbench.Core.Workspace;

namespace TesterWorkbench.Core.ViewModels;

public sealed class MainWorkbenchViewModel
{
    private readonly WorkspaceScanner _workspaceScanner;
    private readonly TesterEngineBridge _engineBridge;

    public MainWorkbenchViewModel(
        WorkspaceScanner workspaceScanner,
        TesterEngineBridge engineBridge)
    {
        _workspaceScanner = workspaceScanner;
        _engineBridge = engineBridge;
    }

    public string? WorkspacePath { get; private set; }

    public WorkspaceNode? WorkspaceRoot { get; private set; }

    public string? SelectedFilePath { get; private set; }

    public string EditorText { get; private set; } = string.Empty;

    public IReadOnlyList<EngineDiagnostic> Problems { get; private set; } = Array.Empty<EngineDiagnostic>();

    public string RunStatus { get; private set; } = "Idle";

    public string? ReportDirectory { get; private set; }

    public IReadOnlyList<EngineRunEvent> ExecutionTrace { get; private set; } = Array.Empty<EngineRunEvent>();

    public IReadOnlyList<EngineVariableValue> Variables { get; private set; } = Array.Empty<EngineVariableValue>();

    public string ConsoleText { get; private set; } = string.Empty;

    public Task OpenWorkspaceAsync(string workspacePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WorkspacePath = Path.GetFullPath(workspacePath);
        WorkspaceRoot = _workspaceScanner.Scan(WorkspacePath);
        ConsoleText = $"Opened workspace: {WorkspacePath}";
        return Task.CompletedTask;
    }

    public async Task OpenFileAsync(string yamlFilePath, CancellationToken cancellationToken = default)
    {
        SelectedFilePath = Path.GetFullPath(yamlFilePath);
        EditorText = await File.ReadAllTextAsync(SelectedFilePath, cancellationToken);
        ConsoleText = $"Opened file: {SelectedFilePath}";
    }

    public async Task CompileAsync(CancellationToken cancellationToken = default)
    {
        EnsureFileSelected();
        var result = await _engineBridge.CompileAsync(SelectedFilePath!, cancellationToken);
        Problems = result.Diagnostics;
        ConsoleText = string.IsNullOrWhiteSpace(result.StandardError)
            ? $"Compile exited with code {result.ExitCode}."
            : result.StandardError;
    }

    public async Task RunAsync(string? runId = null, CancellationToken cancellationToken = default)
    {
        EnsureFileSelected();
        var effectiveRunId = string.IsNullOrWhiteSpace(runId)
            ? $"gui-run-{DateTimeOffset.Now:yyyyMMdd-HHmmss}"
            : runId;
        var reportsRoot = WorkspacePath is null
            ? Path.Combine(Path.GetDirectoryName(SelectedFilePath!)!, "reports")
            : Path.Combine(WorkspacePath, "reports");

        var result = await _engineBridge.RunAsync(
            SelectedFilePath!,
            effectiveRunId,
            reportsRoot,
            cancellationToken);
        RunStatus = result.Status;
        ReportDirectory = result.ReportDirectory;
        ExecutionTrace = result.Events;
        Variables = result.Variables;
        ConsoleText = string.IsNullOrWhiteSpace(result.StandardError)
            ? $"Run '{effectiveRunId}' exited with status {result.Status}."
            : result.StandardError;
    }

    private void EnsureFileSelected()
    {
        if (string.IsNullOrWhiteSpace(SelectedFilePath))
        {
            throw new InvalidOperationException("No YAML file is selected.");
        }
    }
}
