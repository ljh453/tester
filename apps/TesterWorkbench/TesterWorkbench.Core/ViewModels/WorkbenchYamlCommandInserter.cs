using System.Text.RegularExpressions;

namespace TesterWorkbench.Core.ViewModels;

public sealed record WorkbenchYamlCommandInsertionResult(
    string Text,
    int InsertedLineNumber);

public enum WorkbenchCommandInsertPlacement
{
    AtPhaseEnd,
    BeforeFirstInPhase,
    AfterCommand,
    InsideCommand
}

public sealed record WorkbenchCommandInsertionTarget(
    WorkbenchGuiPhase Phase,
    WorkbenchCommandInsertPlacement Placement,
    WorkbenchCommandBlock? ReferenceCommand = null);

public static class WorkbenchYamlCommandInserter
{
    private static readonly Regex PhaseLineRegex = new(@"^(\s*)([A-Za-z0-9_.-]+)\s*:\s*(.*)$", RegexOptions.Compiled);

    public static WorkbenchYamlCommandInsertionResult Insert(
        string yamlText,
        WorkbenchGuiTestcase testcase,
        WorkbenchGuiPhase phase,
        WorkbenchCommandDefinition command,
        WorkbenchCommandBlock? afterCommand = null)
    {
        var placement = afterCommand is null
            ? WorkbenchCommandInsertPlacement.AtPhaseEnd
            : WorkbenchCommandInsertPlacement.AfterCommand;
        return Insert(
            yamlText,
            testcase,
            command,
            new WorkbenchCommandInsertionTarget(phase, placement, afterCommand));
    }

