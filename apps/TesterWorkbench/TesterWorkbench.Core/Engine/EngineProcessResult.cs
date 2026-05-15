namespace TesterWorkbench.Core.Engine;

public sealed record EngineProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);

public sealed record EngineDiagnostic(
    string Severity,
    string Code,
    string Message);

public sealed record EngineCompileResult(
    int ExitCode,
    IReadOnlyList<EngineDiagnostic> Diagnostics,
    string StandardError,
    string RawJson);

public sealed record EngineRunResult(
    int ExitCode,
    string Status,
    string? ReportDirectory,
    string StandardError,
    string RawJson);
