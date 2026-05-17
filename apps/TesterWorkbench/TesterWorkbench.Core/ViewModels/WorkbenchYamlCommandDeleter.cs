namespace TesterWorkbench.Core.ViewModels;

public sealed record WorkbenchYamlCommandDeleteResult(
    string Text,
    int DeletedLineNumber);

public static class WorkbenchYamlCommandDeleter
{
    public static WorkbenchYamlCommandDeleteResult Delete(
        string yamlText,
        WorkbenchCommandBlock commandBlock)
    {
        if (commandBlock is null)
        {
            throw new ArgumentNullException(nameof(commandBlock));
        }

        var newline = yamlText.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = WorkbenchYamlCommandInserter.SplitLines(yamlText, newline);
        var sourceStartIndex = WorkbenchYamlCommandInserter.ToLineIndex(commandBlock.SourceLineStart, lines.Count);
        var sourceEndIndex = WorkbenchYamlCommandInserter.ToLineIndex(commandBlock.SourceLineEnd, lines.Count);
        var lineCount = sourceEndIndex - sourceStartIndex + 1;
        if (lineCount <= 0)
        {
            return new WorkbenchYamlCommandDeleteResult(yamlText, commandBlock.SourceLineStart);
        }

        lines.RemoveRange(sourceStartIndex, lineCount);

        return new WorkbenchYamlCommandDeleteResult(
            WorkbenchYamlCommandInserter.JoinLines(lines, newline),
            commandBlock.SourceLineStart);
    }
}
