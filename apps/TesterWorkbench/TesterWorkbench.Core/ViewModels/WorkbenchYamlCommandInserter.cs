using System.Text.RegularExpressions;

namespace TesterWorkbench.Core.ViewModels;

public sealed record WorkbenchYamlCommandInsertionResult(
    string Text,
    int InsertedLineNumber);

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
        if (testcase is null)
        {
            throw new ArgumentNullException(nameof(testcase));
        }

        if (phase is null)
        {
            throw new ArgumentNullException(nameof(phase));
        }

        if (command is null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        var newline = yamlText.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = SplitLines(yamlText, newline);
        var target = ResolveInsertionTarget(lines, testcase, phase, afterCommand);
        var snippetLines = BuildSnippetLines(command, target.CommandIndent);
        lines.InsertRange(target.InsertAtLineIndex, snippetLines);

        return new WorkbenchYamlCommandInsertionResult(
            JoinLines(lines, newline),
            target.InsertAtLineIndex + 1);
    }

    private static InsertionTarget ResolveInsertionTarget(
        List<string> lines,
        WorkbenchGuiTestcase testcase,
        WorkbenchGuiPhase phase,
        WorkbenchCommandBlock? afterCommand)
    {
        if (afterCommand is not null)
        {
            var commandLineIndex = ToLineIndex(afterCommand.SourceLineStart, lines.Count);
            return new InsertionTarget(
                ToInsertionIndex(afterCommand.SourceLineEnd, lines.Count),
                LeadingSpaceCount(lines[commandLineIndex]));
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
