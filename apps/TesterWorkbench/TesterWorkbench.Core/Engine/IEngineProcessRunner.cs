namespace TesterWorkbench.Core.Engine;

public interface IEngineProcessRunner
{
    Task<EngineProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken = default,
        Action<string>? onEventJsonLine = null);
}
