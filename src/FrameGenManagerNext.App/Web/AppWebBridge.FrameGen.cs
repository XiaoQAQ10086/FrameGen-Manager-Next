using System.Text.Json;
using FrameGenManagerNext.Core.Features.Framegen;
using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Gpu;

namespace FrameGenManagerNext.App.Web;

public sealed partial class AppWebBridge
{
    private async Task<object?> BuildGpuStateAsync()
    {
        var gpu = await _gpu.GetAsync(CancellationToken.None);
        var spoof = await _spoof.InspectAsync(CancellationToken.None);
        return new
        {
            gpu = new
            {
                name = gpu.Name,
                driver = gpu.DriverVersion,
                route = gpu.Route.ToString(),
                frameGeneration = gpu.FrameGeneration.ToString(),
                smoothMotion = gpu.SmoothMotion.ToString(),
                hags = gpu.HardwareAcceleratedGpuScheduling is bool hags
                    ? hags ? "on" : "off"
                    : "unknown",
                diagnostic = gpu.Diagnostic,
            },
            spoof = new
            {
                elevated = spoof.IsElevated,
                current = spoof.Primary?.CurrentName,
                driver = spoof.Primary?.DriverName,
                spoofed = spoof.Primary?.Spoofed ?? false,
                hasOriginal = spoof.Primary?.HasOriginalBackup ?? false,
                presets = GpuSpoofPresets.UiChoices,
            },
        };
    }

    private async Task<object?> ApplySpoofAsync(JsonElement payload)
    {
        var preset = payload.TryGetProperty("preset", out var value) ? value.GetString() : null;
        if (string.IsNullOrWhiteSpace(preset))
        {
            throw new BridgeException(Domain.Errors.ErrorCode.InvalidArgument, "missing preset");
        }

        var result = await _spoof.ApplyAsync(preset, CancellationToken.None);
        if (result.IsFailure)
        {
            throw new BridgeException(result.Error!.Code, result.Error.Diagnostic ?? "GPU name change failed");
        }

        return await BuildGpuStateAsync();
    }

    private async Task<object?> RestoreSpoofAsync(JsonElement payload)
    {
        var target = payload.TryGetProperty("target", out var value) &&
                     value.GetString() == "original"
            ? GpuSpoofRestoreTarget.OriginalValue
            : GpuSpoofRestoreTarget.DriverName;

        var result = await _spoof.RestoreAsync(target, CancellationToken.None);
        if (result.IsFailure)
        {
            throw new BridgeException(result.Error!.Code, result.Error.Diagnostic ?? "GPU name restore failed");
        }

        return await BuildGpuStateAsync();
    }

    private async Task<object?> DeployFrameGenAsync(JsonElement payload)
    {
        var game = await FindGameAsync(IdOf(payload));
        if (game is null)
        {
            throw new BridgeException(Domain.Errors.ErrorCode.FileNotFound, "game not found");
        }

        var options = ReadFrameGenOptions(payload);
        return await RunExclusiveAsync(async () =>
        {
            var progress = new Progress<DeploymentProgress>(update =>
            {
                ReportProgress(update.Phase, update.Subject, update.Current, update.Total);
            });
            var result = await _frameGen.DeployAsync(
                game.TargetDirectory,
                options,
                progress,
                CancellationToken.None);
            if (result.IsFailure)
            {
                throw new BridgeException(result.Error!.Code, result.Error.Diagnostic ?? "deployment failed");
            }

            return await BuildGamesStateAsync();
        });
    }

    private async Task<object?> RestoreFrameGenAsync(JsonElement payload)
    {
        var game = await FindGameAsync(IdOf(payload));
        if (game is null)
        {
            throw new BridgeException(Domain.Errors.ErrorCode.FileNotFound, "game not found");
        }

        return await RunExclusiveAsync(async () =>
        {
            var progress = new Progress<DeploymentProgress>(update =>
            {
                ReportProgress(update.Phase, update.Subject, update.Current, update.Total);
            });
            var result = await _frameGen.RestoreAsync(
                game.TargetDirectory,
                progress,
                CancellationToken.None);
            if (result.IsFailure)
            {
                throw new BridgeException(result.Error!.Code, result.Error.Diagnostic ?? "restore failed");
            }

            return await BuildGamesStateAsync();
        });
    }

    private static FrameGenOptions ReadFrameGenOptions(JsonElement payload)
    {
        if (!payload.TryGetProperty("options", out var options) ||
            options.ValueKind != JsonValueKind.Object)
        {
            return FrameGenOptions.Default;
        }

        return new FrameGenOptions(
            ReadString(options, "proxySlot", "version.dll"),
            ReadInt(options, "optimized", 1),
            ReadInt(options, "frames", 3),
            ReadString(options, "preset", "A"),
            ReadString(options, "engine", "DLSS"),
            ReadString(options, "superResolutionMode", "Off"),
            ReadString(options, "xeSsFlow", "Middle"),
            ReadString(options, "xeSsHudless", "Auto"));
    }

    private static string ReadString(JsonElement element, string name, string fallback)
    {
        return element.TryGetProperty(name, out var value) ? value.GetString() ?? fallback : fallback;
    }

    private static int ReadInt(JsonElement element, string name, int fallback)
    {
        return element.TryGetProperty(name, out var value) && value.TryGetInt32(out var number)
            ? number
            : fallback;
    }
}
