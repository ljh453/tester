namespace TesterWorkbench.Core.ViewModels;

public sealed class WorkbenchGuiSuggestionContext
{
    public WorkbenchGuiSuggestionContext(
        IEnumerable<string>? variableNames = null,
        IEnumerable<string>? functionNames = null,
        IEnumerable<string>? toolReferenceNames = null)
    {
        VariableNames = Normalize(variableNames);
        FunctionNames = Normalize(functionNames);
        ToolReferenceNames = Normalize(toolReferenceNames);
    }

    public IReadOnlyList<string> VariableNames { get; }

    public IReadOnlyList<string> FunctionNames { get; }

    public IReadOnlyList<string> ToolReferenceNames { get; }

    public static WorkbenchGuiSuggestionContext Empty { get; } = new();

    public WorkbenchGuiSuggestionContext Merge(WorkbenchGuiSuggestionContext? other)
    {
        if (other is null)
        {
            return this;
        }

        return new WorkbenchGuiSuggestionContext(
            VariableNames.Concat(other.VariableNames),
            FunctionNames.Concat(other.FunctionNames),
            ToolReferenceNames.Concat(other.ToolReferenceNames));
    }

    public IReadOnlyList<string> SuggestionsFor(WorkbenchCommandArgumentDefinition argument)
    {
        var suggestions = new List<string>();
        suggestions.AddRange(argument.Suggestions);
        switch (argument.AutocompleteKind)
        {
            case WorkbenchCommandAutocompleteKind.Variables:
                suggestions.AddRange(VariableNames.Select(variable => $"${{{variable}}}"));
                suggestions.AddRange(VariableNames);
                break;
            case WorkbenchCommandAutocompleteKind.Functions:
                suggestions.AddRange(FunctionNames);
                break;
            case WorkbenchCommandAutocompleteKind.ToolProfiles:
                suggestions.AddRange(ToolReferenceNames);
                break;
        }

        return Normalize(suggestions);
    }

    private static IReadOnlyList<string> Normalize(IEnumerable<string>? values)
    {
        return (values ?? Array.Empty<string>())
            .Select(value => value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }
}
