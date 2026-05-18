using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TesterWorkbench.Core.ViewModels;

public abstract class WorkbenchDragInsertionPreviewTarget : INotifyPropertyChanged
{
    private bool _isDragInsertionTarget;
    private string _dragInsertionText = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsDragInsertionTarget
    {
        get => _isDragInsertionTarget;
        set => SetField(ref _isDragInsertionTarget, value);
    }

    public string DragInsertionText
    {
        get => _dragInsertionText;
        set => SetField(ref _dragInsertionText, value);
    }

    protected bool SetField<T>(
        ref T field,
        T value,
        [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class WorkbenchPhaseStartDropTarget : WorkbenchDragInsertionPreviewTarget
{
    public WorkbenchPhaseStartDropTarget(WorkbenchGuiPhase phase)
    {
        Phase = phase;
    }

    public WorkbenchGuiPhase Phase { get; }
}

public sealed class WorkbenchPhaseEndDropTarget : WorkbenchDragInsertionPreviewTarget
{
    public WorkbenchPhaseEndDropTarget(WorkbenchGuiPhase phase)
    {
        Phase = phase;
    }

    public WorkbenchGuiPhase Phase { get; }
}

public sealed class WorkbenchCommandInsideDropTarget : WorkbenchDragInsertionPreviewTarget
{
    public WorkbenchCommandInsideDropTarget(WorkbenchCommandBlock commandBlock)
    {
        CommandBlock = commandBlock;
    }

    public WorkbenchCommandBlock CommandBlock { get; }
}

public sealed class WorkbenchGuiModel
{
    public WorkbenchGuiModel(IReadOnlyList<WorkbenchGuiTestcase> testcases)
    {
        Testcases = testcases;
    }

    public IReadOnlyList<WorkbenchGuiTestcase> Testcases { get; }

    public static WorkbenchGuiModel Empty { get; } =
        new(Array.Empty<WorkbenchGuiTestcase>());
}

public sealed class WorkbenchGuiTestcase
{
    public WorkbenchGuiTestcase(
        string name,
        string description,
        string tagsText,
        string failurePolicy,
        string lineRangeText,
        int sourceLineStart,
        int sourceLineEnd,
        IReadOnlyList<WorkbenchGuiPhase> phases)
    {
        Name = name;
        Description = description;
        TagsText = tagsText;
        FailurePolicy = failurePolicy;
        LineRangeText = lineRangeText;
        SourceLineStart = sourceLineStart;
        SourceLineEnd = sourceLineEnd;
        Phases = phases;
    }

    public string Name { get; }

    public string Description { get; }

    public string TagsText { get; }

    public string FailurePolicy { get; }

    public string LineRangeText { get; }

    public int SourceLineStart { get; }

    public int SourceLineEnd { get; }

    public IReadOnlyList<WorkbenchGuiPhase> Phases { get; }
}

public sealed class WorkbenchGuiPhase : WorkbenchDragInsertionPreviewTarget
{
    public WorkbenchGuiPhase(
        string name,
        string yamlName,
        int sourceLineStart,
        int sourceLineEnd,
        bool hasYamlSection,
        IReadOnlyList<WorkbenchCommandBlock> blocks)
    {
        Name = name;
        YamlName = yamlName;
        SourceLineStart = sourceLineStart;
        SourceLineEnd = sourceLineEnd;
        HasYamlSection = hasYamlSection;
        Blocks = blocks;
        StartDropTarget = new WorkbenchPhaseStartDropTarget(this);
        EndDropTarget = new WorkbenchPhaseEndDropTarget(this);
    }

    public string Name { get; }

    public string YamlName { get; }

    public int SourceLineStart { get; }

    public int SourceLineEnd { get; }

    public bool HasYamlSection { get; }

    public IReadOnlyList<WorkbenchCommandBlock> Blocks { get; }

    public WorkbenchPhaseStartDropTarget StartDropTarget { get; }

    public WorkbenchPhaseEndDropTarget EndDropTarget { get; }

    public string CountText => Blocks.Count == 1
        ? "1 command"
        : $"{Blocks.Count} commands";
}

public sealed class WorkbenchCommandArgument
{
    public WorkbenchCommandArgument(
        WorkbenchCommandArgumentDefinition definition,
        string value,
        int sourceLine,
        IReadOnlyList<string>? suggestions = null)
    {
        Definition = definition;
        Value = value;
        SourceLine = sourceLine;
        Suggestions = suggestions ?? definition.Suggestions;
    }

    public WorkbenchCommandArgumentDefinition Definition { get; }

    public string Name => Definition.Name;

    public WorkbenchCommandArgumentKind Kind => Definition.Kind;

    public bool IsRequired => Definition.IsRequired;

    public bool IsScalarEditable => Definition.IsScalarEditable;

    public bool IsComplexEditable =>
        Kind is WorkbenchCommandArgumentKind.Map or WorkbenchCommandArgumentKind.List;

    public WorkbenchCommandAutocompleteKind AutocompleteKind => Definition.AutocompleteKind;

    public IReadOnlyList<string> Suggestions { get; }

    public bool HasSuggestions => Suggestions.Count > 0;

    public bool IsMissingRequired => IsRequired && string.IsNullOrWhiteSpace(Value);

    public bool IsExplicitlyConfigured => SourceLine > 0;

    public bool IsVisibleByDefault => IsRequired || IsExplicitlyConfigured;

    public string RequirementText => IsMissingRequired
        ? "missing required"
        : Definition.RequirementText;

    public string Value { get; }

    public int SourceLine { get; }
}

public sealed class WorkbenchCommandBlock : WorkbenchDragInsertionPreviewTarget
{
    private bool _isExpanded = true;
    private bool _isCurrentExecution;
    private bool _isBreakpoint;
    private bool _isSelectedForBulkAction;

    public WorkbenchCommandBlock(
        string displayIndex,
        string commandType,
        string displayType,
        string summary,
        int sourceLineStart,
        int sourceLineEnd,
        int depth,
        string sourcePreview,
        string accentColor,
        IReadOnlyList<WorkbenchCommandArgument> arguments,
        IReadOnlyList<WorkbenchCommandBlock> children)
    {
        DisplayIndex = displayIndex;
        CommandType = commandType;
        DisplayType = displayType;
        Summary = summary;
        SourceLineStart = sourceLineStart;
        SourceLineEnd = sourceLineEnd;
        Depth = depth;
        SourcePreview = sourcePreview;
        AccentColor = accentColor;
        Arguments = arguments;
        Children = children;
        InsideDropTarget = new WorkbenchCommandInsideDropTarget(this);
    }

    public string DisplayIndex { get; }

    public string CommandType { get; }

    public string DisplayType { get; }

    public string Summary { get; }

    public int SourceLineStart { get; }

    public int SourceLineEnd { get; }

    public int Depth { get; }

    public string SourcePreview { get; }

    public string AccentColor { get; }

    public IReadOnlyList<WorkbenchCommandArgument> Arguments { get; }

    public IReadOnlyList<WorkbenchCommandBlock> Children { get; }

    public WorkbenchCommandInsideDropTarget InsideDropTarget { get; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetField(ref _isExpanded, value);
    }

    public bool IsCurrentExecution
    {
        get => _isCurrentExecution;
        set => SetField(ref _isCurrentExecution, value);
    }

    public bool IsBreakpoint
    {
        get => _isBreakpoint;
        set
        {
            if (SetField(ref _isBreakpoint, value))
            {
                OnPropertyChanged(nameof(BreakpointMarker));
            }
        }
    }

    public bool IsSelectedForBulkAction
    {
        get => _isSelectedForBulkAction;
        set => SetField(ref _isSelectedForBulkAction, value);
    }

    public string BreakpointMarker => IsBreakpoint ? MainWorkbenchViewModel.ActiveBreakpointMarker : string.Empty;

    public bool IsFoldable => Children.Count > 0;

    public bool HasValidationIssues => Arguments.Any(argument => argument.IsMissingRequired);

    public string ValidationSummary => HasValidationIssues
        ? $"Missing required: {string.Join(", ", Arguments.Where(argument => argument.IsMissingRequired).Select(argument => argument.Name))}"
        : "Ready";

    public bool CanInsertInside => CommandType == "for";

    public string LineRangeText => SourceLineStart == SourceLineEnd
        ? $"L{SourceLineStart}"
        : $"L{SourceLineStart}-L{SourceLineEnd}";

    public string FoldSummary => Children.Count == 1
        ? "1 nested command hidden"
        : $"{Children.Count} nested commands hidden";
}
