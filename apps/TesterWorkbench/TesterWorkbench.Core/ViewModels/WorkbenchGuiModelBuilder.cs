using System.Text.RegularExpressions;

namespace TesterWorkbench.Core.ViewModels;

public static class WorkbenchGuiModelBuilder
{
    private static readonly Regex CommandRegex = new(@"^-\s+([A-Za-z0-9_.-]+)\s*:", RegexOptions.Compiled);
    private static readonly Regex KeyValueRegex = new(@"^([A-Za-z0-9_.-]+)\s*:\s*(.*)$", RegexOptions.Compiled);

    public static WorkbenchGuiModel Build(
        string yamlText,
        WorkbenchGuiSuggestionContext? externalSuggestionContext = null)
    {
        var lines = SplitLines(yamlText);
        var suggestionContext = BuildSuggestionContext(lines)
            .Merge(externalSuggestionContext);
        var testcases = new List<WorkbenchGuiTestcase>();
        var testcasesLineIndex = FindRootSection(lines, "testcases");
        if (testcasesLineIndex < 0)
        {
            return WorkbenchGuiModel.Empty;
        }

        var testcaseStarts = FindTestcaseStarts(lines, testcasesLineIndex + 1);
        for (var index = 0; index < testcaseStarts.Count; index++)
        {
            var startLineIndex = testcaseStarts[index];
            var endLineIndex = index + 1 < testcaseStarts.Count
                ? testcaseStarts[index + 1] - 1
                : FindSectionEnd(lines, startLineIndex, 2);
            testcases.Add(BuildTestcase(lines, startLineIndex, endLineIndex, suggestionContext));
        }

        return new WorkbenchGuiModel(testcases);
    }

    public static WorkbenchGuiSuggestionContext BuildSuggestionContext(string yamlText)
    {
        return BuildSuggestionContext(SplitLines(yamlText));
    }

    private static WorkbenchGuiTestcase BuildTestcase(
        IReadOnlyList<YamlLine> lines,
        int startLineIndex,
        int endLineIndex,
        WorkbenchGuiSuggestionContext suggestionContext)
    {
        var name = ValueAfterPrefix(lines[startLineIndex].Trimmed, "- name:");
        var description = FindScalar(lines, startLineIndex + 1, endLineIndex, 4, "description");
        var tagsText = NormalizeList(FindScalar(lines, startLineIndex + 1, endLineIndex, 4, "tags"));
        var failurePolicy = FindScalar(lines, startLineIndex + 1, endLineIndex, 4, "on_step_failure");
        var phases = new[]
        {
            BuildPhase("Preconditions", "preconditions", lines, startLineIndex, endLineIndex, suggestionContext),
            BuildPhase("Steps", "steps", lines, startLineIndex, endLineIndex, suggestionContext),
            BuildPhase("Postconditions", "postconditions", lines, startLineIndex, endLineIndex, suggestionContext)
        };

        return new WorkbenchGuiTestcase(
            string.IsNullOrWhiteSpace(name) ? "unnamed_testcase" : name,
            description,
            tagsText,
            string.IsNullOrWhiteSpace(failurePolicy) ? "stop" : failurePolicy,
            LineRange(startLineIndex + 1, endLineIndex + 1),
            startLineIndex + 1,
            endLineIndex + 1,
            phases);
    }

