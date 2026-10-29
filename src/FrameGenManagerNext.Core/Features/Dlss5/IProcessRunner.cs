namespace FrameGenManagerNext.Core.Features.Dlss5;

public sealed record ProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool TimedOut);

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(
        string executablePath,
        string arguments,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}
