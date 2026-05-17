using System.Text.RegularExpressions;

namespace TesterWorkbench.Core.ViewModels;

public sealed record WorkbenchYamlCommandArgumentUpdateResult(
    string Text,
    int UpdatedLineNumber);

public static class WorkbenchYamlCommandArgumentUpdater
{
    private static readonly Regex KeyValueRegex = new(@"^(\s*)([A-Za-z0-9_.-]+)\s*:\s*(.*)$", RegexOptions.Compiled);

    public static WorkbenchYamlCommandArgumentUpdateResult Update(
        string yamlText,
        WorkbenchCommandBlock commandBlock,
        WorkbenchCommandArgumentDefinition argument,
        string value)
    {
        if (commandBlock is null)
        {
            throw new ArgumentNullException(nameof(commandBlock));
        }

        if (argument is null)
        {
            throw new ArgumentNullException(nameof(argument));
        }

        if (!argument.IsScalarEditable
            && argument.Kind is not WorkbenchCommandArgumentKind.Map
                and not WorkbenchCommandArgumentKind.List)
        {
            throw new InvalidOperationException($"Argument '{argument.Name}' is not editable from Properties.");
        }

        var newline = yamlText.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = SplitLines(yamlText, newline);
        var commandLineIndex = ToLineIndex(commandBlock.SourceLineStart, lines.Count);
        var commandIndent = LeadingSpaceCount(lines[commandLineIndex]);
        var argumentIndent = commandIndent + 4;
        var directArgument = FindDirectArgumentLine(lines, commandBlock, argument.Name, argumentIndent);
        if (argument.IsScalarEditable)
        {
            var formattedLine = $"{new string(' ', argumentIndent)}{argument.Name}: {FormatScalar(argument, value)}";
            if (directArgument >= 0)
            {
                lines[directArgument] = formattedLine;
                return new WorkbenchYamlCommandArgumentUpdateResult(
                    JoinLines(lines, newline),
                    directArgument + 1);
            }

            var scalarInsertAtLineIndex = FindInsertionLine(lines, commandBlock, commandLineIndex, commandIndent, argumentIndent);
            lines.Insert(scalarInsertAtLineIndex, formattedLine);
            return new WorkbenchYamlCommandArgumentUpdateResult(
                JoinLines(lines, newline),
                scalarInsertAtLineIndex + 1);
        }

        var replacementLines = BuildComplexArgumentLines(argument, value, argumentIndent);
        if (directArgument >= 0)
        {
            var replaceEndLineIndex = FindArgumentBlockEndLine(lines, commandBlock, directArgument, argumentIndent);
            lines.RemoveRange(directArgument, replaceEndLineIndex - directArgument + 1);
            lines.InsertRange(directArgument, replacementLines);
            return new WorkbenchYamlCommandArgumentUpdateResult(
                JoinLines(lines, newline),
                directArgument + 1);
        }

        var insertAtLineIndex = FindInsertionLine(lines, commandBlock, commandLineIndex, commandIndent, argumentIndent);
        lines.InsertRange(insertAtLineIndex, replacementLines);
        return new WorkbenchYamlCommandArgumentUpdateResult(
            JoinLines(lines, newline),
            insertAtLineIndex + 1);
    }