    private static WorkbenchGuiPhase BuildPhase(
        string displayName,
        string yamlName,
        IReadOnlyList<YamlLine> lines,
        int testcaseStartLineIndex,
        int testcaseEndLineIndex,
        WorkbenchGuiSuggestionContext suggestionContext)
    {
        var phaseLineIndex = FindPhaseLine(lines, testcaseStartLineIndex, testcaseEndLineIndex, yamlName);
        if (phaseLineIndex < 0)
        {
            return new WorkbenchGuiPhase(
                displayName,
                yamlName,
                0,
                0,
                hasYamlSection: false,
                Array.Empty<WorkbenchCommandBlock>());
        }

        var phaseEndLineIndex = FindPhaseEnd(lines, phaseLineIndex + 1, testcaseEndLineIndex);
        var tempBlocks = FindCommandBlocks(lines, phaseLineIndex + 1, phaseEndLineIndex);
        AssignEndLines(tempBlocks, phaseEndLineIndex);
        var rootTempBlocks = BuildCommandTree(tempBlocks);
        AssignDisplayIndexes(rootTempBlocks, string.Empty);
        var rootBlocks = rootTempBlocks
            .Select(block => ToCommandBlock(lines, block, suggestionContext))
            .ToArray();

        return new WorkbenchGuiPhase(
            displayName,
            yamlName,
            phaseLineIndex + 1,
            phaseEndLineIndex + 1,
            hasYamlSection: true,
            rootBlocks);
    }

    private static List<TempCommandBlock> FindCommandBlocks(
        IReadOnlyList<YamlLine> lines,
        int startLineIndex,
        int endLineIndex)
    {
        var blocks = new List<TempCommandBlock>();
        for (var index = startLineIndex; index <= endLineIndex; index++)
        {
            var line = lines[index];
            var match = CommandRegex.Match(line.Trimmed);
            if (!match.Success)
            {
                continue;
            }

            blocks.Add(new TempCommandBlock(
                match.Groups[1].Value,
                index,
                line.Indent));
        }

        return blocks;
    }

    private static void AssignEndLines(
        IReadOnlyList<TempCommandBlock> tempBlocks,
        int phaseEndLineIndex)
    {
        for (var index = 0; index < tempBlocks.Count; index++)
        {
            var block = tempBlocks[index];
            var endLineIndex = phaseEndLineIndex;
            for (var nextIndex = index + 1; nextIndex < tempBlocks.Count; nextIndex++)
            {
                var nextBlock = tempBlocks[nextIndex];
                if (nextBlock.Indent <= block.Indent)
                {
                    endLineIndex = nextBlock.StartLineIndex - 1;
                    break;
                }
            }

            block.EndLineIndex = TrimTrailingBlankLines(block.StartLineIndex, endLineIndex);
        }
    }

    private static IReadOnlyList<TempCommandBlock> BuildCommandTree(
        IReadOnlyList<TempCommandBlock> tempBlocks)
    {
        var root = new List<TempCommandBlock>();
        var stack = new Stack<TempCommandBlock>();
        foreach (var block in tempBlocks)
        {
            while (stack.Count > 0 && stack.Peek().Indent >= block.Indent)
            {
                stack.Pop();
            }

            if (stack.TryPeek(out var parent))
            {
                parent.Children.Add(block);
                block.Depth = parent.Depth + 1;
            }
            else
            {
                root.Add(block);
                block.Depth = 1;
            }

            stack.Push(block);
        }

        return root;
    }

    private static WorkbenchCommandBlock ToCommandBlock(
        IReadOnlyList<YamlLine> lines,
        TempCommandBlock tempBlock,
        WorkbenchGuiSuggestionContext suggestionContext)
    {
        var children = tempBlock.Children
            .Select(child => ToCommandBlock(lines, child, suggestionContext))
            .ToArray();

        return new WorkbenchCommandBlock(
            tempBlock.DisplayIndex,
            tempBlock.CommandType,
            DisplayCommandType(tempBlock.CommandType).ToUpperInvariant(),
            BuildSummary(lines, tempBlock),
            tempBlock.StartLineIndex + 1,
            tempBlock.EndLineIndex + 1,
            tempBlock.Depth,
            BuildSourcePreview(lines, tempBlock.StartLineIndex, tempBlock.EndLineIndex),
            AccentColorFor(tempBlock.CommandType),
            BuildArguments(lines, tempBlock, suggestionContext),
            children);
    }

