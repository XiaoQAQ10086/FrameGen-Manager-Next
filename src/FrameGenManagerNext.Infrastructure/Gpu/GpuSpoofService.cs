using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using FrameGenManagerNext.Core.Features.Gpu;
using FrameGenManagerNext.Domain.Errors;
using FrameGenManagerNext.Domain.Gpu;
using FrameGenManagerNext.Domain.Results;
using FrameGenManagerNext.Infrastructure.Paths;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.Infrastructure.Gpu;

public sealed class GpuSpoofService : IGpuSpoofService
{
    private const string HelperFileName = "fgm-next-elevated.exe";
    private const string DevelopmentHelperFileName = "FrameGenManagerNext.Elevated.exe";

    private static readonly JsonSerializerOptions ResultOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly AppPaths _paths;
    private readonly ILogger<GpuSpoofService> _logger;

    public GpuSpoofService(AppPaths paths, ILogger<GpuSpoofService> logger)
    {
        _paths = paths;
        _logger = logger;
    }

    public Task<GpuSpoofState> InspectAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return Task.FromResult(GpuSpoofOperations.ReadState(_paths));
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException or IOException or InvalidOperationException)
        {
            _logger.LogWarning(exception, "Could not read GPU spoof state");
            return Task.FromResult(new GpuSpoofState(
                GpuSpoofOperations.IsProcessElevated(),
                Array.Empty<GpuSpoofAdapter>(),
                exception.Message));
        }
    }

    public Task<Result<GpuSpoofOperationReport>> ApplyAsync(
        string presetName,
        CancellationToken cancellationToken)
    {
        if (!GpuSpoofPresets.IsAllowed(presetName))
        {
            return Task.FromResult(Result<GpuSpoofOperationReport>.Fail(new AppError(
                ErrorCode.InvalidArgument,
                $"GPU name is not in the allowed preset list: {presetName}")));
        }

        return RunAsync(
            ["--gpu-apply", presetName],
            () => GpuSpoofOperations.Apply(_paths, presetName),
            cancellationToken);
    }

    public Task<Result<GpuSpoofOperationReport>> RestoreAsync(
        GpuSpoofRestoreTarget target,
        CancellationToken cancellationToken)
    {
        var argument = target == GpuSpoofRestoreTarget.DriverName ? "driver" : "backup";
        return RunAsync(
            ["--gpu-restore", argument],
            () => GpuSpoofOperations.Restore(_paths, target),
            cancellationToken);
    }

    private async Task<Result<GpuSpoofOperationReport>> RunAsync(
        string[] operationArguments,
        Func<Result<GpuSpoofOperationReport>> localOperation,
        CancellationToken cancellationToken)
    {
        if (GpuSpoofOperations.IsProcessElevated())
        {
            return localOperation();
        }

        var helper = FindHelper();
        if (helper is null)
        {
            return Result<GpuSpoofOperationReport>.Fail(new AppError(
                ErrorCode.ElevationRequired,
                "The elevated helper (fgm-next-elevated.exe) is missing. Use the portable or installed build."));
        }

        var resultPath = Path.Combine(
            Path.GetTempPath(),
            $"fgm-gpuspoof-{Environment.ProcessId}-{Guid.NewGuid():N}.json");

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = helper,
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            foreach (var argument in operationArguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            startInfo.ArgumentList.Add(resultPath);

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return Result<GpuSpoofOperationReport>.Fail(new AppError(
                    ErrorCode.ElevationRequired,
                    "The elevated helper could not be started."));
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(2));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return Result<GpuSpoofOperationReport>.Fail(new AppError(
                    ErrorCode.GpuSpoofFailed,
                    "The elevated helper timed out."));
            }
            catch (OperationCanceledException)
            {
                return Result<GpuSpoofOperationReport>.Fail(new AppError(
                    ErrorCode.OperationCancelled,
                    "The GPU operation was cancelled."));
            }

            if (!File.Exists(resultPath))
            {
                return Result<GpuSpoofOperationReport>.Fail(new AppError(
                    ErrorCode.GpuSpoofFailed,
                    $"The elevated helper exited with code {process.ExitCode} without a result."));
            }

            var json = await File.ReadAllTextAsync(resultPath, cancellationToken);
            var result = JsonSerializer.Deserialize<ElevatedResult>(json, ResultOptions);
            if (result is null)
            {
                return Result<GpuSpoofOperationReport>.Fail(new AppError(
                    ErrorCode.GpuSpoofFailed,
                    "The elevated helper returned an unreadable result."));
            }

            if (!result.Ok)
            {
                var code = Enum.TryParse<ErrorCode>(result.Code, ignoreCase: true, out var parsed)
                    ? parsed
                    : ErrorCode.GpuSpoofFailed;
                return Result<GpuSpoofOperationReport>.Fail(new AppError(
                    code,
                    result.Message ?? "The elevated helper failed."));
            }

            return Result<GpuSpoofOperationReport>.Ok(new GpuSpoofOperationReport(
                result.Message ?? "GPU name operation completed.",
                GpuSpoofOperations.ReadState(_paths)));
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            return Result<GpuSpoofOperationReport>.Fail(new AppError(
                ErrorCode.ElevationCancelled,
                "Administrator approval was declined."));
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            _logger.LogWarning(exception, "Could not launch the elevated GPU helper");
            return Result<GpuSpoofOperationReport>.Fail(new AppError(
                ErrorCode.ElevationRequired,
                exception.Message));
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            _logger.LogWarning(exception, "Could not read the elevated GPU result");
            return Result<GpuSpoofOperationReport>.Fail(new AppError(
                ErrorCode.GpuSpoofFailed,
                exception.Message));
        }
        finally
        {
            TryDelete(resultPath);
        }
    }

    private static string? FindHelper()
    {
        var directory = AppContext.BaseDirectory;
        foreach (var name in new[] { HelperFileName, DevelopmentHelperFileName })
        {
            var candidate = Path.Combine(directory, name);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A stale temporary result is harmless.
        }
    }

    private sealed record ElevatedResult(bool Ok, string? Code, string? Message);
}
