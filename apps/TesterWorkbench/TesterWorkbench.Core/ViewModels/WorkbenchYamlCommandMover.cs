namespace TesterWorkbench.Core.ViewModels;

public static class WorkbenchYamlCommandMover
{
    public static WorkbenchYamlCommandInsertionResult Move(
        string yamlText,
        WorkbenchGuiTestcase testcase,
        WorkbenchCommandBlock movingCommand,
        WorkbenchCommandInsertionTarget target)
    {
        if (testcase is null)
        {
            throw new ArgumentNullException(nameof(testcase));
        }

        if (movingCommand is null)
        {
            throw new ArgumentNullException(nameof(movingCommand));
        }

        if (target is null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        if (IsInvalidSelfTarget(movingCommand, target))
        {
            return new WorkbenchYamlCommandInsertionResult(yamlText, movingCommand.SourceLineStart);
        }

        var newline = yamlText.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = WorkbenchYamlCommandInserter.SplitLines(yamlText, newline);
        var sourceStartIndex = WorkbenchYamlCommandInserter.ToLineIndex(movingCommand.SourceLineStart, lines.Count);
        var sourceEndIndex = WorkbenchYamlCommandInserter.ToLineIndex(movingCommand.SourceLineEnd, lines.Count);
        var originalLineCount = lines.Count;
        var resolvedTarget = WorkbenchYamlCommandInserter.ResolveInsertionTarget(lines, testcase, target);
        var insertedStructuralLineCount = lines.Count - originalLineCount;
        if (insertedStructuralLineCount > 0
            && target.ReferenceCommand is not null
            && target.ReferenceCommand.SourceLineEnd < movingCommand.SourceLineStart)
        {
            sourceStartIndex += insertedStructuralLineCount;
            sourceEndIndex += insertedStructuralLineCount;
        }

        var sourceLineCount = sourceEndIndex - sourceStartIndex + 1;
        if (sourceLineCount <= 0)
        {
            return new WorkbenchYamlCommandInsertionResult(yamlText, movingCommand.SourceLineStart);
        }

        var sourceIndent = WorkbenchYamlCommandInserter.LeadingSpaceCount(lines[sourceStartIndex]);
        if (resolvedTarget.InsertAtLineIndex == sourceStartIndex
            && resolvedTarget.CommandIndent == sourceIndent
            || resolvedTarget.InsertAtLineIndex > sourceStartIndex
            && resolvedTarget.InsertAtLineIndex <= sourceEndIndex + 1)
        {
            return new WorkbenchYamlCommandInsertionResult(yamlText, movingCommand.SourceLineStart);
        }

        var movingLines = lines
            .Skip(sourceStartIndex)
            .Take(sourceLineCount)
            .ToArray();
        var reindentedLines = Reindent(movingLines, sourceIndent, resolvedTarget.CommandIndent);
        lines.RemoveRange(sourceStartIndex, sourceLineCount);

        var insertIndex = resolvedTarget.InsertAtLineIndex;
        if (insertIndex > sourceStartIndex)
        {
            insertIndex -= sourceLineCount;
        }

        insertIndex = Math.Clamp(insertIndex, 0, lines.Count);
        lines.InsertRange(insertIndex, reindentedLines);

        return new WorkbenchYamlCommandInsertionResult(
            WorkbenchYamlCommandInserter.JoinLines(lines, newline),
            insertIndex + 1);
    }

    public static WorkbenchYamlCommandInsertionResult MoveMany(
        string yamlText,
        WorkbenchGuiTestcase testcase,
        IReadOnlyList<WorkbenchCommandBlock> movingCommands,
        WorkbenchCommandInsertionTarget target)
    {
        if (testcase is null)
        {
            throw new ArgumentNullException(nameof(testcase));
        }

        if (movingCommands is null)
        {
            throw new ArgumentNullException(nameof(movingCommands));
        }

        if (target is null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        var sortedCommands = movingCommands
            .Where(command => command is not null)
            .OrderBy(command => command.SourceLineStart)
            .ToArray();
        if (sortedCommands.Length == 0)
        {
            return new WorkbenchYamlCommandInsertionResult(yamlText, 0);
        }

        if (sortedCommands.Length == 1)
        {
            return Move(yamlText, testcase, sortedCommands[0], target);
        }

        if (sortedCommands.Any(command => IsInvalidSelfTarget(command, target)))
        {
            return new WorkbenchYamlCommandInsertionResult(yamlText, sortedCommands[0].SourceLineStart);
        }

        var newline = yamlText.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = WorkbenchYamlCommandInserter.SplitLines(yamlText, newline);
        var ranges = sortedCommands
            .Select(command => MovingRange.FromCommand(command, lines))
            .Where(range => range.LineCount > 0)
            .ToArray();
        if (ranges.Length == 0)
        {
            return new WorkbenchYamlCommandInsertionResult(yamlText, sortedCommands[0].SourceLineStart);
        }

        var originalLineCount = lines.Count;
        var resolvedTarget = WorkbenchYamlCommandInserter.ResolveInsertionTarget(lines, testcase, target);
        var insertedStructuralLineCount = lines.Count - originalLineCount;
        if (insertedStructuralLineCount > 0)
        {
            var structuralInsertIndex = resolvedTarget.InsertAtLineIndex - insertedStructuralLineCount;
            ranges = ranges
                .Select(range => range.ShiftForInsertedLines(
                    structuralInsertIndex,
                    insertedStructuralLineCount))
                .ToArray();
        }

        if (ranges.Any(range =>
                resolvedTarget.InsertAtLineIndex >= range.StartIndex
                && resolvedTarget.InsertAtLineIndex <= range.EndIndex))
        {
            return new WorkbenchYamlCommandInsertionResult(yamlText, sortedCommands[0].SourceLineStart);
        }

        var movingLines = ranges
            .SelectMany(range =>
                Reindent(
                    lines.Skip(range.StartIndex).Take(range.LineCount).ToArray(),
                    range.SourceIndent,
                    resolvedTarget.CommandIndent))
            .ToArray();

        var insertIndex = resolvedTarget.InsertAtLineIndex;
        foreach (var range in ranges.OrderByDescending(range => range.StartIndex))
        {
            lines.RemoveRange(range.StartIndex, range.LineCount);
            if (range.StartIndex < insertIndex)
            {
                insertIndex -= range.LineCount;
            }
        }

        insertIndex = Math.Clamp(insertIndex, 0, lines.Count);
        lines.InsertRange(insertIndex, movingLines);

        return new WorkbenchYamlCommandInsertionResult(
            WorkbenchYamlCommandInserter.JoinLines(lines, newline),
            insertIndex + 1);
    }

    private static bool IsInvalidSelfTarget(
        WorkbenchCommandBlock movingCommand,
        WorkbenchCommandInsertionTarget target)
    {
        if (target.ReferenceCommand is null)
        {
            return false;
        }

        return target.ReferenceCommand == movingCommand
            || target.ReferenceCommand.SourceLineStart >= movingCommand.SourceLineStart
            && target.ReferenceCommand.SourceLineStart <= movingCommand.SourceLineEnd;
    }

    private static IReadOnlyList<string> Reindent(
        IReadOnlyList<string> lines,
        int sourceIndent,
        int targetIndent)
    {
        var sourcePrefix = new string(' ', sourceIndent);
        var targetPrefix = new string(' ', targetIndent);
        return lines
            .Select(line => ReindentLine(line, sourcePrefix, targetPrefix))
            .ToArray();
    }

    private static string ReindentLine(
        string line,
        string sourcePrefix,
        string targetPrefix)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return string.Empty;
        }

        return line.StartsWith(sourcePrefix, StringComparison.Ordinal)
            ? $"{targetPrefix}{line[sourcePrefix.Length..]}"
            : line;
    }

    private sealed record MovingRange(
        int StartIndex,
        int EndIndex,
        int SourceIndent)
    {
        public int LineCount => EndIndex - StartIndex + 1;

        public static MovingRange FromCommand(
            WorkbenchCommandBlock command,
            IReadOnlyList<string> lines)
        {
            var startIndex = WorkbenchYamlCommandInserter.ToLineIndex(
                command.SourceLineStart,
                lines.Count);
            var endIndex = WorkbenchYamlCommandInserter.ToLineIndex(
                command.SourceLineEnd,
                lines.Count);
            return new MovingRange(
                startIndex,
                endIndex,
                WorkbenchYamlCommandInserter.LeadingSpaceCount(lines[startIndex]));
        }

        public MovingRange ShiftForInsertedLines(
            int insertedAtLineIndex,
            int insertedLineCount)
        {
            if (insertedAtLineIndex <= StartIndex)
            {
                return this with
                {
                    StartIndex = StartIndex + insertedLineCount,
                    EndIndex = EndIndex + insertedLineCount
                };
            }

            return this;
        }
    }
}
