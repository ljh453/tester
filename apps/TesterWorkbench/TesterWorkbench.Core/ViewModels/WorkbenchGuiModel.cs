namespace TesterWorkbench.Core.ViewModels;

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

public sealed class WorkbenchGuiPhase
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
    }

    public string Name { get; }

    public string YamlName { get; }

    public int SourceLineStart { get; }

    public int SourceLineEnd { get; }

    public bool HasYamlSection { get; }

    public IReadOnlyList<WorkbenchCommandBlock> Blocks { get; }

    public string CountText => Blocks.Count == 1
        ? "1 command"
        : $"{Blocks.Count} commands";
}

public sealed class WorkbenchCommandBlock
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

    public bool IsExpanded { get; set; } = true;

    public bool IsCurrentExecution { get; set; }

    public bool IsFoldable => Children.Count > 0;

    public string LineRangeText => SourceLineStart == SourceLineEnd
        ? $"L{SourceLineStart}"
        : $"L{SourceLineStart}-L{SourceLineEnd}";

    public string FoldSummary => Children.Count == 1
        ? "1 nested command hidden"
        : $"{Children.Count} nested commands hidden";
}