    public static WorkbenchYamlCommandInsertionResult Insert(
        string yamlText,
        WorkbenchGuiTestcase testcase,
        WorkbenchCommandDefinition command,
        WorkbenchCommandInsertionTarget target)
    {
        if (testcase is null)
        {
            throw new ArgumentNullException(nameof(testcase));
        }

        if (target is null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        if (command is null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        var newline = yamlText.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = SplitLines(yamlText, newline);
        var resolvedTarget = ResolveInsertionTarget(lines, testcase, target);
        var snippetLines = BuildSnippetLines(command, resolvedTarget.CommandIndent);
        lines.InsertRange(resolvedTarget.InsertAtLineIndex, snippetLines);

        return new WorkbenchYamlCommandInsertionResult(
            JoinLines(lines, newline),
            resolvedTarget.InsertAtLineIndex + 1);
    }

    private static InsertionTarget ResolveInsertionTarget(
        List<string> lines,
        WorkbenchGuiTestcase testcase,
        WorkbenchCommandInsertionTarget target)
    {
        if (target.Placement == WorkbenchCommandInsertPlacement.AfterCommand
            && target.ReferenceCommand is not null)
        {
            var commandLineIndex = ToLineIndex(target.ReferenceCommand.SourceLineStart, lines.Count);
            return new InsertionTarget(
                ToInsertionIndex(target.ReferenceCommand.SourceLineEnd, lines.Count),
                LeadingSpaceCount(lines[commandLineIndex]));
        }

        if (target.Placement == WorkbenchCommandInsertPlacement.InsideCommand
            && target.ReferenceCommand is not null)
        {
            return ResolveInsideCommandTarget(lines, target.ReferenceCommand);
        }

        var phase = target.Phase;
        if (target.Placement == WorkbenchCommandInsertPlacement.BeforeFirstInPhase
            && phase.Blocks.Count > 0)
        {
            var firstBlock = phase.Blocks[0];
            var commandLineIndex = ToLineIndex(firstBlock.SourceLineStart, lines.Count);
            return new InsertionTarget(commandLineIndex, LeadingSpaceCount(lines[commandLineIndex]));
        }

        if (phase.Blocks.Count > 0)
        {
            var lastBlock = phase.Blocks[^1];
            var commandLineIndex = ToLineIndex(lastBlock.SourceLineStart, lines.Count);
            return new InsertionTarget(
                ToInsertionIndex(lastBlock.SourceLineEnd, lines.Count),
                LeadingSpaceCount(lines[commandLineIndex]));
        }

        if (phase.HasYamlSection && phase.SourceLineStart > 0)
        {
            var phaseLineIndex = ToLineIndex(phase.SourceLineStart, lines.Count);
            NormalizeInlineEmptyPhase(lines, phaseLineIndex, phase.YamlName);
            return new InsertionTarget(
                phaseLineIndex + 1,
                LeadingSpaceCount(lines[phaseLineIndex]) + 2);
        }

        return InsertMissingPhase(lines, testcase, phase);
    }

    private static InsertionTarget ResolveInsideCommandTarget(
        List<string> lines,
        WorkbenchCommandBlock parentCommand)
    {
        if (parentCommand.Children.Count > 0)
        {
            var lastChild = parentCommand.Children[^1];
            var childLineIndex = ToLineIndex(lastChild.SourceLineStart, lines.Count);
            return new InsertionTarget(
                ToInsertionIndex(lastChild.SourceLineEnd, lines.Count),
                LeadingSpaceCount(lines[childLineIndex]));
        }

        var doLineIndex = FindChildCommandListLine(lines, parentCommand, "do");
        if (doLineIndex >= 0)
        {
            NormalizeInlineEmptyChildList(lines, doLineIndex, "do");
            return new InsertionTarget(
                doLineIndex + 1,
                LeadingSpaceCount(lines[doLineIndex]) + 2);
        }

        var commandLineIndex = ToLineIndex(parentCommand.SourceLineStart, lines.Count);
        return new InsertionTarget(
            ToInsertionIndex(parentCommand.SourceLineEnd, lines.Count),
            LeadingSpaceCount(lines[commandLineIndex]));
    }

    private static int FindChildCommandListLine(
        IReadOnlyList<string> lines,
        WorkbenchCommandBlock parentCommand,
        string childListName)
    {
        var parentLineIndex = ToLineIndex(parentCommand.SourceLineStart, lines.Count);
        var parentIndent = LeadingSpaceCount(lines[parentLineIndex]);
        var expectedChildListIndent = parentIndent + 4;
        var endLineIndex = ToLineIndex(parentCommand.SourceLineEnd, lines.Count);
        for (var index = parentLineIndex + 1; index <= endLineIndex; index++)
        {
            if (LeadingSpaceCount(lines[index]) != expectedChildListIndent)
            {
                continue;
            }

            var match = PhaseLineRegex.Match(lines[index]);
            if (match.Success && match.Groups[2].Value == childListName)
            {
                return index;
            }
        }

        return -1;
    }

    private static void NormalizeInlineEmptyChildList(
        IList<string> lines,
        int childListLineIndex,
        string childListName)
    {
        var match = PhaseLineRegex.Match(lines[childListLineIndex]);
        if (!match.Success || match.Groups[2].Value != childListName)
        {
            return;
        }

        var suffix = match.Groups[3].Value.Trim();
        if (suffix is "[]" or "{}" or "null")
        {
            lines[childListLineIndex] = $"{match.Groups[1].Value}{childListName}:";
        }
    }

    private static InsertionTarget InsertMissingPhase(
        List<string> lines,
        WorkbenchGuiTestcase testcase,
        WorkbenchGuiPhase phase)
    {
        var testcaseStartLineIndex = ToLineIndex(testcase.SourceLineStart, lines.Count);
        var testcaseEndLineIndex = ToLineIndex(testcase.SourceLineEnd, lines.Count);
        var testcaseIndent = LeadingSpaceCount(lines[testcaseStartLineIndex]);
        var phaseIndent = testcaseIndent + 2;
        var phaseLineIndex = FindMissingPhaseInsertionLine(lines, testcaseStartLineIndex, testcaseEndLineIndex, phase.YamlName);
        lines.Insert(phaseLineIndex, $"{new string(' ', phaseIndent)}{phase.YamlName}:");
        return new InsertionTarget(phaseLineIndex + 1, phaseIndent + 2);
    }

    private static int FindMissingPhaseInsertionLine(
        IReadOnlyList<string> lines,
        int testcaseStartLineIndex,
        int testcaseEndLineIndex,
        string phaseName)
    {
        var order = new[] { "preconditions", "steps", "postconditions" };
        var phaseOrderIndex = Array.IndexOf(order, phaseName);
        if (phaseOrderIndex < 0)
        {
            return testcaseEndLineIndex + 1;
        }

        for (var index = testcaseStartLineIndex + 1; index <= testcaseEndLineIndex; index++)
        {
            var match = PhaseLineRegex.Match(lines[index]);
            if (!match.Success || LeadingSpaceCount(lines[index]) != LeadingSpaceCount(lines[testcaseStartLineIndex]) + 2)
            {
                continue;
            }

            var existingOrderIndex = Array.IndexOf(order, match.Groups[2].Value);
            if (existingOrderIndex > phaseOrderIndex)
            {
                return index;
            }
        }

        return testcaseEndLineIndex + 1;
    }

    private static void NormalizeInlineEmptyPhase(
        IList<string> lines,
        int phaseLineIndex,
        string phaseName)
    {
        var match = PhaseLineRegex.Match(lines[phaseLineIndex]);
        if (!match.Success || match.Groups[2].Value != phaseName)
        {
            return;
        }

        var suffix = match.Groups[3].Value.Trim();
        if (string.IsNullOrWhiteSpace(suffix) || suffix is not "[]" and not "{}" and not "null")
        {
            return;
        }

        lines[phaseLineIndex] = $"{match.Groups[1].Value}{phaseName}:";
    }

    private static IReadOnlyList<string> BuildSnippetLines(
        WorkbenchCommandDefinition command,
        int commandIndent)
    {
        var snippetLines = new List<string>
        {
            $"{new string(' ', commandIndent)}- {command.CommandType}:"
        };

        if (command.SnippetBody == "{}")
        {
            snippetLines[0] += " {}";
            return snippetLines;
        }

        var bodyIndent = commandIndent + 4;
        foreach (var bodyLine in command.SnippetBody.Replace("\r\n", "\n").Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(bodyLine))
            {
                snippetLines.Add(string.Empty);
                continue;
            }

            snippetLines.Add($"{new string(' ', bodyIndent)}{bodyLine}");
        }

        return snippetLines;
    }

    private static List<string> SplitLines(string text, string newline)
    {
        var normalized = newline == "\r\n"
            ? text.Replace("\r\n", "\n", StringComparison.Ordinal)
            : text;
        return normalized.Split('\n').ToList();
    }

    private static string JoinLines(IReadOnlyList<string> lines, string newline)
    {
        return string.Join(newline, lines);
    }

    private static int ToLineIndex(int lineNumber, int lineCount)
    {
        return Math.Clamp(lineNumber - 1, 0, Math.Max(0, lineCount - 1));
    }

    private static int ToInsertionIndex(int lineNumber, int lineCount)
    {
        return Math.Clamp(lineNumber, 0, lineCount);
    }

    private static int LeadingSpaceCount(string line)
    {
        var count = 0;
        while (count < line.Length && line[count] == ' ')
        {
            count++;
        }

        return count;
    }

    private sealed record InsertionTarget(
        int InsertAtLineIndex,
        int CommandIndent);
}
