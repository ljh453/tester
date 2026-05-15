using TesterWorkbench.Core.Engine;
using TesterWorkbench.Core.Workspace;

namespace TesterWorkbench.Core.ViewModels;

public sealed class MainWorkbenchViewModel
{
    private readonly WorkspaceScanner _workspaceScanner;
    private readonly TesterEngineBridge _engineBridge;
    private IReadOnlyList<EngineVariableValue> _runVariables = Array.Empty<EngineVariableValue>();

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

    public string EditorLineNumbersText { get; private set; } = "1";

    public IReadOnlyList<EngineDiagnostic> Problems { get; private set; } = Array.Empty<EngineDiagnostic>();

    public string RunStatus { get; private set; } = "Idle";

    public string? ReportDirectory { get; private set; }

    public IReadOnlyList<EngineRunEvent> ExecutionTrace { get; private set; } = Array.Empty<EngineRunEvent>();

    public IReadOnlyList<EngineVariableValue> Variables { get; private set; } = Array.Empty<EngineVariableValue>();

    public string CurrentSourceFile { get; private set; } = string.Empty;

    public int CurrentLineNumber { get; private set; }

    public string CurrentLocationText { get; private set; } = "No execution line selected.";

    public string ConsoleText { get; private set; } = string.Empty;

    public Task OpenWorkspaceAsync(string workspacePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WorkspacePath = Path.GetFullPath(workspacePath);
        WorkspaceRoot = _workspaceScanner.Scan(WorkspacePath);
        ClearCurrentExecutionLocation();
        ConsoleText = $"Opened workspace: {WorkspacePath}";
        return Task.CompletedTask;
    }

    public async Task OpenFileAsync(string yamlFilePath, CancellationToken cancellationToken = default)
    {
        SelectedFilePath = Path.GetFullPath(yamlFilePath);
        UpdateEditorText(await File.ReadAllTextAsync(SelectedFilePath, cancellationToken));
        ClearCurrentExecutionLocation();
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

    public async Task RunAsync(
        string? runId = null,
        Action? onExecutionChanged = null,
        CancellationToken cancellationToken = default)
    {
        EnsureFileSelected();
        var effectiveRunId = string.IsNullOrWhiteSpace(runId)
            ? $"gui-run-{DateTimeOffset.Now:yyyyMMdd-HHmmss}"
            : runId;
        var reportsRoot = WorkspacePath is null
            ? Path.Combine(Path.GetDirectoryName(SelectedFilePath!)!, "reports")
            : Path.Combine(WorkspacePath, "reports");

        RunStatus = "Running";
        ReportDirectory = null;
        ExecutionTrace = Array.Empty<EngineRunEvent>();
        _runVariables = Array.Empty<EngineVariableValue>();
        Variables = Array.Empty<EngineVariableValue>();
        ClearCurrentExecutionLocation();
        NotifyExecutionChanged(onExecutionChanged);

        var result = await _engineBridge.RunAsync(
            SelectedFilePath!,
            effectiveRunId,
            reportsRoot,
            cancellationToken,
            runEvent =>
            {
                AppendExecutionTraceEvent(runEvent);
                NotifyExecutionChanged(onExecutionChanged);
            });
        RunStatus = result.Status;
        ReportDirectory = result.ReportDirectory;
        var streamedEvents = ExecutionTrace;
        ExecutionTrace = result.Events.Count > 0 ? result.Events : streamedEvents;
        _runVariables = result.Variables;
        SelectExecutionTraceEvent(ExecutionTrace.LastOrDefault());
        ConsoleText = string.IsNullOrWhiteSpace(result.StandardError)
            ? $"Run '{effectiveRunId}' exited with status {result.Status}."
            : result.StandardError;
    }

    public void UpdateEditorText(string editorText)
    {
        EditorText = editorText;
        EditorLineNumbersText = BuildLineNumbersText(editorText);
    }

    public void SelectExecutionTraceEvent(EngineRunEvent? runEvent)
    {
        if (runEvent is null)
        {
            Variables = LatestLocalVariablesOrRunVariables();
            ClearCurrentExecutionLocation();
            return;
        }

        Variables = runEvent.HasLocalVariables
            ? runEvent.LocalVariables
            : _runVariables.Where(variable => variable.Testcase == runEvent.Testcase).ToArray();
        CurrentSourceFile = runEvent.SourceFile;
        CurrentLineNumber = runEvent.SourceLine;
        CurrentLocationText = runEvent.SourceLine > 0
            ? $"Line {runEvent.SourceLine} - {runEvent.CommandType}"
            : runEvent.CommandType;
    }

    private void AppendExecutionTraceEvent(EngineRunEvent runEvent)
    {
        var events = ExecutionTrace.ToList();
        var runningEventIndex = events.FindLastIndex(
            candidate => candidate.Status == "running"
                && candidate.Testcase == runEvent.Testcase
                && candidate.Phase == runEvent.Phase
                && candidate.CommandPath == runEvent.CommandPath);
        if (runEvent.Status != "running" && runningEventIndex >= 0)
        {
            events[runningEventIndex] = runEvent;
        }
        else
        {
            events.Add(runEvent);
        }

        ExecutionTrace = events;
        SelectExecutionTraceEvent(runEvent);
    }

    private static void NotifyExecutionChanged(Action? onExecutionChanged)
    {
        try
        {
            onExecutionChanged?.Invoke();
        }
        catch
        {
            // UI refresh callbacks should not cancel or corrupt an engine run.
        }
    }

    private IReadOnlyList<EngineVariableValue> LatestLocalVariablesOrRunVariables()
    {
        var latestEvent = ExecutionTrace.LastOrDefault();
        return latestEvent is { HasLocalVariables: true }
            ? latestEvent.LocalVariables
            : _runVariables;
    }

    private void ClearCurrentExecutionLocation()
    {
        CurrentSourceFile = string.Empty;
        CurrentLineNumber = 0;
        CurrentLocationText = "No execution line selected.";
    }

    private static string BuildLineNumbersText(string editorText)
    {
        var lineCount = editorText.Count(character => character == '\n') + 1;
        return string.Join(
            Environment.NewLine,
            Enumerable.Range(1, Math.Max(1, lineCount)));
    }

    private void EnsureFileSelected()
    {
        if (string.IsNullOrWhiteSpace(SelectedFilePath))
        {
            throw new InvalidOperationException("No YAML file is selected.");
        }
    }
}
