using TesterWorkbench.Core.Engine;
using TesterWorkbench.Core.Workspace;
using System.Text.Json;

namespace TesterWorkbench.Core.ViewModels;

public sealed class MainWorkbenchViewModel
{
    public const string AvailableBreakpointMarker = "\u25A1";
    public const string ActiveBreakpointMarker = "\u25A3";

    private const double MinimumEditorFontSize = 8.0;
    private const double MaximumEditorFontSize = 32.0;
    private const double EditorFontSizeStep = 1.0;

    private readonly WorkspaceScanner _workspaceScanner;
    private readonly TesterEngineBridge _engineBridge;
    private readonly SortedSet<int> _breakpointLineNumbers = new();
    private IReadOnlyList<EngineVariableValue> _runVariables = Array.Empty<EngineVariableValue>();
    private string? _activeRunControlFile;

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

    public IReadOnlyCollection<int> BreakpointLineNumbers => _breakpointLineNumbers;

    public string BreakpointsText { get; private set; } = "No breakpoints.";

    public IReadOnlyList<EngineDiagnostic> Problems { get; private set; } = Array.Empty<EngineDiagnostic>();

    public string RunStatus { get; private set; } = "Idle";

    public string? ReportDirectory { get; private set; }

    public IReadOnlyList<EngineRunEvent> ExecutionTrace { get; private set; } = Array.Empty<EngineRunEvent>();

    public IReadOnlyList<EngineVariableValue> Variables { get; private set; } = Array.Empty<EngineVariableValue>();

    public WorkbenchGuiModel GuiModel { get; private set; } = WorkbenchGuiModel.Empty;

    public IReadOnlyList<WorkbenchCommandCatalogGroup> CommandCatalogGroups => WorkbenchCommandCatalog.Groups;

    public WorkbenchGuiTestcase? SelectedGuiTestcase { get; private set; }

    public WorkbenchCommandBlock? SelectedGuiCommand { get; private set; }

    public string CurrentSourceFile { get; private set; } = string.Empty;

    public int CurrentLineNumber { get; private set; }

    public string CurrentLocationText { get; private set; } = "No execution line selected.";

    public string ConsoleText { get; private set; } = string.Empty;

    public bool AutoFocusExecutionLine { get; private set; } = true;

    public double EditorFontSize { get; private set; } = 13.0;

    public WorkbenchThemeMode ThemeMode { get; private set; } = WorkbenchThemeMode.Dark;

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
        _breakpointLineNumbers.Clear();
        UpdateBreakpointsText();
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
        var runControlFile = CreateRunControlFilePath(reportsRoot, effectiveRunId);

        RunStatus = "Running";
        ReportDirectory = null;
        ExecutionTrace = Array.Empty<EngineRunEvent>();
        _runVariables = Array.Empty<EngineVariableValue>();
        Variables = Array.Empty<EngineVariableValue>();
        ConsoleText = $"Run '{effectiveRunId}' started.";
        ClearCurrentExecutionLocation();
        NotifyExecutionChanged(onExecutionChanged);

        EngineRunResult result;
        _activeRunControlFile = runControlFile;
        await WriteRunControlStateAsync(runControlFile, "running", _breakpointLineNumbers, cancellationToken);
        try
        {
            result = await _engineBridge.RunAsync(
                SelectedFilePath!,
                effectiveRunId,
                reportsRoot,
                cancellationToken,
                runEvent =>
                {
                    AppendExecutionTraceEvent(runEvent);
                    NotifyExecutionChanged(onExecutionChanged);
                },
                runControlFile,
                _breakpointLineNumbers.ToArray());
        }
        finally
        {
            _activeRunControlFile = null;
            TryDeleteRunControlFile(runControlFile);
        }
        RunStatus = result.Status;
        ReportDirectory = result.ReportDirectory;
        var streamedEvents = ExecutionTrace;
        var hadStreamedEvents = streamedEvents.Count > 0;
        ExecutionTrace = result.Events.Count > 0 ? result.Events : streamedEvents;
        if (!hadStreamedEvents)
        {
            AppendLogEvents(ExecutionTrace);
        }