    private static IReadOnlyList<WorkbenchCommandArgument> BuildArguments(
        IReadOnlyList<YamlLine> lines,
        TempCommandBlock tempBlock,
        WorkbenchGuiSuggestionContext suggestionContext)
    {
        var definition = WorkbenchCommandCatalog.Find(tempBlock.CommandType);
        if (definition is null)
        {
            return BuildUnknownCommandArguments(lines, tempBlock);
        }

        return definition.Arguments
            .Select(argumentDefinition =>
            {
                var argumentValue = ReadArgumentValue(lines, tempBlock, argumentDefinition);
                return new WorkbenchCommandArgument(
                    argumentDefinition,
                    argumentValue.Value,
                    argumentValue.SourceLine,
                    suggestionContext.SuggestionsFor(argumentDefinition));
            })
            .ToArray();
    }

    private static IReadOnlyList<WorkbenchCommandArgument> BuildUnknownCommandArguments(
        IReadOnlyList<YamlLine> lines,
        TempCommandBlock tempBlock)
    {
        var arguments = new List<WorkbenchCommandArgument>();
        var expectedIndent = tempBlock.Indent + 4;
        for (var index = tempBlock.StartLineIndex + 1; index <= tempBlock.EndLineIndex; index++)
        {
            var line = lines[index];
            if (line.Indent != expectedIndent)
            {
                continue;
            }

            var match = KeyValueRegex.Match(line.Trimmed);
            if (!match.Success)
            {
                continue;
            }

            var definition = new WorkbenchCommandArgumentDefinition(
                match.Groups[1].Value,
                string.IsNullOrWhiteSpace(match.Groups[2].Value)
                    ? WorkbenchCommandArgumentKind.Map
                    : WorkbenchCommandArgumentKind.Value,
                isRequired: false,
                WorkbenchCommandAutocompleteKind.None,
                Array.Empty<string>());
            arguments.Add(new WorkbenchCommandArgument(
                definition,
                StripQuotes(match.Groups[2].Value.Trim()),
                index + 1));
        }

        return arguments;
    }

    private static void AssignDisplayIndexes(
        IReadOnlyList<TempCommandBlock> blocks,
        string parentIndex)
    {
        for (var index = 0; index < blocks.Count; index++)
        {
            var displayIndex = string.IsNullOrWhiteSpace(parentIndex)
                ? (index + 1).ToString()
                : $"{parentIndex}.{index + 1}";
            blocks[index].DisplayIndex = displayIndex;
            AssignDisplayIndexes(blocks[index].Children, displayIndex);
        }
    }

    private static string BuildSummary(
        IReadOnlyList<YamlLine> lines,
        TempCommandBlock tempBlock)
    {
        var commandType = tempBlock.CommandType;
        return commandType switch
        {
            "set" => SummaryFromParts(
                ReadArgument(lines, tempBlock, "var"),
                "=",
                ReadArgument(lines, tempBlock, "value")),
            "delay" => SummaryFromParts(ReadArgument(lines, tempBlock, "ms"), "ms"),
            "for" => SummaryFromParts(
                "each",
                ReadArgument(lines, tempBlock, "each"),
                "as",
                ReadArgument(lines, tempBlock, "as")),
            "if" => ReadArgument(lines, tempBlock, "condition"),
            "call" => $"{DefaultIfBlank(ReadArgument(lines, tempBlock, "function"), "function")}(...)",
            "log.text" => ReadArgument(lines, tempBlock, "text"),
            "log.value" => SummaryFromParts(
                ReadArgument(lines, tempBlock, "name"),
                "=",
                ReadArgument(lines, tempBlock, "value")),
            "assert.eq" => SummaryFromParts(
                ReadArgument(lines, tempBlock, "left"),
                "==",
                ReadArgument(lines, tempBlock, "right")),
            "assert.gt" => SummaryFromParts(
                ReadArgument(lines, tempBlock, "left"),
                ">",
                ReadArgument(lines, tempBlock, "right")),
            "assert.fail" => ReadArgument(lines, tempBlock, "message"),
            _ => commandType
        };
    }

