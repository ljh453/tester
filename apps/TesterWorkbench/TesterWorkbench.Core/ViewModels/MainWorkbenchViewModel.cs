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
    private readonly SortedSet<int> _selectedGuiCommandLineNumbers = new();
    private readonly HashSet<string> _selectedGuiTestcaseRunNames = new(StringComparer.Ordinal);
    private IReadOnlyList<EngineVariableValue> _runVariables = Array.Empty<EngineVariableValue>();
    private string? _activeRunControlFile;
    private bool _isRunInProgress;
    private string _lastSavedEditorText = string.Empty;
    private int? _guiBulkSelectionAnchorLineNumber;
    private string? _cachedSuggestionContextPath;
    private DateTime _cachedSuggestionContextWriteTimeUtc;
    private WorkbenchGuiSuggestionContext _cachedSuggestionContext = WorkbenchGuiSuggestionContext.Empty;

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

    public bool IsDirty { get; private set; }

    public string SaveStatusText { get; private set; } = "No file selected.";

    public string SelectedFileDisplayText => SelectedFilePath is null
        ? string.Empty
        : IsDirty
            ? $"{SelectedFilePath} *"
            : SelectedFilePath;

    public string EditorLineNumbersText { get; private set; } = "1";

    public IReadOnlyCollection<int> BreakpointLineNumbers => _breakpointLineNumbers;

    public string BreakpointsText { get; private set; } = "No breakpoints.";

    public IReadOnlyList<EngineDiagnostic> Problems { get; private set; } = Array.Empty<EngineDiagnostic>();

    public string RunStatus { get; private set; } = "Idle";

    public bool IsRunInProgress => _isRunInProgress;

    public string? ReportDirectory { get; private set; }

    public string? ReportSummaryPath => string.IsNullOrWhiteSpace(ReportDirectory)
        ? null
        : Path.Combine(ReportDirectory, "summary.html");

    public IReadOnlyList<EngineRunEvent> ExecutionTrace { get; private set; } = Array.Empty<EngineRunEvent>();

    public IReadOnlyList<EngineVariableValue> Variables { get; private set; } = Array.Empty<EngineVariableValue>();

    public WorkbenchGuiModel GuiModel { get; private set; } = WorkbenchGuiModel.Empty;

    public IReadOnlyList<WorkbenchCommandCatalogGroup> CommandCatalogGroups => WorkbenchCommandCatalog.Groups;

    public WorkbenchGuiTestcase? SelectedGuiTestcase { get; private set; }

    public WorkbenchCommandBlock? SelectedGuiCommand { get; private set; }

    public IReadOnlyList<string> SelectedGuiTestcaseRunNames => SelectedGuiTestcaseRunNamesInFileOrder();

    public int SelectedGuiTestcaseRunCount => SelectedGuiTestcaseRunNames.Count;

    public string SelectedGuiTestcaseRunText
    {
        get
        {
            var selectedNames = SelectedGuiTestcaseRunNames;
            return selectedNames.Count switch
            {
                0 => "Run target: all testcases",
                1 => $"Run target: {selectedNames[0]}",
                _ => $"Run target: {selectedNames.Count} testcases"
            };
        }
    }

    public int SelectedGuiCommandCount => _selectedGuiCommandLineNumbers.Count;

    public string CurrentSourceFile { get; private set; } = string.Empty;

    public int CurrentLineNumber { get; private set; }

    public string CurrentLocationText { get; private set; } = "No execution line selected.";

    public string ConsoleText { get; private set; } = string.Empty;

    public bool AutoFocusExecutionLine { get; private set; } = true;

    public double EditorFontSize { get; private set; } = 13.0;

    public WorkbenchThemeMode ThemeMode { get; private set; } = WorkbenchThemeMode.System;

    public async Task OpenWorkspaceAsync(string workspacePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var resolvedWorkspacePath = Path.GetFullPath(workspacePath);
        var workspaceRoot = await Task.Run(
            () => _workspaceScanner.Scan(resolvedWorkspacePath),
            cancellationToken);
        WorkspacePath = resolvedWorkspacePath;
        WorkspaceRoot = workspaceRoot;
        ClearCurrentExecutionLocation();
        ConsoleText = $"Opened workspace: {WorkspacePath}";
    }

    public async Task OpenFileAsync(string yamlFilePath, CancellationToken cancellationToken = default)
    {
        SelectedFilePath = Path.GetFullPath(yamlFilePath);
        _breakpointLineNumbers.Clear();
        _selectedGuiCommandLineNumbers.Clear();
        _selectedGuiTestcaseRunNames.Clear();
        _guiBulkSelectionAnchorLineNumber = null;
        UpdateBreakpointsText();
        var editorText = await File.ReadAllTextAsync(SelectedFilePath, cancellationToken);
        _lastSavedEditorText = editorText;
        UpdateEditorText(editorText, markDirty: false);
        IsDirty = false;
        SaveStatusText = "Saved.";
        ClearCurrentExecutionLocation();
        ConsoleText = $"Opened file: {SelectedFilePath}";
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        EnsureFileSelected();
        var diagnostics = await CompileEditorTextForSaveAsync(cancellationToken);
        Problems = diagnostics;
        await File.WriteAllTextAsync(SelectedFilePath!, EditorText, cancellationToken);
        _lastSavedEditorText = EditorText;
        IsDirty = false;
        SaveStatusText = diagnostics.Count == 0
            ? $"Saved {Path.GetFileName(SelectedFilePath)} at {DateTime.Now:HH:mm:ss}."
            : $"Saved with {diagnostics.Count} diagnostic(s): {Path.GetFileName(SelectedFilePath)} at {DateTime.Now:HH:mm:ss}.";
        AppendConsoleLine(SaveStatusText);
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
        CancellationToken cancellationToken = default,
        Action<Action>? dispatchExecutionUpdate = null)
    {
        EnsureFileSelected();
        if (_isRunInProgress)
        {
            AppendConsoleLine("Run is already running. Ignored duplicate run request.");
            NotifyExecutionChanged(onExecutionChanged);
            return;
        }

        _isRunInProgress = true;
        try
        {
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
        var selectedTestcaseNames = SelectedGuiTestcaseRunNames;
        ConsoleText = selectedTestcaseNames.Count == 0
            ? $"Run '{effectiveRunId}' started."
            : $"Run '{effectiveRunId}' started for {selectedTestcaseNames.Count} selected testcase(s): {string.Join(", ", selectedTestcaseNames)}.";
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
                    DispatchExecutionUpdate(
                        dispatchExecutionUpdate,
                        () =>
                        {
                            AppendExecutionTraceEvent(runEvent);
                            NotifyExecutionChanged(onExecutionChanged);
                        });
                },
                runControlFile,
                _breakpointLineNumbers.ToArray(),
                selectedTestcaseNames);
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
        finally
        {
            _isRunInProgress = false;
        }
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

    public async Task StopRunAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_activeRunControlFile))
        {
            AppendConsoleLine("No active run to stop.");
            return;
        }

        await WriteRunControlStateAsync(
            _activeRunControlFile,
            "stopping",
            _breakpointLineNumbers,
            cancellationToken);
        RunStatus = "Stop Requested";
        AppendConsoleLine("Stop requested.");
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

    public void UpdateEditorText(string editorText, bool markDirty = true)
    {
        var previousCommandLineNumber = SelectedGuiCommand?.SourceLineStart;
        var previousCommandType = SelectedGuiCommand?.CommandType;
        EditorText = editorText;
        if (markDirty)
        {
            IsDirty = !string.Equals(EditorText, _lastSavedEditorText, StringComparison.Ordinal);
            SaveStatusText = IsDirty ? "Unsaved changes." : "Saved.";
        }

        var previousTestcaseName = SelectedGuiTestcase?.Name;
        GuiModel = WorkbenchGuiModelBuilder.Build(
            editorText,
            BuildExternalGuiSuggestionContext(editorText));
        SelectedGuiTestcase = GuiModel.Testcases.FirstOrDefault(testcase => testcase.Name == previousTestcaseName)
            ?? GuiModel.Testcases.FirstOrDefault();
        PruneSelectedGuiTestcaseRunNames();
        SelectedGuiCommand = FindCommandByLineAndType(
                SelectedGuiTestcase,
                previousCommandLineNumber,
                previousCommandType)
            ?? SelectedGuiTestcase?.Phases
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
        UpdateGuiSelectionMarkers();
    }

    public void SetAutoFocusExecutionLine(bool enabled)
    {
        AutoFocusExecutionLine = enabled;
    }

    public void SetThemeMode(WorkbenchThemeMode themeMode)
    {
        ThemeMode = themeMode;
    }

    public async Task<WorkbenchSettingsSnapshot> GetSettingsSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        var toolProfilePath = ResolveCurrentToolProfilePath();
        if (string.IsNullOrWhiteSpace(toolProfilePath) || !File.Exists(toolProfilePath))
        {
            return new WorkbenchSettingsSnapshot(
                ThemeMode,
                toolProfilePath,
                Array.Empty<WorkbenchSerialDeviceSettings>(),
                Array.Empty<WorkbenchCommandDefaultSettings>());
        }

        var profileText = await File.ReadAllTextAsync(toolProfilePath, cancellationToken);
        return new WorkbenchSettingsSnapshot(
            ThemeMode,
            toolProfilePath,
            WorkbenchToolProfileSettingsEditor.ReadSerialDevices(profileText),
            WorkbenchToolProfileSettingsEditor.ReadCommandDefaults(profileText));
    }

    public async Task ApplySettingsAsync(
        WorkbenchSettingsUpdate settings,
        CancellationToken cancellationToken = default)
    {
        ThemeMode = settings.ThemeMode;
        var toolProfilePath = ResolveCurrentToolProfilePath();
        if (!string.IsNullOrWhiteSpace(toolProfilePath)
            && File.Exists(toolProfilePath)
            && (settings.SerialDevices.Count > 0 || settings.CommandDefaults.Count > 0))
        {
            var profileText = await File.ReadAllTextAsync(toolProfilePath, cancellationToken);
            var updatedProfileText = await WorkbenchToolProfileSettingsEditor.UpdateSerialDevicesAsync(
                profileText,
                settings.SerialDevices);
            updatedProfileText = await WorkbenchToolProfileSettingsEditor.UpdateCommandDefaultsAsync(
                updatedProfileText,
                settings.CommandDefaults);
            await File.WriteAllTextAsync(toolProfilePath, updatedProfileText, cancellationToken);
            _cachedSuggestionContextPath = null;
            _cachedSuggestionContext = WorkbenchGuiSuggestionContext.Empty;
            AppendConsoleLine($"Saved settings to {Path.GetFileName(toolProfilePath)}.");
        }
    }

    private WorkbenchGuiSuggestionContext BuildExternalGuiSuggestionContext(string editorText)
    {
        var toolProfilePath = FindToolProfilePath(editorText);
        if (string.IsNullOrWhiteSpace(toolProfilePath))
        {
            return WorkbenchGuiSuggestionContext.Empty;
        }

        var resolvedPath = ResolveWorkspacePath(toolProfilePath);
        if (string.IsNullOrWhiteSpace(resolvedPath) || !File.Exists(resolvedPath))
        {
            return WorkbenchGuiSuggestionContext.Empty;
        }

        try
        {
            var writeTimeUtc = File.GetLastWriteTimeUtc(resolvedPath);
            if (string.Equals(_cachedSuggestionContextPath, resolvedPath, StringComparison.OrdinalIgnoreCase)
                && _cachedSuggestionContextWriteTimeUtc == writeTimeUtc)
            {
                return _cachedSuggestionContext;
            }

            var suggestionContext = WorkbenchGuiModelBuilder.BuildSuggestionContext(
                File.ReadAllText(resolvedPath));
            _cachedSuggestionContextPath = resolvedPath;
            _cachedSuggestionContextWriteTimeUtc = writeTimeUtc;
            _cachedSuggestionContext = suggestionContext;
            return suggestionContext;
        }
        catch
        {
            return WorkbenchGuiSuggestionContext.Empty;
        }
    }

    private async Task<IReadOnlyList<EngineDiagnostic>> CompileEditorTextForSaveAsync(
        CancellationToken cancellationToken)
    {
        var selectedFile = SelectedFilePath!;
        var selectedDirectory = Path.GetDirectoryName(selectedFile)!;
        var fileName = Path.GetFileName(selectedFile);
        var tempFile = Path.Combine(
            selectedDirectory,
            $".{fileName}.{Guid.NewGuid():N}.save-check.yaml");
        try
        {
            await File.WriteAllTextAsync(tempFile, EditorText, cancellationToken);
            var result = await _engineBridge.CompileAsync(tempFile, cancellationToken);
            return result.Diagnostics;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new[]
            {
                new EngineDiagnostic(
                    "error",
                    "SAVE_COMPILE_FAILED",
                    $"Save diagnostics could not run: {ex.Message}")
            };
        }
        finally
        {
            TryDeleteFile(tempFile);
        }
    }

    private static void TryDeleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // Temporary save-diagnostic files should not block the user from saving.
        }
    }

    private string? ResolveWorkspacePath(string path)
    {
        var normalizedPath = path.Trim().Trim('"', '\'');
        if (Path.IsPathRooted(normalizedPath))
        {
            return normalizedPath;
        }

        if (!string.IsNullOrWhiteSpace(WorkspacePath))
        {
            return Path.GetFullPath(Path.Combine(WorkspacePath, normalizedPath));
        }

        if (!string.IsNullOrWhiteSpace(SelectedFilePath))
        {
            return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(SelectedFilePath)!, normalizedPath));
        }

        return null;
    }

    private string? ResolveCurrentToolProfilePath()
    {
        var toolProfilePath = FindToolProfilePath(EditorText);
        if (string.IsNullOrWhiteSpace(toolProfilePath))
        {
            return null;
        }

        return ResolveWorkspacePath(toolProfilePath);
    }

    private static string FindToolProfilePath(string editorText)
    {
        foreach (var line in editorText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("tool_profile:", StringComparison.Ordinal))
            {
                continue;
            }

            return trimmed["tool_profile:".Length..].Trim().Trim('"', '\'');
        }

        return string.Empty;
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

    public void SelectGuiCommandForBulkAction(
        WorkbenchCommandBlock commandBlock,
        bool replaceSelection)
    {
        if (replaceSelection)
        {
            _selectedGuiCommandLineNumbers.Clear();
        }

        _selectedGuiCommandLineNumbers.Add(commandBlock.SourceLineStart);
        _guiBulkSelectionAnchorLineNumber = commandBlock.SourceLineStart;
        SelectGuiCommand(commandBlock);
        UpdateGuiSelectionMarkers();
    }

    public void ToggleGuiCommandForBulkAction(WorkbenchCommandBlock commandBlock)
    {
        if (!_selectedGuiCommandLineNumbers.Add(commandBlock.SourceLineStart))
        {
            _selectedGuiCommandLineNumbers.Remove(commandBlock.SourceLineStart);
        }

        _guiBulkSelectionAnchorLineNumber = commandBlock.SourceLineStart;
        SelectGuiCommand(commandBlock);
        UpdateGuiSelectionMarkers();
    }

    public void SelectGuiCommandRangeForBulkAction(WorkbenchCommandBlock commandBlock)
    {
        if (_guiBulkSelectionAnchorLineNumber is null)
        {
            SelectGuiCommandForBulkAction(commandBlock, replaceSelection: true);
            return;
        }

        var commandBlocks = AllGuiCommandBlocks()
            .OrderBy(candidate => candidate.SourceLineStart)
            .ToArray();
        var anchorIndex = Array.FindIndex(
            commandBlocks,
            candidate => candidate.SourceLineStart == _guiBulkSelectionAnchorLineNumber.Value);
        var targetIndex = Array.FindIndex(
            commandBlocks,
            candidate => candidate.SourceLineStart == commandBlock.SourceLineStart);
        if (anchorIndex < 0 || targetIndex < 0)
        {
            SelectGuiCommandForBulkAction(commandBlock, replaceSelection: true);
            return;
        }

        var firstIndex = Math.Min(anchorIndex, targetIndex);
        var lastIndex = Math.Max(anchorIndex, targetIndex);
        _selectedGuiCommandLineNumbers.Clear();
        for (var index = firstIndex; index <= lastIndex; index++)
        {
            _selectedGuiCommandLineNumbers.Add(commandBlocks[index].SourceLineStart);
        }

        SelectGuiCommand(commandBlock);
        UpdateGuiSelectionMarkers();
    }

    public void SelectGuiCommandsForBulkAction(IReadOnlyList<WorkbenchCommandBlock> commandBlocks)
    {
        var requestedLineNumbers = commandBlocks
            .Select(commandBlock => commandBlock.SourceLineStart)
            .ToHashSet();
        var selectedBlocks = AllGuiCommandBlocks()
            .Where(commandBlock => requestedLineNumbers.Contains(commandBlock.SourceLineStart))
            .OrderBy(commandBlock => commandBlock.SourceLineStart)
            .ToArray();

        _selectedGuiCommandLineNumbers.Clear();
        foreach (var commandBlock in selectedBlocks)
        {
            _selectedGuiCommandLineNumbers.Add(commandBlock.SourceLineStart);
        }

        _guiBulkSelectionAnchorLineNumber = selectedBlocks.FirstOrDefault()?.SourceLineStart;
        SelectGuiCommand(selectedBlocks.LastOrDefault());
        UpdateGuiSelectionMarkers();
    }

    public void ClearGuiCommandBulkSelection()
    {
        _selectedGuiCommandLineNumbers.Clear();
        _guiBulkSelectionAnchorLineNumber = null;
        UpdateGuiSelectionMarkers();
    }

    public void SelectGuiTestcase(WorkbenchGuiTestcase? testcase)
    {
        SelectedGuiTestcase = testcase;
        ClearGuiCommandBulkSelection();
        SelectedGuiCommand = SelectedGuiTestcase?.Phases
            .SelectMany(phase => FlattenCommands(phase.Blocks))
            .FirstOrDefault();
        UpdateGuiCurrentExecutionBlock();
    }

    public void SetSelectedGuiTestcasesForRun(IEnumerable<WorkbenchGuiTestcase> testcases)
    {
        _selectedGuiTestcaseRunNames.Clear();
        foreach (var testcase in testcases)
        {
            if (!string.IsNullOrWhiteSpace(testcase.Name))
            {
                _selectedGuiTestcaseRunNames.Add(testcase.Name);
            }
        }

        PruneSelectedGuiTestcaseRunNames();
    }

    public bool SelectGuiCommandAtLine(int lineNumber)
    {
        if (lineNumber <= 0 || SelectedGuiTestcase is null)
        {
            return false;
        }

        var commandBlock = SelectedGuiTestcase.Phases
            .SelectMany(phase => FlattenCommands(phase.Blocks))
            .Where(candidate =>
                lineNumber >= candidate.SourceLineStart
                && lineNumber <= candidate.SourceLineEnd)
            .OrderByDescending(candidate => candidate.Depth)
            .ThenByDescending(candidate => candidate.SourceLineStart)
            .FirstOrDefault();
        if (commandBlock is null)
        {
            return false;
        }

        SelectGuiCommandForBulkAction(commandBlock, replaceSelection: true);
        CurrentSourceFile = SelectedFilePath ?? string.Empty;
        CurrentLineNumber = lineNumber;
        CurrentLocationText = $"Line {lineNumber} - {commandBlock.CommandType}";
        UpdateGuiCurrentExecutionBlock();
        return true;
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
        _selectedGuiCommandLineNumbers.Clear();
        _guiBulkSelectionAnchorLineNumber = null;
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

    public void UpdateSelectedGuiCommandArgument(string argumentName, string value)
    {
        if (SelectedGuiTestcase is null)
        {
            throw new InvalidOperationException("No testcase is selected.");
        }

        if (SelectedGuiCommand is null)
        {
            throw new InvalidOperationException("No command is selected.");
        }

        var argument = SelectedGuiCommand.Arguments
            .FirstOrDefault(candidate => candidate.Name == argumentName)
            ?.Definition
            ?? WorkbenchCommandCatalog.Find(SelectedGuiCommand.CommandType)
                ?.Arguments
                .FirstOrDefault(candidate => candidate.Name == argumentName);
        if (argument is null)
        {
            throw new InvalidOperationException($"Command '{SelectedGuiCommand.CommandType}' does not define argument '{argumentName}'.");
        }

        var testcaseName = SelectedGuiTestcase.Name;
        var commandLineNumber = SelectedGuiCommand.SourceLineStart;
        var commandType = SelectedGuiCommand.CommandType;
        var result = WorkbenchYamlCommandArgumentUpdater.Update(
            EditorText,
            SelectedGuiCommand,
            argument,
            value);
        UpdateEditorText(result.Text);
        _selectedGuiCommandLineNumbers.Clear();
        _guiBulkSelectionAnchorLineNumber = null;
        SelectedGuiTestcase = GuiModel.Testcases.FirstOrDefault(testcase =>
            testcase.Name == testcaseName)
            ?? GuiModel.Testcases.FirstOrDefault();
        SelectedGuiCommand = SelectedGuiTestcase?.Phases
            .SelectMany(phaseModel => FlattenCommands(phaseModel.Blocks))
            .FirstOrDefault(commandBlock =>
                commandBlock.SourceLineStart == commandLineNumber
                && commandBlock.CommandType == commandType)
            ?? SelectedGuiTestcase?.Phases
                .SelectMany(phaseModel => FlattenCommands(phaseModel.Blocks))
                .FirstOrDefault(commandBlock => commandBlock.CommandType == commandType);
        if (SelectedGuiCommand is not null)
        {
            CurrentLineNumber = SelectedGuiCommand.SourceLineStart;
            CurrentLocationText = $"Line {CurrentLineNumber} - {SelectedGuiCommand.CommandType}";
        }

        UpdateGuiCurrentExecutionBlock();
        UpdateGuiSelectionMarkers();
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
        _selectedGuiCommandLineNumbers.Clear();
        _guiBulkSelectionAnchorLineNumber = null;
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

    public IReadOnlyList<WorkbenchCommandBlock> GetGuiCommandsForDrag(
        WorkbenchCommandBlock dragAnchor)
    {
        if (dragAnchor is null)
        {
            throw new ArgumentNullException(nameof(dragAnchor));
        }

        if (!dragAnchor.IsSelectedForBulkAction || _selectedGuiCommandLineNumbers.Count <= 1)
        {
            return new[] { dragAnchor };
        }

        return TopLevelSelectedCommandBlocks(
            AllGuiCommandBlocks()
                .Where(commandBlock => _selectedGuiCommandLineNumbers.Contains(commandBlock.SourceLineStart))
                .OrderBy(commandBlock => commandBlock.SourceLineStart)
                .ToArray());
    }

    public void MoveSelectedGuiCommands(
        WorkbenchCommandBlock dragAnchor,
        WorkbenchCommandInsertionTarget target)
    {
        MoveGuiCommands(GetGuiCommandsForDrag(dragAnchor), target);
    }

    public void MoveGuiCommands(
        IReadOnlyList<WorkbenchCommandBlock> movingCommands,
        WorkbenchCommandInsertionTarget target)
    {
        if (SelectedGuiTestcase is null)
        {
            throw new InvalidOperationException("No testcase is selected.");
        }

        if (movingCommands is null)
        {
            throw new ArgumentNullException(nameof(movingCommands));
        }

        var selectedBlocks = TopLevelSelectedCommandBlocks(
            movingCommands
                .Where(commandBlock => commandBlock is not null)
                .OrderBy(commandBlock => commandBlock.SourceLineStart)
                .ToArray());
        if (selectedBlocks.Count == 0)
        {
            return;
        }

        if (selectedBlocks.Count == 1)
        {
            MoveGuiCommand(selectedBlocks[0], target);
            return;
        }

        var testcaseName = SelectedGuiTestcase.Name;
        var movedLineCount = selectedBlocks.Sum(commandBlock =>
            commandBlock.SourceLineEnd - commandBlock.SourceLineStart + 1);
        var result = WorkbenchYamlCommandMover.MoveMany(
            EditorText,
            SelectedGuiTestcase,
            selectedBlocks,
            target);
        _selectedGuiCommandLineNumbers.Clear();
        _guiBulkSelectionAnchorLineNumber = null;
        UpdateEditorText(result.Text);
        SelectedGuiTestcase = GuiModel.Testcases.FirstOrDefault(testcase =>
            testcase.Name == testcaseName)
            ?? GuiModel.Testcases.FirstOrDefault();

        var movedBlocks = FindMovedCommandBlocks(
            SelectedGuiTestcase,
            selectedBlocks,
            result.InsertedLineNumber,
            movedLineCount);
        foreach (var movedBlock in movedBlocks)
        {
            _selectedGuiCommandLineNumbers.Add(movedBlock.SourceLineStart);
        }

        SelectedGuiCommand = movedBlocks.FirstOrDefault()
            ?? SelectedGuiTestcase?.Phases
                .SelectMany(phaseModel => FlattenCommands(phaseModel.Blocks))
                .FirstOrDefault(commandBlock => commandBlock.SourceLineStart >= result.InsertedLineNumber);
        if (SelectedGuiCommand is not null)
        {
            CurrentLineNumber = SelectedGuiCommand.SourceLineStart;
            CurrentLocationText = $"Line {CurrentLineNumber} - {SelectedGuiCommand.CommandType}";
        }

        UpdateGuiCurrentExecutionBlock();
        UpdateGuiSelectionMarkers();
    }

    public void DeleteGuiCommand(WorkbenchCommandBlock commandBlock)
    {
        if (SelectedGuiTestcase is null)
        {
            throw new InvalidOperationException("No testcase is selected.");
        }

        if (commandBlock is null)
        {
            throw new ArgumentNullException(nameof(commandBlock));
        }

        var testcaseName = SelectedGuiTestcase.Name;
        var deletedLineNumber = commandBlock.SourceLineStart;
        var result = WorkbenchYamlCommandDeleter.Delete(EditorText, commandBlock);
        _selectedGuiCommandLineNumbers.Clear();
        _guiBulkSelectionAnchorLineNumber = null;
        UpdateEditorText(result.Text);
        SelectedGuiTestcase = GuiModel.Testcases.FirstOrDefault(testcase =>
            testcase.Name == testcaseName)
            ?? GuiModel.Testcases.FirstOrDefault();
        SelectedGuiCommand = FindCommandNearDeletedLine(SelectedGuiTestcase, deletedLineNumber);
        if (SelectedGuiCommand is not null)
        {
            CurrentLineNumber = SelectedGuiCommand.SourceLineStart;
            CurrentLocationText = $"Line {CurrentLineNumber} - {SelectedGuiCommand.CommandType}";
        }
        else
        {
            ClearCurrentExecutionLocation();
        }

        UpdateGuiCurrentExecutionBlock();
        UpdateGuiSelectionMarkers();
    }

    public void DeleteSelectedGuiCommands()
    {
        if (SelectedGuiTestcase is null)
        {
            throw new InvalidOperationException("No testcase is selected.");
        }

        var selectedBlocks = TopLevelSelectedCommandBlocks(
            AllGuiCommandBlocks()
                .Where(commandBlock => _selectedGuiCommandLineNumbers.Contains(commandBlock.SourceLineStart))
                .OrderBy(commandBlock => commandBlock.SourceLineStart)
                .ToArray());
        if (selectedBlocks.Count == 0)
        {
            return;
        }

        var testcaseName = SelectedGuiTestcase.Name;
        var deletedLineNumber = selectedBlocks.Min(commandBlock => commandBlock.SourceLineStart);
        var editorText = EditorText;
        foreach (var commandBlock in selectedBlocks.OrderByDescending(commandBlock => commandBlock.SourceLineStart))
        {
            editorText = WorkbenchYamlCommandDeleter.Delete(editorText, commandBlock).Text;
        }

        _selectedGuiCommandLineNumbers.Clear();
        _guiBulkSelectionAnchorLineNumber = null;
        UpdateEditorText(editorText);
        SelectedGuiTestcase = GuiModel.Testcases.FirstOrDefault(testcase =>
            testcase.Name == testcaseName)
            ?? GuiModel.Testcases.FirstOrDefault();
        SelectedGuiCommand = FindCommandNearDeletedLine(SelectedGuiTestcase, deletedLineNumber);
        if (SelectedGuiCommand is not null)
        {
            CurrentLineNumber = SelectedGuiCommand.SourceLineStart;
            CurrentLocationText = $"Line {CurrentLineNumber} - {SelectedGuiCommand.CommandType}";
        }
        else
        {
            ClearCurrentExecutionLocation();
        }

        UpdateGuiCurrentExecutionBlock();
        UpdateGuiSelectionMarkers();
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

    public void ShowGuiCommandMovePreview(
        IReadOnlyList<WorkbenchCommandBlock> movingCommands,
        WorkbenchCommandInsertionTarget target)
    {
        var commandText = movingCommands.Count == 1
            ? movingCommands[0].CommandType
            : $"{movingCommands.Count} commands";
        ShowGuiDropPreview(commandText, target, "Move");
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
                target.ReferenceCommand.IsDragInsertionTarget = true;
                target.ReferenceCommand.DragInsertionText =
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
        else if (runEvent.Status == "aborted")
        {
            RunStatus = "Aborted";
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

    private static void DispatchExecutionUpdate(
        Action<Action>? dispatchExecutionUpdate,
        Action update)
    {
        try
        {
            if (dispatchExecutionUpdate is null)
            {
                update();
            }
            else
            {
                dispatchExecutionUpdate(update);
            }
        }
        catch
        {
            // Streaming UI updates should not cancel or corrupt an engine run.
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

    private void UpdateGuiSelectionMarkers()
    {
        var validLineNumbers = AllGuiCommandBlocks()
            .Select(commandBlock => commandBlock.SourceLineStart)
            .ToHashSet();
        _selectedGuiCommandLineNumbers.RemoveWhere(lineNumber => !validLineNumbers.Contains(lineNumber));
        foreach (var commandBlock in AllGuiCommandBlocks())
        {
            commandBlock.IsSelectedForBulkAction =
                _selectedGuiCommandLineNumbers.Contains(commandBlock.SourceLineStart);
        }
    }

    private IEnumerable<WorkbenchCommandBlock> AllGuiCommandBlocks()
    {
        return SelectedGuiTestcase is null
            ? Array.Empty<WorkbenchCommandBlock>()
            : SelectedGuiTestcase.Phases.SelectMany(phase => FlattenCommands(phase.Blocks));
    }

    private IReadOnlyList<string> SelectedGuiTestcaseRunNamesInFileOrder()
    {
        return GuiModel.Testcases
            .Where(testcase => _selectedGuiTestcaseRunNames.Contains(testcase.Name))
            .Select(testcase => testcase.Name)
            .ToArray();
    }

    private void PruneSelectedGuiTestcaseRunNames()
    {
        var validNames = GuiModel.Testcases
            .Select(testcase => testcase.Name)
            .ToHashSet(StringComparer.Ordinal);
        _selectedGuiTestcaseRunNames.RemoveWhere(name => !validNames.Contains(name));
    }

    private static IReadOnlyList<WorkbenchCommandBlock> TopLevelSelectedCommandBlocks(
        IReadOnlyList<WorkbenchCommandBlock> selectedBlocks)
    {
        var result = new List<WorkbenchCommandBlock>();
        foreach (var commandBlock in selectedBlocks)
        {
            if (result.Any(parent =>
                    commandBlock.SourceLineStart > parent.SourceLineStart
                    && commandBlock.SourceLineEnd <= parent.SourceLineEnd))
            {
                continue;
            }

            result.Add(commandBlock);
        }

        return result;
    }

    private static WorkbenchCommandBlock? FindCommandNearDeletedLine(
        WorkbenchGuiTestcase? testcase,
        int deletedLineNumber)
    {
        var commandBlocks = testcase?.Phases
            .SelectMany(phase => FlattenCommands(phase.Blocks))
            .OrderBy(commandBlock => commandBlock.SourceLineStart)
            .ToArray() ?? Array.Empty<WorkbenchCommandBlock>();
        return commandBlocks.FirstOrDefault(commandBlock =>
                commandBlock.SourceLineStart >= deletedLineNumber)
            ?? commandBlocks.LastOrDefault();
    }

    private static IReadOnlyList<WorkbenchCommandBlock> FindMovedCommandBlocks(
        WorkbenchGuiTestcase? testcase,
        IReadOnlyList<WorkbenchCommandBlock> sourceBlocks,
        int insertedLineNumber,
        int movedLineCount)
    {
        var candidates = testcase?.Phases
            .SelectMany(phase => FlattenCommands(phase.Blocks))
            .OrderBy(commandBlock => commandBlock.SourceLineStart)
            .ToArray() ?? Array.Empty<WorkbenchCommandBlock>();
        var movedBlocks = new List<WorkbenchCommandBlock>();
        var expectedLine = insertedLineNumber;
        foreach (var sourceBlock in sourceBlocks)
        {
            var movedBlock = candidates.FirstOrDefault(commandBlock =>
                commandBlock.SourceLineStart == expectedLine
                && commandBlock.CommandType == sourceBlock.CommandType);
            if (movedBlock is not null)
            {
                movedBlocks.Add(movedBlock);
            }

            expectedLine += sourceBlock.SourceLineEnd - sourceBlock.SourceLineStart + 1;
        }

        if (movedBlocks.Count == sourceBlocks.Count)
        {
            return movedBlocks;
        }

        var movedEndLineNumber = insertedLineNumber + movedLineCount - 1;
        return candidates
            .Where(commandBlock =>
                commandBlock.SourceLineStart >= insertedLineNumber
                && commandBlock.SourceLineStart <= movedEndLineNumber)
            .Take(sourceBlocks.Count)
            .ToArray();
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

    private static WorkbenchCommandBlock? FindCommandByLineAndType(
        WorkbenchGuiTestcase? testcase,
        int? sourceLineStart,
        string? commandType)
    {
        if (testcase is null || sourceLineStart is null || string.IsNullOrWhiteSpace(commandType))
        {
            return null;
        }

        return testcase.Phases
            .SelectMany(phase => FlattenCommands(phase.Blocks))
            .FirstOrDefault(commandBlock =>
                commandBlock.SourceLineStart == sourceLineStart
                && commandBlock.CommandType == commandType);
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
