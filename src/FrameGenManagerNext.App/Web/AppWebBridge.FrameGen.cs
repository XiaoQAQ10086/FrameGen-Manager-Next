using System.IO;
using System.Text.Json;
using FrameGenManagerNext.Core.Features.Framegen;
using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Gpu;
using FrameGenManagerNext.Domain.Library;

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
        var target = ResolveDeploymentTarget(game, payload);
        var occupiedProxy = FindReShadeProxy(target.Directory);
        if (occupiedProxy is not null &&
            string.Equals(occupiedProxy, options.ProxySlot, StringComparison.OrdinalIgnoreCase))
        {
            throw new BridgeException(
                Domain.Errors.ErrorCode.Conflict,
                $"proxy slot {occupiedProxy} is used by DLSS5/ReShade; choose another proxy");
        }

        var exePath = target.ExePath ?? game.RenderExePath;
        if (!string.IsNullOrWhiteSpace(exePath))
        {
            var smoothMotion = await _nvapiSmoothMotion.GetStateAsync(exePath, CancellationToken.None);
            if (smoothMotion.Enabled == true)
            {
                throw new BridgeException(
                    Domain.Errors.ErrorCode.Conflict,
                    "Smooth Motion is enabled for this game; turn it off before deploying frame generation.");
            }
        }

        return await RunExclusiveAsync(async () =>
        {
            await PersistTargetAsync(game, target.ExePath);

            var progress = new Progress<DeploymentProgress>(update =>
            {
                ReportProgress(update.Phase, update.Subject, update.Current, update.Total);
            });
            var result = await _frameGen.DeployAsync(
                target.Directory,
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

        var target = ResolveDeploymentTarget(game, payload);
        return await RunExclusiveAsync(async () =>
        {
            await PersistTargetAsync(game, target.ExePath);

            var progress = new Progress<DeploymentProgress>(update =>
            {
                ReportProgress(update.Phase, update.Subject, update.Current, update.Total);
            });
            var result = await _frameGen.RestoreAsync(
                target.Directory,
                progress,
                CancellationToken.None);
            if (result.IsFailure)
            {
                throw new BridgeException(result.Error!.Code, result.Error.Diagnostic ?? "restore failed");
            }

            return await BuildGamesStateAsync();
        });
    }

    private async Task<object?> CleanFrameGenAsync(JsonElement payload)
    {
        var game = await FindGameAsync(IdOf(payload));
        if (game is null)
        {
            throw new BridgeException(Domain.Errors.ErrorCode.FileNotFound, "game not found");
        }

        var target = ResolveDeploymentTarget(game, payload);
        return await RunExclusiveAsync(async () =>
        {
            var progress = new Progress<DeploymentProgress>(update =>
            {
                ReportProgress(update.Phase, update.Subject, update.Current, update.Total);
            });
            var result = await _frameGen.CleanAsync(
                target.Directory,
                progress,
                CancellationToken.None);
            if (result.IsFailure)
            {
                throw new BridgeException(result.Error!.Code, result.Error.Diagnostic ?? "cleanup failed");
            }

            return await BuildGamesStateAsync();
        });
    }

    private async Task PersistTargetAsync(GameEntry game, string? exePath)
    {
        if (exePath is null ||
            string.Equals(exePath, game.RenderExePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            await _library.SetTargetAsync(game.Id, exePath, CancellationToken.None);
        }
        catch (ArgumentException exception)
        {
            throw new BridgeException(Domain.Errors.ErrorCode.InvalidPath, exception.Message);
        }
    }

    private static (string Directory, string? ExePath) ResolveDeploymentTarget(
        GameEntry game,
        JsonElement payload)
    {
        var requested = payload.TryGetProperty("target", out var value)
            ? value.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(requested))
        {
            return (game.TargetDirectory, null);
        }

        if (requested == "__manual__")
        {
            throw new BridgeException(
                Domain.Errors.ErrorCode.InvalidPath,
                "select the game's actual launch EXE first");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(requested);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new BridgeException(Domain.Errors.ErrorCode.InvalidPath, exception.Message);
        }

        if (File.Exists(fullPath))
        {
            var directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new BridgeException(
                    Domain.Errors.ErrorCode.InvalidPath,
                    "the selected target has no parent directory");
            }

            return (directory, fullPath);
        }

        if (Directory.Exists(fullPath))
        {
            return (fullPath, null);
        }

        throw new BridgeException(
            Domain.Errors.ErrorCode.InvalidPath,
            "the selected target does not exist");
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
