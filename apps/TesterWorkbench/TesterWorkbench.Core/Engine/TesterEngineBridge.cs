using System.Text.Json;

namespace TesterWorkbench.Core.Engine;

public sealed class TesterEngineBridge
{
    private readonly string _pythonExecutable;
    private readonly string _repositoryRoot;
    private readonly IEngineProcessRunner _processRunner;

    public TesterEngineBridge(
        string pythonExecutable,
        string repositoryRoot,
        IEngineProcessRunner processRunner)
    {
        _pythonExecutable = string.IsNullOrWhiteSpace(pythonExecutable)
            ? throw new ArgumentException("Python executable is required.", nameof(pythonExecutable))
            : pythonExecutable;
        _repositoryRoot = string.IsNullOrWhiteSpace(repositoryRoot)
            ? throw new ArgumentException("Repository root is required.", nameof(repositoryRoot))
            : repositoryRoot;
        _processRunner = processRunner;
    }

    public async Task<EngineCompileResult> CompileAsync(
        string yamlFile,
        CancellationToken cancellationToken = default)
    {
        var result = await _processRunner.RunAsync(
            _pythonExecutable,
            new[] { "-m", "embsw_tester.cli", "compile", yamlFile, "--json" },
            _repositoryRoot,
            cancellationToken);

        return new EngineCompileResult(
            result.ExitCode,
            ParseDiagnostics(result.StandardOutput),
            result.StandardError,
            result.StandardOutput);
    }

    public async Task<EngineRunResult> RunAsync(
        string yamlFile,
        string runId,
        string reportsRoot,
        CancellationToken cancellationToken = default)
    {
        var result = await _processRunner.RunAsync(
            _pythonExecutable,
            new[] { "-m", "embsw_tester.cli", "run", yamlFile, "--json", "--run-id", runId, "--reports-root", reportsRoot },
            _repositoryRoot,
            cancellationToken);

        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        var status = root.TryGetProperty("status", out var statusElement)
            ? statusElement.GetString() ?? "unknown"
            : "unknown";
        string? reportDirectory = null;
        if (root.TryGetProperty("report", out var reportElement)
            && reportElement.TryGetProperty("report_dir", out var reportDirElement))
        {
            reportDirectory = reportDirElement.GetString();
        }

        return new EngineRunResult(
            result.ExitCode,
            status,
            reportDirectory,
            result.StandardError,
            result.StandardOutput);
    }

    private static IReadOnlyList<EngineDiagnostic> ParseDiagnostics(string stdout)
    {
        using var document = JsonDocument.Parse(stdout);
        if (!document.RootElement.TryGetProperty("diagnostics", out var diagnosticsElement)
            || diagnosticsElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<EngineDiagnostic>();
        }

        var diagnostics = new List<EngineDiagnostic>();
        foreach (var diagnostic in diagnosticsElement.EnumerateArray())
        {
            diagnostics.Add(new EngineDiagnostic(
                GetString(diagnostic, "severity"),
                GetString(diagnostic, "code"),
                GetString(diagnostic, "message")));
        }

        return diagnostics;
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }
}
