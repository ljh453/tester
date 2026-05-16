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

    protected void SetField<T>(
        ref T field,
        T value,
        [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
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

public sealed class WorkbenchCommandBlock : WorkbenchDragInsertionPreviewTarget
{
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

    public IReadOnlyList<WorkbenchCommandBlock> Children { get; }

    public WorkbenchCommandInsideDropTarget InsideDropTarget { get; }

    public bool IsExpanded { get; set; } = true;

    public bool IsCurrentExecution { get; set; }

    public bool IsBreakpoint { get; set; }

    public string BreakpointMarker => IsBreakpoint ? MainWorkbenchViewModel.ActiveBreakpointMarker : string.Empty;

    public bool IsFoldable => Children.Count > 0;

    public bool CanInsertInside => CommandType == "for";

    public string LineRangeText => SourceLineStart == SourceLineEnd
        ? $"L{SourceLineStart}"
        : $"L{SourceLineStart}-L{SourceLineEnd}";

    public string FoldSummary => Children.Count == 1
        ? "1 nested command hidden"
        : $"{Children.Count} nested commands hidden";
}
