using System.Diagnostics;
using FrameGenManagerNext.Core.Features.Gpu;
using FrameGenManagerNext.Domain.Gpu;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace FrameGenManagerNext.Infrastructure.Gpu;

public sealed class NvidiaSmiGpuProbe : IGpuProbe
{
    private readonly ILogger<NvidiaSmiGpuProbe> _logger;

    public NvidiaSmiGpuProbe(ILogger<NvidiaSmiGpuProbe> logger)
    {
        _logger = logger;
    }

    public async Task<GpuInfo> GetAsync(CancellationToken cancellationToken)
    {
        var (name, driver, diagnostic) = await QueryAsync(cancellationToken);
        var gpuName = string.IsNullOrWhiteSpace(name) ? "NVIDIA GPU not detected" : name;
        var route = GpuRouteClassifier.Classify(gpuName);
        var hags = ReadHags();

        if (diagnostic is not null)
        {
            _logger.LogInformation("GPU probe diagnostic: {Diagnostic}", diagnostic);
        }

        return new GpuInfo(
            gpuName,
            driver,
            route,
            hags,
            GpuCompatibility.EvaluateFrameGeneration(route, driver),
            GpuCompatibility.EvaluateSmoothMotion(route, driver),
            diagnostic);
    }

    private async Task<(string? Name, string? Driver, string? Diagnostic)> QueryAsync(
        CancellationToken cancellationToken)
    {
        var executable = FindNvidiaSmi();
        if (executable is null)
        {
            return (null, null, "nvidia-smi.exe was not found.");
        }

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = "--query-gpu=name,driver_version --format=csv,noheader",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };

            process.Start();

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));

            try
            {
                var output = await process.StandardOutput.ReadToEndAsync(timeout.Token);
                var error = await process.StandardError.ReadToEndAsync(timeout.Token);
                await process.WaitForExitAsync(timeout.Token);

                if (process.ExitCode != 0)
                {
                    return (null, null, string.IsNullOrWhiteSpace(error)
                        ? $"nvidia-smi exited with code {process.ExitCode}."
                        : error.Trim());
                }

                var lines = output.Split(
                    ['\r', '\n'],
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (lines.Length == 0)
                {
                    return (null, null, "nvidia-smi returned no GPU rows.");
                }

                var columns = lines[0].Split(',', StringSplitOptions.TrimEntries);
                var name = columns.Length > 0 ? columns[0] : null;
                var driver = columns.Length > 1 ? columns[1] : null;
                var diagnostic = lines.Length > 1
                    ? $"Multiple GPUs detected ({lines.Length}); using the first row."
                    : null;

                return (name, driver, diagnostic);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                return (null, null, "nvidia-smi timed out.");
            }
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            _logger.LogWarning(exception, "Could not run nvidia-smi");
            return (null, null, exception.Message);
        }
    }

    private static string? FindNvidiaSmi()
    {
        var systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
        if (!string.IsNullOrWhiteSpace(systemDirectory))
        {
            var candidate = Path.Combine(systemDirectory, "nvidia-smi.exe");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            var candidate = Path.Combine(programFiles, "NVIDIA Corporation", "NVSMI", "nvidia-smi.exe");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return "nvidia-smi.exe";
    }

    private static bool? ReadHags()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers");
            var value = key?.GetValue("HwSchMode");
            return value is int mode
                ? mode switch
                {
                    2 => true,
                    1 => false,
                    _ => null,
                }
                : null;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }

    private static void TryKill(Process process)
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
    }
}
