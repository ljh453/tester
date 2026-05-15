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
    IReadOnlyList<EngineRunEvent> Events,
    IReadOnlyList<EngineVariableValue> Variables,
    string StandardError,
    string RawJson);

public sealed record EngineRunEvent(
    string Testcase,
    string Phase,
    string CommandType,
    string Status,
    string CommandPath,
    string SourceFile,
    int SourceLine,
    IReadOnlyList<EngineVariableValue> LocalVariables,
    bool HasLocalVariables,
    string Error,
    string Detail,
    string LogText);

public sealed record EngineVariableValue(
    string Testcase,
    string Name,
    string Value);
