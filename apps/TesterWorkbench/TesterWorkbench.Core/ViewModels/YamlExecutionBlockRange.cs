namespace TesterWorkbench.Core.ViewModels;

public sealed record YamlExecutionBlockRange(
    int StartLineIndex,
    int EndLineIndex)
{
    public static YamlExecutionBlockRange? Find(string yamlText, int sourceLineNumber)
    {
        var lines = yamlText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var startLineIndex = sourceLineNumber - 1;
        if (startLineIndex < 0 || startLineIndex >= lines.Length)
        {
            return null;
        }

        var startIndent = CountLeadingWhitespace(lines[startLineIndex]);
        var endLineIndex = startLineIndex;
        for (var index = startLineIndex + 1; index < lines.Length; index++)
        {
            var line = lines[index];
            if (string.IsNullOrWhiteSpace(line))
            {
                endLineIndex = index;
                continue;
            }

            if (CountLeadingWhitespace(line) <= startIndent)
            {
                break;
            }

            endLineIndex = index;
        }

        return new YamlExecutionBlockRange(startLineIndex, endLineIndex);
    }

    private static int CountLeadingWhitespace(string line)
    {
        var count = 0;
        foreach (var character in line)
        {
            if (character != ' ' && character != '\t')
            {
                break;
            }

            count++;
        }

        return count;
    }
}
