using System.Diagnostics;
using FrameGenManagerNext.Core.Features.Dlss5;

namespace FrameGenManagerNext.Infrastructure.Processes;

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(
        string executablePath,
        string arguments,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var stderrTask = process.StandardError.ReadToEndAsync(CancellationToken.None);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
            return new ProcessResult(
                process.ExitCode,
                await stdoutTask,
                await stderrTask,
                TimedOut: false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (Exception exception) when (
                exception is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
            {
                // The process may have exited between the check and the kill.
            }

            var stdout = await SafeReadAsync(stdoutTask);
            var stderr = await SafeReadAsync(stderrTask);
            return new ProcessResult(-1, stdout, stderr, TimedOut: true);
        }
    }

    private static async Task<string> SafeReadAsync(Task<string> task)
    {
        try
        {
            return await task;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
}