    private static int FindDirectArgumentLine(
        IReadOnlyList<string> lines,
        WorkbenchCommandBlock commandBlock,
        string argumentName,
        int argumentIndent)
    {
        var endLineIndex = ToLineIndex(commandBlock.SourceLineEnd, lines.Count);
        for (var index = ToLineIndex(commandBlock.SourceLineStart, lines.Count) + 1; index <= endLineIndex; index++)
        {
            if (LeadingSpaceCount(lines[index]) != argumentIndent)
            {
                continue;
            }

            var match = KeyValueRegex.Match(lines[index]);
            if (match.Success && match.Groups[2].Value == argumentName)
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindInsertionLine(
        IReadOnlyList<string> lines,
        WorkbenchCommandBlock commandBlock,
        int commandLineIndex,
        int commandIndent,
        int argumentIndent)
    {
        var endLineIndex = ToLineIndex(commandBlock.SourceLineEnd, lines.Count);
        var insertAtLineIndex = commandLineIndex + 1;
        for (var index = commandLineIndex + 1; index <= endLineIndex; index++)
        {
            var indent = LeadingSpaceCount(lines[index]);
            if (indent <= commandIndent)
            {
                break;
            }

            if (indent != argumentIndent)
            {
                continue;
            }

            var match = KeyValueRegex.Match(lines[index]);
            if (!match.Success)
            {
                continue;
            }

            var suffix = match.Groups[3].Value.Trim();
            if (string.IsNullOrWhiteSpace(suffix))
            {
                return index;
            }

            insertAtLineIndex = index + 1;
        }

        return insertAtLineIndex;
    }

    private static int FindArgumentBlockEndLine(
        IReadOnlyList<string> lines,
        WorkbenchCommandBlock commandBlock,
        int argumentLineIndex,
        int argumentIndent)
    {
        var endLineIndex = ToLineIndex(commandBlock.SourceLineEnd, lines.Count);
        var replaceEndLineIndex = argumentLineIndex;
        for (var index = argumentLineIndex + 1; index <= endLineIndex; index++)
        {
            var indent = LeadingSpaceCount(lines[index]);
            if (!string.IsNullOrWhiteSpace(lines[index]) && indent <= argumentIndent)
            {
                break;
            }

            replaceEndLineIndex = index;
        }

        return replaceEndLineIndex;
    }

    private static IReadOnlyList<string> BuildComplexArgumentLines(
        WorkbenchCommandArgumentDefinition argument,
        string value,
        int argumentIndent)
    {
        var indent = new string(' ', argumentIndent);
        var trimmed = value.Trim();
        if (argument.Kind == WorkbenchCommandArgumentKind.List
            && trimmed.StartsWith("[", StringComparison.Ordinal)
            && trimmed.EndsWith("]", StringComparison.Ordinal))
        {
            return new[] { $"{indent}{argument.Name}: {trimmed}" };
        }

        var bodyLines = NormalizeComplexValueLines(value);
        if (bodyLines.Count == 0)
        {
            var emptyValue = argument.Kind == WorkbenchCommandArgumentKind.List ? "[]" : "{}";
            return new[] { $"{indent}{argument.Name}: {emptyValue}" };
        }

        var result = new List<string> { $"{indent}{argument.Name}:" };
        result.AddRange(bodyLines.Select(line => $"{indent}  {line}"));
        return result;
    }

    private static IReadOnlyList<string> NormalizeComplexValueLines(string value)
    {
        var lines = value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .ToList();
        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0]))
        {
            lines.RemoveAt(0);
        }

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
        {
            lines.RemoveAt(lines.Count - 1);
        }

        var commonIndent = lines
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(LeadingSpaceCount)
            .DefaultIfEmpty(0)
            .Min();
        return lines
            .Select(line => line.Length >= commonIndent ? line[commonIndent..] : line)
            .ToArray();
    }

    private static string FormatScalar(WorkbenchCommandArgumentDefinition argument, string value)
    {
        var trimmed = value.Trim();
        return argument.Kind switch
        {
            WorkbenchCommandArgumentKind.Text => QuoteString(value),
            WorkbenchCommandArgumentKind.Expression => QuoteString(trimmed),
            WorkbenchCommandArgumentKind.Number => string.IsNullOrWhiteSpace(trimmed) ? "0" : trimmed,
            WorkbenchCommandArgumentKind.Boolean => NormalizeBoolean(trimmed),
            WorkbenchCommandArgumentKind.Value => FormatValue(trimmed),
            _ => string.IsNullOrWhiteSpace(trimmed) ? "\"\"" : trimmed
        };
    }

    private static string FormatValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "null";
        }

        if (value.StartsWith("${", StringComparison.Ordinal)
            || value.StartsWith("{{", StringComparison.Ordinal))
        {
            return QuoteString(value);
        }

        return value;
    }

    private static string NormalizeBoolean(string value)
    {
        if (bool.TryParse(value, out var parsed))
        {
            return parsed ? "true" : "false";
        }

        return "false";
    }

    private static string QuoteString(string value)
    {
        var trimmed = value.Trim();
        if ((trimmed.StartsWith("\"", StringComparison.Ordinal) && trimmed.EndsWith("\"", StringComparison.Ordinal))
            || (trimmed.StartsWith("'", StringComparison.Ordinal) && trimmed.EndsWith("'", StringComparison.Ordinal)))
        {
            return trimmed;
        }

        return $"\"{value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }

    private static List<string> SplitLines(string text, string newline)
    {
        return text.Split(new[] { newline }, StringSplitOptions.None).ToList();
    }

    private static string JoinLines(IEnumerable<string> lines, string newline)
    {
        return string.Join(newline, lines);
    }

    private static int ToLineIndex(int lineNumber, int lineCount)
    {
        return Math.Clamp(lineNumber - 1, 0, Math.Max(0, lineCount - 1));
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
}
