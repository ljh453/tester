using System.Diagnostics;
using System.Text;

namespace TesterWorkbench.Core.Engine;

public sealed class ProcessEngineRunner : IEngineProcessRunner
{
    private const string EventJsonLinePrefix = "__EMBSW_EVENT__ ";

    public async Task<EngineProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken = default,
        Action<string>? onEventJsonLine = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        var srcPath = Path.Combine(workingDirectory, "src");
        if (Directory.Exists(srcPath))
        {
            var existingPythonPath = startInfo.Environment.TryGetValue("PYTHONPATH", out var value)
                ? value
                : string.Empty;
            startInfo.Environment["PYTHONPATH"] = string.IsNullOrWhiteSpace(existingPythonPath)
                ? srcPath
                : $"{srcPath}{Path.PathSeparator}{existingPythonPath}";
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start engine process '{fileName}'.");
        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = ReadStandardErrorAsync(process, onEventJsonLine, cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        return new EngineProcessResult(
            process.ExitCode,
            await stdout,
            await stderr);
    }

    private static async Task<string> ReadStandardErrorAsync(
        Process process,
        Action<string>? onEventJsonLine,
        CancellationToken cancellationToken)
    {
        var standardError = new StringBuilder();
        while (await process.StandardError.ReadLineAsync(cancellationToken) is { } line)
        {
            if (line.StartsWith(EventJsonLinePrefix, StringComparison.Ordinal))
            {
                onEventJsonLine?.Invoke(line[EventJsonLinePrefix.Length..]);
                continue;
            }

            standardError.AppendLine(line);
        }

        return standardError.ToString();
    }
}
