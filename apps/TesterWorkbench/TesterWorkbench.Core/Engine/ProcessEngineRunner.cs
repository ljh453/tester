using System.Diagnostics;

namespace TesterWorkbench.Core.Engine;

public sealed class ProcessEngineRunner : IEngineProcessRunner
{
    public async Task<EngineProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken = default)
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
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        return new EngineProcessResult(
            process.ExitCode,
            await stdout,
            await stderr);
    }
}