    private static WorkbenchGuiSuggestionContext BuildSuggestionContext(IReadOnlyList<YamlLine> lines)
    {
        return new WorkbenchGuiSuggestionContext(
            ExtractVariableNames(lines),
            ExtractFunctionNames(lines),
            ExtractToolReferenceNames(lines));
    }

    private static IReadOnlyList<string> ExtractFunctionNames(IReadOnlyList<YamlLine> lines)
    {
        var functionsLineIndex = FindRootSection(lines, "functions");
        if (functionsLineIndex < 0)
        {
            return Array.Empty<string>();
        }

        var functionNames = new List<string>();
        var endLineIndex = FindRootSectionEnd(lines, functionsLineIndex);
        for (var index = functionsLineIndex + 1; index <= endLineIndex; index++)
        {
            var line = lines[index];
            var match = KeyValueRegex.Match(line.Trimmed);
            if (line.Indent == 2 && match.Success)
            {
                functionNames.Add(match.Groups[1].Value);
            }
        }

        return functionNames;
    }

    private static IReadOnlyList<string> ExtractVariableNames(IReadOnlyList<YamlLine> lines)
    {
        var variableNames = new List<string>();
        foreach (var line in lines)
        {
            var match = KeyValueRegex.Match(line.Trimmed);
            if (!match.Success)
            {
                continue;
            }

            var key = match.Groups[1].Value;
            var value = StripQuotes(match.Groups[2].Value.Trim());
            if (key is "var" or "as" or "save_as")
            {
                variableNames.Add(value);
            }
            else if (key is "params" or "returns")
            {
                variableNames.AddRange(ParseInlineList(value));
            }
        }

        return variableNames;
    }

    private static IReadOnlyList<string> ExtractToolReferenceNames(IReadOnlyList<YamlLine> lines)
    {
        var toolNames = new List<string>();
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            if (line.Trimmed is not "devices:" and not "ports:")
            {
                continue;
            }

            var parentIndent = line.Indent;
            for (var childIndex = index + 1; childIndex < lines.Count; childIndex++)
            {
                var child = lines[childIndex];
                if (!string.IsNullOrWhiteSpace(child.Trimmed) && child.Indent <= parentIndent)
                {
                    break;
                }

                var match = KeyValueRegex.Match(child.Trimmed);
                if (child.Indent == parentIndent + 2 && match.Success)
                {
                    toolNames.Add(match.Groups[1].Value);
                }
            }
        }