        _runVariables = result.Variables;
        SelectExecutionTraceEvent(ExecutionTrace.LastOrDefault());
        AppendConsoleLine(
            string.IsNullOrWhiteSpace(result.StandardError)
                ? $"Run '{effectiveRunId}' exited with status {result.Status}."
                : result.StandardError);
    }

    public async Task PauseRunAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_activeRunControlFile))
        {
            AppendConsoleLine("No active run to pause.");
            return;
        }

        await WriteRunControlStateAsync(
            _activeRunControlFile,
            "paused",
            _breakpointLineNumbers,
            cancellationToken);
        RunStatus = "Pause Requested";
        AppendConsoleLine("Pause requested.");
    }

    public async Task ResumeRunAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_activeRunControlFile))
        {
            AppendConsoleLine("No active run to resume.");
            return;
        }

        await WriteRunControlStateAsync(
            _activeRunControlFile,
            "running",
            _breakpointLineNumbers,
            cancellationToken);
        RunStatus = "Running";
        AppendConsoleLine("Resume requested.");
    }

    public void ToggleBreakpointAtCurrentLine()
    {
        ToggleBreakpointAtLine(CurrentLineNumber);
    }

    public void ToggleBreakpointForSelectedCommand()
    {
        if (SelectedGuiCommand is not null)
        {
            ToggleBreakpointAtLine(SelectedGuiCommand.SourceLineStart);
        }
    }

    public void ToggleBreakpointAtLine(int lineNumber)
    {
        if (!IsBreakpointEligibleLine(lineNumber))
        {
            return;
        }

        if (!_breakpointLineNumbers.Add(lineNumber))
        {
            _breakpointLineNumbers.Remove(lineNumber);
        }

        RefreshBreakpointViews();
        SyncActiveRunBreakpoints();
    }

    public void UpdateEditorText(string editorText)
    {
        EditorText = editorText;
        var previousTestcaseName = SelectedGuiTestcase?.Name;
        GuiModel = WorkbenchGuiModelBuilder.Build(editorText);
        SelectedGuiTestcase = GuiModel.Testcases.FirstOrDefault(testcase => testcase.Name == previousTestcaseName)
            ?? GuiModel.Testcases.FirstOrDefault();
        SelectedGuiCommand = SelectedGuiTestcase?.Phases
            .SelectMany(phase => FlattenCommands(phase.Blocks))
            .FirstOrDefault();
        PruneBreakpointsOutsideEditor();
        PruneBreakpointsOutsideCommandLines();
        EditorLineNumbersText = BuildLineNumbersText(
            editorText,
            _breakpointLineNumbers,
            EligibleBreakpointLineNumbers());
        UpdateBreakpointsText();
        UpdateGuiCurrentExecutionBlock();
        UpdateGuiBreakpointMarkers();
    }

    public void SetAutoFocusExecutionLine(bool enabled)
    {
        AutoFocusExecutionLine = enabled;
    }

    public void SetThemeMode(WorkbenchThemeMode themeMode)
    {
        ThemeMode = themeMode;
    }

    public void ZoomEditorIn()
    {
        EditorFontSize = Math.Min(MaximumEditorFontSize, EditorFontSize + EditorFontSizeStep);
    }

    public void ZoomEditorOut()
    {
        EditorFontSize = Math.Max(MinimumEditorFontSize, EditorFontSize - EditorFontSizeStep);
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
        UpdateGuiCurrentExecutionBlock();
    }

    public void SelectGuiCommand(WorkbenchCommandBlock? commandBlock)
    {
        SelectedGuiCommand = commandBlock;
        if (commandBlock is null)
        {
            return;
        }

        CurrentLineNumber = commandBlock.SourceLineStart;
        CurrentLocationText = $"Line {commandBlock.SourceLineStart} - {commandBlock.CommandType}";
    }

    public void SelectGuiTestcase(WorkbenchGuiTestcase? testcase)
    {
        SelectedGuiTestcase = testcase;
        SelectedGuiCommand = SelectedGuiTestcase?.Phases
            .SelectMany(phase => FlattenCommands(phase.Blocks))
            .FirstOrDefault();
        UpdateGuiCurrentExecutionBlock();
    }

    public void InsertGuiCommand(
        WorkbenchCommandDefinition command,
        WorkbenchGuiPhase phase,
        WorkbenchCommandBlock? afterCommand = null)
    {
        var placement = afterCommand is null
            ? WorkbenchCommandInsertPlacement.AtPhaseEnd
            : WorkbenchCommandInsertPlacement.AfterCommand;
        InsertGuiCommand(
            command,
            new WorkbenchCommandInsertionTarget(phase, placement, afterCommand));
    }

    public void InsertGuiCommand(
        WorkbenchCommandDefinition command,
        WorkbenchCommandInsertionTarget target)
    {
        if (SelectedGuiTestcase is null)
        {
            throw new InvalidOperationException("No testcase is selected.");
        }

        var testcaseName = SelectedGuiTestcase.Name;
        var result = WorkbenchYamlCommandInserter.Insert(
            EditorText,
            SelectedGuiTestcase,
            command,
            target);
        UpdateEditorText(result.Text);
        SelectedGuiTestcase = GuiModel.Testcases.FirstOrDefault(testcase =>
            testcase.Name == testcaseName)
            ?? GuiModel.Testcases.FirstOrDefault();
        SelectedGuiCommand = SelectedGuiTestcase?.Phases
            .SelectMany(phaseModel => FlattenCommands(phaseModel.Blocks))
            .FirstOrDefault(commandBlock =>
                commandBlock.SourceLineStart == result.InsertedLineNumber
                && commandBlock.CommandType == command.CommandType);
        CurrentLineNumber = result.InsertedLineNumber;
        CurrentLocationText = $"Line {CurrentLineNumber} - {command.CommandType}";
        UpdateGuiCurrentExecutionBlock();
    }

    public void MoveGuiCommand(
        WorkbenchCommandBlock movingCommand,
        WorkbenchCommandInsertionTarget target)
    {
        if (SelectedGuiTestcase is null)
        {
            throw new InvalidOperationException("No testcase is selected.");
        }

        var testcaseName = SelectedGuiTestcase.Name;
        var result = WorkbenchYamlCommandMover.Move(
            EditorText,
            SelectedGuiTestcase,
            movingCommand,
            target);
        UpdateEditorText(result.Text);
        SelectedGuiTestcase = GuiModel.Testcases.FirstOrDefault(testcase =>
            testcase.Name == testcaseName)
            ?? GuiModel.Testcases.FirstOrDefault();
        SelectedGuiCommand = SelectedGuiTestcase?.Phases
            .SelectMany(phaseModel => FlattenCommands(phaseModel.Blocks))
            .FirstOrDefault(commandBlock =>
                commandBlock.SourceLineStart == result.InsertedLineNumber
                && commandBlock.CommandType == movingCommand.CommandType);
        CurrentLineNumber = result.InsertedLineNumber;
        CurrentLocationText = $"Line {CurrentLineNumber} - {movingCommand.CommandType}";
        UpdateGuiCurrentExecutionBlock();
    }

    public void ShowGuiCommandInsertionPreview(
        WorkbenchCommandDefinition command,
        WorkbenchGuiPhase phase,
        WorkbenchCommandBlock? afterCommand = null)
    {
        ClearGuiCommandInsertionPreview();
        if (afterCommand is not null)
        {
            afterCommand.IsDragInsertionTarget = true;
            afterCommand.DragInsertionText =
                $"Insert {command.CommandType} after command {afterCommand.DisplayIndex}";
            return;
        }

        phase.IsDragInsertionTarget = true;
        phase.DragInsertionText = $"Insert {command.CommandType} at end of {phase.Name}";
    }

    public void ShowGuiCommandInsertionPreview(
        WorkbenchCommandDefinition command,
        WorkbenchCommandInsertionTarget target)
    {
        ShowGuiDropPreview(command.CommandType, target, "Insert");
    }

    public void ShowGuiCommandMovePreview(
        WorkbenchCommandBlock movingCommand,
        WorkbenchCommandInsertionTarget target)
    {
        ShowGuiDropPreview(movingCommand.CommandType, target, "Move");
    }

    private void ShowGuiDropPreview(
        string commandType,
        WorkbenchCommandInsertionTarget target,
        string action)
    {
        ClearGuiCommandInsertionPreview();
        switch (target.Placement)
        {
            case WorkbenchCommandInsertPlacement.BeforeFirstInPhase:
                target.Phase.StartDropTarget.IsDragInsertionTarget = true;
                target.Phase.StartDropTarget.DragInsertionText =
                    $"{action} {commandType} to top of {target.Phase.Name}";
                break;
            case WorkbenchCommandInsertPlacement.InsideCommand when target.ReferenceCommand is not null:
                target.ReferenceCommand.InsideDropTarget.IsDragInsertionTarget = true;
                target.ReferenceCommand.InsideDropTarget.DragInsertionText =
                    $"{action} {commandType} inside {target.ReferenceCommand.DisplayIndex}";
                break;
            case WorkbenchCommandInsertPlacement.AfterCommand when target.ReferenceCommand is not null:
                target.ReferenceCommand.IsDragInsertionTarget = true;
                target.ReferenceCommand.DragInsertionText =
                    $"{action} {commandType} after command {target.ReferenceCommand.DisplayIndex}";
                break;
            default:
                target.Phase.EndDropTarget.IsDragInsertionTarget = true;
                target.Phase.EndDropTarget.DragInsertionText =
                    $"{action} {commandType} to end of {target.Phase.Name}";
                break;
        }
    }

    public void ClearGuiCommandInsertionPreview()
    {
        foreach (var phase in SelectedGuiTestcase?.Phases ?? Array.Empty<WorkbenchGuiPhase>())
        {
            phase.IsDragInsertionTarget = false;
            phase.DragInsertionText = string.Empty;
            phase.StartDropTarget.IsDragInsertionTarget = false;
            phase.StartDropTarget.DragInsertionText = string.Empty;
            phase.EndDropTarget.IsDragInsertionTarget = false;
            phase.EndDropTarget.DragInsertionText = string.Empty;
            foreach (var commandBlock in FlattenCommands(phase.Blocks))
            {
                commandBlock.IsDragInsertionTarget = false;
                commandBlock.DragInsertionText = string.Empty;
                commandBlock.InsideDropTarget.IsDragInsertionTarget = false;
                commandBlock.InsideDropTarget.DragInsertionText = string.Empty;
            }
        }
    }

    public void ExpandAllGuiBlocks()
    {
        foreach (var commandBlock in AllGuiCommandBlocks())
        {
            commandBlock.IsExpanded = true;
        }
    }

    public void FoldAllGuiBlocks()
    {
        foreach (var commandBlock in AllGuiCommandBlocks())
        {
            if (commandBlock.IsFoldable)
            {
                commandBlock.IsExpanded = false;
            }
        }
    }

    public void FoldGuiBlocksFromLevel(int level)
    {
        foreach (var commandBlock in AllGuiCommandBlocks())
        {
            commandBlock.IsExpanded = !commandBlock.IsFoldable || commandBlock.Depth < level;
        }
    }

    private void AppendExecutionTraceEvent(EngineRunEvent runEvent)
    {
        if (runEvent.Status == "paused")
        {
            RunStatus = "Paused";
        }
        else if (runEvent.Status == "running")
        {
            RunStatus = "Running";
        }

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
        AppendLogEvent(runEvent);
    }

    private void AppendLogEvents(IEnumerable<EngineRunEvent> runEvents)
    {
        foreach (var runEvent in runEvents)
        {
            AppendLogEvent(runEvent);
        }
    }

    private void AppendLogEvent(EngineRunEvent runEvent)
    {
        if (!string.IsNullOrWhiteSpace(runEvent.LogText))
        {
            AppendConsoleLine(runEvent.LogText);
        }
    }

    private void AppendConsoleLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        ConsoleText = string.IsNullOrWhiteSpace(ConsoleText)
            ? line
            : $"{ConsoleText}{Environment.NewLine}{line}";
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
        UpdateGuiCurrentExecutionBlock();
    }

    private void UpdateGuiCurrentExecutionBlock()
    {
        foreach (var commandBlock in AllGuiCommandBlocks())
        {
            commandBlock.IsCurrentExecution = CurrentLineNumber >= commandBlock.SourceLineStart
                && CurrentLineNumber <= commandBlock.SourceLineEnd;
        }
    }

    private void UpdateGuiBreakpointMarkers()
    {
        foreach (var commandBlock in AllGuiCommandBlocks())
        {
            commandBlock.IsBreakpoint = _breakpointLineNumbers.Contains(commandBlock.SourceLineStart);
        }
    }

    private IEnumerable<WorkbenchCommandBlock> AllGuiCommandBlocks()
    {
        return SelectedGuiTestcase is null
            ? Array.Empty<WorkbenchCommandBlock>()
            : SelectedGuiTestcase.Phases.SelectMany(phase => FlattenCommands(phase.Blocks));
    }

    private static IEnumerable<WorkbenchCommandBlock> FlattenCommands(
        IEnumerable<WorkbenchCommandBlock> commandBlocks)
    {
        foreach (var commandBlock in commandBlocks)
        {
            yield return commandBlock;
            foreach (var childCommandBlock in FlattenCommands(commandBlock.Children))
            {
                yield return childCommandBlock;
            }
        }
    }

    private void RefreshBreakpointViews()
    {
        EditorLineNumbersText = BuildLineNumbersText(
            EditorText,
            _breakpointLineNumbers,
            EligibleBreakpointLineNumbers());
        UpdateBreakpointsText();
        UpdateGuiBreakpointMarkers();
    }

    private void SyncActiveRunBreakpoints()
    {
        if (string.IsNullOrWhiteSpace(_activeRunControlFile))
        {
            return;
        }

        var state = ReadRunControlState(_activeRunControlFile);
        WriteRunControlState(_activeRunControlFile, state, _breakpointLineNumbers);
    }

    private void UpdateBreakpointsText()
    {
        BreakpointsText = _breakpointLineNumbers.Count == 0
            ? "No breakpoints."
            : string.Join(", ", _breakpointLineNumbers.Select(lineNumber => $"L{lineNumber}"));
    }

    private void PruneBreakpointsOutsideEditor()
    {
        var lineCount = CountEditorLines(EditorText);
        _breakpointLineNumbers.RemoveWhere(lineNumber => lineNumber > lineCount);
    }

    private void PruneBreakpointsOutsideCommandLines()
    {
        var eligibleLines = EligibleBreakpointLineNumbers();
        _breakpointLineNumbers.RemoveWhere(lineNumber => !eligibleLines.Contains(lineNumber));
    }

    private bool IsBreakpointEligibleLine(int lineNumber)
    {
        return lineNumber > 0
            && lineNumber <= CountEditorLines(EditorText)
            && EligibleBreakpointLineNumbers().Contains(lineNumber);
    }

    private IReadOnlySet<int> EligibleBreakpointLineNumbers()
    {
        return GuiModel.Testcases
            .SelectMany(testcase => testcase.Phases)
            .SelectMany(phase => FlattenCommands(phase.Blocks))
            .Select(commandBlock => commandBlock.SourceLineStart)
            .Where(lineNumber => lineNumber > 0)
            .ToHashSet();
    }

    private static string BuildLineNumbersText(
        string editorText,
        IReadOnlySet<int> breakpointLineNumbers,
        IReadOnlySet<int> eligibleBreakpointLineNumbers)
    {
        var lineCount = CountEditorLines(editorText);
        return string.Join(
            Environment.NewLine,
            Enumerable.Range(1, Math.Max(1, lineCount))
                .Select(lineNumber =>
                    breakpointLineNumbers.Contains(lineNumber)
                        ? $"{ActiveBreakpointMarker} {lineNumber}"
                        : eligibleBreakpointLineNumbers.Contains(lineNumber)
                            ? $"{AvailableBreakpointMarker} {lineNumber}"
                            : $"{lineNumber}"));
    }

    private static int CountEditorLines(string editorText)
    {
        return editorText.Count(character => character == '\n') + 1;
    }

    private static string CreateRunControlFilePath(string reportsRoot, string runId)
    {
        return Path.Combine(reportsRoot, runId, "control.json");
    }

    private static async Task WriteRunControlStateAsync(
        string controlFile,
        string state,
        IEnumerable<int> breakpointLineNumbers,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(controlFile)!);
        await File.WriteAllTextAsync(
            controlFile,
            JsonSerializer.Serialize(RunControlPayload(state, breakpointLineNumbers)),
            cancellationToken);
    }

    private static void WriteRunControlState(
        string controlFile,
        string state,
        IEnumerable<int> breakpointLineNumbers)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(controlFile)!);
        File.WriteAllText(
            controlFile,
            JsonSerializer.Serialize(RunControlPayload(state, breakpointLineNumbers)));
    }

    private static string ReadRunControlState(string controlFile)
    {
        try
        {
            if (!File.Exists(controlFile))
            {
                return "running";
            }

            using var document = JsonDocument.Parse(File.ReadAllText(controlFile));
            return document.RootElement.TryGetProperty("state", out var stateElement)
                ? stateElement.GetString() ?? "running"
                : "running";
        }
        catch
        {
            return "running";
        }
    }

    private static object RunControlPayload(
        string state,
        IEnumerable<int> breakpointLineNumbers)
    {
        return new
        {
            state,
            breakpoint_lines = breakpointLineNumbers
                .Where(lineNumber => lineNumber > 0)
                .Distinct()
                .Order()
                .ToArray()
        };
    }

    private static void TryDeleteRunControlFile(string controlFile)
    {
        try
        {
            if (File.Exists(controlFile))
            {
                File.Delete(controlFile);
            }
        }
        catch
        {
            // Control files are transient debug IPC; stale files should not mask run results.
        }
    }

    private void EnsureFileSelected()
    {
        if (string.IsNullOrWhiteSpace(SelectedFilePath))
        {
            throw new InvalidOperationException("No YAML file is selected.");
        }
    }
}