        return toolNames;
    }

    private static IReadOnlyList<string> ParseInlineList(string value)
    {
        var trimmed = value.Trim();
        if (!trimmed.StartsWith("[", StringComparison.Ordinal)
            || !trimmed.EndsWith("]", StringComparison.Ordinal))
        {
            return Array.Empty<string>();
        }

        return trimmed[1..^1]
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(StripQuotes)
            .ToArray();
    }

    private static string ReadArgument(
        IReadOnlyList<YamlLine> lines,
        TempCommandBlock tempBlock,
        string key)
    {
        for (var index = tempBlock.StartLineIndex + 1; index <= tempBlock.EndLineIndex; index++)
        {
            var line = lines[index];
            if (line.Indent != tempBlock.Indent + 4)
            {
                continue;
            }

            var match = KeyValueRegex.Match(line.Trimmed);
            if (match.Success && match.Groups[1].Value == key)
            {
                return StripQuotes(match.Groups[2].Value.Trim());
            }
        }

        return string.Empty;
    }

    private static (string Value, int SourceLine) ReadArgumentValue(
        IReadOnlyList<YamlLine> lines,
        TempCommandBlock tempBlock,
        WorkbenchCommandArgumentDefinition argumentDefinition)
    {
        var argumentIndent = tempBlock.Indent + 4;
        for (var index = tempBlock.StartLineIndex + 1; index <= tempBlock.EndLineIndex; index++)
        {
            var line = lines[index];
            if (line.Indent != argumentIndent)
            {
                continue;
            }

            var match = KeyValueRegex.Match(line.Trimmed);
            if (!match.Success || match.Groups[1].Value != argumentDefinition.Name)
            {
                continue;
            }

            var suffix = match.Groups[2].Value.Trim();
            if (argumentDefinition.IsScalarEditable)
            {
                return (StripQuotes(suffix), index + 1);
            }

            if (!string.IsNullOrWhiteSpace(suffix) && suffix is not "{}" and not "[]" and not "null")
            {
                return (StripQuotes(suffix), index + 1);
            }

            return (ReadNestedArgumentBody(lines, index, tempBlock.EndLineIndex, argumentIndent + 2), index + 1);
        }

        return (string.Empty, 0);
    }

    private static string ReadNestedArgumentBody(
        IReadOnlyList<YamlLine> lines,
        int argumentLineIndex,
        int commandEndLineIndex,
        int bodyIndent)
    {
        var bodyLines = new List<string>();
        for (var index = argumentLineIndex + 1; index <= commandEndLineIndex; index++)
        {
            var line = lines[index];
            if (!string.IsNullOrWhiteSpace(line.Trimmed) && line.Indent < bodyIndent)
            {
                break;
            }

            bodyLines.Add(RemoveLeadingSpaces(line.Text, bodyIndent));
        }

        return string.Join(Environment.NewLine, bodyLines).TrimEnd();
    }

    private static string RemoveLeadingSpaces(string line, int count)
    {
        var removable = Math.Min(count, line.TakeWhile(character => character == ' ').Count());
        return line[removable..];
    }

    private static string BuildSourcePreview(
        IReadOnlyList<YamlLine> lines,
        int startLineIndex,
        int endLineIndex)
    {
        return string.Join(
            Environment.NewLine,
            lines
                .Skip(startLineIndex)
                .Take(endLineIndex - startLineIndex + 1)
                .Select(line => line.Text));
    }

    private static string DisplayCommandType(string commandType)
    {
        return commandType switch
        {
            "log.text" or "log.value" => "log",
            var type when type.StartsWith("assert.", StringComparison.Ordinal) => "assert",
            _ => commandType
        };
    }

    private static string AccentColorFor(string commandType)
    {
        if (commandType == "set")
        {
            return "#68D4CF";
        }

        if (commandType == "call")
        {
            return "#B59CFF";
        }

        if (commandType is "for")
        {
            return "#65A8FF";
        }

        if (commandType == "if")
        {
            return "#DF95FF";
        }

        if (commandType.StartsWith("log.", StringComparison.Ordinal))
        {
            return "#66D09A";
        }

        if (commandType == "delay")
        {
            return "#EFCA74";
        }

        if (commandType.StartsWith("assert.", StringComparison.Ordinal))
        {
            return "#F17B7B";
        }

        return "#9EAABB";
    }

    private static List<int> FindTestcaseStarts(
        IReadOnlyList<YamlLine> lines,
        int startLineIndex)
    {
        var starts = new List<int>();
        for (var index = startLineIndex; index < lines.Count; index++)
        {
            var line = lines[index];
            if (line.Indent == 0 && line.Trimmed.EndsWith(':'))
            {
                break;
            }

            if (line.Indent == 2 && line.Trimmed.StartsWith("- name:", StringComparison.Ordinal))
            {
                starts.Add(index);
            }
        }

        return starts;
    }

    private static int FindRootSection(
        IReadOnlyList<YamlLine> lines,
        string sectionName)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            if (line.Indent == 0 && line.Trimmed == $"{sectionName}:")
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindPhaseLine(
        IReadOnlyList<YamlLine> lines,
        int testcaseStartLineIndex,
        int testcaseEndLineIndex,
        string phaseName)
    {
        for (var index = testcaseStartLineIndex + 1; index <= testcaseEndLineIndex; index++)
        {
            var line = lines[index];
            if (line.Indent == 4 && line.Trimmed == $"{phaseName}:")
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindPhaseEnd(
        IReadOnlyList<YamlLine> lines,
        int startLineIndex,
        int testcaseEndLineIndex)
    {
        for (var index = startLineIndex; index <= testcaseEndLineIndex; index++)
        {
            var line = lines[index];
            if (line.Indent <= 4 && !string.IsNullOrWhiteSpace(line.Trimmed))
            {
                return index - 1;
            }
        }

        return testcaseEndLineIndex;
    }

    private static int FindSectionEnd(
        IReadOnlyList<YamlLine> lines,
        int startLineIndex,
        int sectionIndent)
    {
        for (var index = startLineIndex + 1; index < lines.Count; index++)
        {
            var line = lines[index];
            if (line.Indent < sectionIndent && !string.IsNullOrWhiteSpace(line.Trimmed))
            {
                return index - 1;
            }
        }

        return lines.Count - 1;
    }

    private static int FindRootSectionEnd(
        IReadOnlyList<YamlLine> lines,
        int startLineIndex)
    {
        for (var index = startLineIndex + 1; index < lines.Count; index++)
        {
            var line = lines[index];
            if (line.Indent == 0 && !string.IsNullOrWhiteSpace(line.Trimmed))
            {
                return index - 1;
            }
        }

        return lines.Count - 1;
    }

    private static string FindScalar(
        IReadOnlyList<YamlLine> lines,
        int startLineIndex,
        int endLineIndex,
        int indent,
        string key)
    {
        for (var index = startLineIndex; index <= endLineIndex; index++)
        {
            var line = lines[index];
            var match = KeyValueRegex.Match(line.Trimmed);
            if (line.Indent == indent && match.Success && match.Groups[1].Value == key)
            {
                return StripQuotes(match.Groups[2].Value.Trim());
            }
        }

        return string.Empty;
    }

    private static string ValueAfterPrefix(string text, string prefix)
    {
        return StripQuotes(text.StartsWith(prefix, StringComparison.Ordinal)
            ? text[prefix.Length..].Trim()
            : string.Empty);
    }

    private static string NormalizeList(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim('[', ']')
            .Replace(",", ", ", StringComparison.Ordinal)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Aggregate(string.Empty, (current, part) => current.Length == 0 ? part : $"{current} {part}");
    }

    private static string StripQuotes(string value)
    {
        if (value.Length >= 2
            && ((value[0] == '"' && value[^1] == '"')
                || (value[0] == '\'' && value[^1] == '\'')))
        {
            return value[1..^1];
        }

        return value;
    }

    private static string SummaryFromParts(params string[] parts)
    {
        var summary = string.Join(
            " ",
            parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        return string.IsNullOrWhiteSpace(summary) ? "Configure command" : summary;
    }

    private static string DefaultIfBlank(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static string LineRange(int startLine, int endLine)
    {
        return startLine == endLine ? $"L{startLine}" : $"L{startLine}-L{endLine}";
    }

    private static int TrimTrailingBlankLines(int startLineIndex, int endLineIndex)
    {
        var end = Math.Max(startLineIndex, endLineIndex);
        return end;
    }

    private static IReadOnlyList<YamlLine> SplitLines(string yamlText)
    {
        return yamlText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Select((text, index) => new YamlLine(index, text))
            .ToArray();
    }

    private sealed class TempCommandBlock
    {
        public TempCommandBlock(string commandType, int startLineIndex, int indent)
        {
            CommandType = commandType;
            StartLineIndex = startLineIndex;
            EndLineIndex = startLineIndex;
            Indent = indent;
        }

        public string DisplayIndex { get; set; } = string.Empty;

        public string CommandType { get; }

        public int StartLineIndex { get; }

        public int EndLineIndex { get; set; }

        public int Indent { get; }

        public int Depth { get; set; }

        public List<TempCommandBlock> Children { get; } = new();
    }

    private sealed class YamlLine
    {
        public YamlLine(int index, string text)
        {
            Index = index;
            Text = text;
            Trimmed = text.TrimStart();
            Indent = text.Length - Trimmed.Length;
        }

        public int Index { get; }

        public string Text { get; }

        public string Trimmed { get; }

        public int Indent { get; }
    }
}
