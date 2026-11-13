using System.Diagnostics;
using System.IO;
using System.Text.Json;
using FrameGenManagerNext.Core.Features.Dlss5;
using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Library;
using FrameGenManagerNext.Infrastructure.Library;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.App.Web;

public sealed partial class AppWebBridge
{
    private async Task<object?> BuildDlss5StateAsync(
        Dlss5UninstallReport? uninstall = null,
        string? reframeworkSkipReason = null)
    {
        var stateClock = Stopwatch.StartNew();
        var settings = _settings.Load();
        var library = await _library.LoadAsync(CancellationToken.None);
        var games = new List<object>();
        var allowCoverDownload = library.EnumerateVisibleGames().Count() <= 40;

        foreach (var game in library.EnumerateVisibleGames())
        {
            var cover = settings.ShowCovers ? await GetCoverUrlAsync(game, allowCoverDownload) : null;
            var initial = string.IsNullOrWhiteSpace(game.Name) ? "?" : game.Name[..1].ToUpperInvariant();
            var targets = GetExeTargets(game.TargetDirectory);

            if (string.IsNullOrWhiteSpace(game.RenderExePath))
            {
                games.Add(new
                {
                    id = game.Id,
                    name = game.Name,
                    launcher = game.Launcher.ToString(),
                    targetDirectory = game.TargetDirectory,
                    initial,
                    cover,
                    renderExe = game.RenderExePath,
                    targets,
                    available = false,
                    state = "Unavailable",
                    api = game.GraphicsApi.ToString(),
                    engine = ResolveEngine(game).ToString(),
                    reShade = false,
                    legacy = false,
                    legacyFiles = Array.Empty<string>(),
                    leftovers = false,
                });
                continue;
            }

            var inspection = await _dlss5.InspectAsync(
                game.TargetDirectory,
                game.RenderExePath,
                CancellationToken.None,
                game.GraphicsApi);
            if (inspection.IsFailure)
            {
                games.Add(new
                {
                    id = game.Id,
                    name = game.Name,
                    launcher = game.Launcher.ToString(),
                    targetDirectory = game.TargetDirectory,
                    initial,
                    cover,
                    renderExe = game.RenderExePath,
                    targets,
                    available = false,
                    state = inspection.Error!.Code.ToString(),
                    api = game.GraphicsApi.ToString(),
                    engine = ResolveEngine(game).ToString(),
                    reShade = false,
                    legacy = false,
                    legacyFiles = Array.Empty<string>(),
                    leftovers = false,
                });
                continue;
            }

            games.Add(new
            {
                id = game.Id,
                name = game.Name,
                launcher = game.Launcher.ToString(),
                targetDirectory = game.TargetDirectory,
                initial,
                cover,
                renderExe = game.RenderExePath,
                targets,
                available = true,
                state = inspection.Value.FileState.ToString(),
                api = inspection.Value.Api.ToString(),
                engine = ResolveEngine(game).ToString(),
                reShade = inspection.Value.ReShadePresent,
                legacy = inspection.Value.LegacyRenoDxPresent,
                legacyFiles = inspection.Value.LegacyFiles ?? Array.Empty<string>(),
                leftovers = inspection.Value.HasLeftovers,
            });
        }

        var removed = await BuildRemovedGamesAsync(library);
        _logger.LogInformation(
            "dlss5.state count={Count} elapsed={Elapsed}ms",
            games.Count,
            stateClock.ElapsedMilliseconds);
        return new
        {
            games,
            removed,
            reframeworkSkipped = reframeworkSkipReason is not null,
            reframeworkSkipReason,
            uninstall = uninstall is null
                ? null
                : new
                {
                    removed = uninstall.Removed,
                    remaining = uninstall.Remaining,
                    clean = uninstall.IsClean,
                },
        };
    }

    private async Task<object?> InstallDlss5Async(JsonElement payload)
    {
        var game = await FindGameAsync(IdOf(payload));
        if (game is null)
        {
            throw new BridgeException(Domain.Errors.ErrorCode.FileNotFound, "game not found");
        }

        var target = ResolveDeploymentTarget(game, payload);
        var installExe = target.ExePath ?? game.RenderExePath;
        if (string.IsNullOrWhiteSpace(installExe) || !File.Exists(installExe))
        {
            throw new BridgeException(Domain.Errors.ErrorCode.FileNotFound, "render executable not found");
        }
        EnsureGameStopped(game, installExe);
        var knownApi = ReadGraphicsApi(payload);
        var selectedProxy = ReadProxyModule(payload);
        var apiModule = selectedProxy ??
                        (knownApi is null
                            ? Dlss5ProxyCatalog.AutoModule(game.GraphicsApi)
                            : Dlss5ProxyCatalog.ExplicitModule(knownApi.Value));
        if (apiModule is not null)
        {
            var occupied = await GetOccupiedProxiesAsync(target.Directory);
            var conflict = occupied.FirstOrDefault(item =>
                item.Owner != "Dlss5" &&
                string.Equals(item.Module, apiModule, StringComparison.OrdinalIgnoreCase));
            if (conflict is not null)
            {
                throw new BridgeException(
                    Domain.Errors.ErrorCode.Conflict,
                    $"proxy slot {apiModule} is used by {conflict.Owner}; choose another proxy");
            }
        }

        var options = new Dlss5InstallOptions(
            knownApi,
            selectedProxy,
            ReadOverlayKey(payload));

        return await RunExclusiveAsync(async () =>
        {
            await PersistTargetAsync(game, target.ExePath);

            var progress = new Progress<DeploymentProgress>(update =>
            {
                ReportProgress(update.Phase, update.Subject, update.Current, update.Total);
            });
            var reframeworkSkipReason = await EnsureReframeworkAsync(
                game,
                target.Directory,
                progress);

            var result = await _dlss5.InstallAsync(
                target.Directory,
                installExe,
                progress,
                CancellationToken.None,
                options);
            if (result.IsFailure)
            {
                throw new BridgeException(result.Error!.Code, result.Error.Diagnostic ?? "install failed");
            }

            return await BuildDlss5StateAsync(reframeworkSkipReason: reframeworkSkipReason);
        });
    }

    private async Task<object?> SetDlss5HotkeyAsync(JsonElement payload)
    {
        var game = await FindGameAsync(IdOf(payload));
        if (game is null)
        {
            throw new BridgeException(Domain.Errors.ErrorCode.FileNotFound, "game not found");
        }

        var target = ResolveDeploymentTarget(game, payload);
        var hotkey = ReadOverlayKey(payload) ??
                     throw new BridgeException(
                         Domain.Errors.ErrorCode.InvalidArgument,
                         "missing ReShade overlay key");

        return await RunExclusiveAsync(async () =>
        {
            var result = await _dlss5.SetOverlayKeyAsync(
                target.Directory,
                target.ExePath ?? game.RenderExePath,
                hotkey,
                CancellationToken.None);
            if (result.IsFailure)
            {
                throw new BridgeException(
                    result.Error!.Code,
                    result.Error.Diagnostic ?? "could not update ReShade overlay key");
            }

            return new
            {
                updated = result.Value.Applied,
                queued = result.Value.Queued,
                hotkey,
            };
        });
    }

    private async Task<object?> RestoreDlss5Async(JsonElement payload)
    {
        var game = await FindGameAsync(IdOf(payload));
        if (game is null)
        {
            throw new BridgeException(Domain.Errors.ErrorCode.FileNotFound, "game not found");
        }

        EnsureGameStopped(game);
        return await RunExclusiveAsync(async () =>
        {
            var progress = new Progress<DeploymentProgress>(update =>
            {
                ReportProgress(update.Phase, update.Subject, update.Current, update.Total);
            });
            var result = await _dlss5.RestoreAsync(
                game.TargetDirectory,
                progress,
                CancellationToken.None);
            if (result.IsFailure)
            {
                throw new BridgeException(result.Error!.Code, result.Error.Diagnostic ?? "restore failed");
            }

            return await BuildDlss5StateAsync();
        });
    }

    private async Task<object?> UninstallDlss5Async(JsonElement payload)
    {
        var game = await FindGameAsync(IdOf(payload));
        if (game is null)
        {
            throw new BridgeException(Domain.Errors.ErrorCode.FileNotFound, "game not found");
        }

        EnsureGameStopped(game);
        return await RunExclusiveAsync(async () =>
        {
            var progress = new Progress<DeploymentProgress>(update =>
            {
                ReportProgress(update.Phase, update.Subject, update.Current, update.Total);
            });
            var result = await _dlss5.UninstallAsync(
                game.TargetDirectory,
                game.RenderExePath,
                progress,
                CancellationToken.None);
            if (result.IsFailure)
            {
                throw new BridgeException(result.Error!.Code, result.Error.Diagnostic ?? "uninstall failed");
            }

            var report = result.Value;
            if (!report.IsClean)
            {
                _logger.LogWarning(
                    "dlss5.uninstall target={Target} removed={Removed} remaining={Remaining}",
                    game.TargetDirectory,
                    report.Removed.Count,
                    report.Remaining.Count);
            }

            var reframework = await _reframework.UninstallAsync(
                game.TargetDirectory,
                progress: null,
                CancellationToken.None);
            if (reframework.IsFailure &&
                reframework.Error!.Code != Domain.Errors.ErrorCode.ManifestMissing)
            {
                _logger.LogWarning(
                    "reframework.uninstall target={Target} code={Code} diagnostic={Diagnostic}",
                    game.TargetDirectory,
                    reframework.Error.Code,
                    reframework.Error.Diagnostic);
            }

            return await BuildDlss5StateAsync(report);
        });
    }

    private static GameEngine ResolveEngine(GameEntry game)
    {
        if (game.Engine != GameEngine.Unknown)
        {
            return game.Engine;
        }

        return EngineDetector.Detect(
            game.InstallDirectory,
            game.TargetDirectory,
            game.RenderExePath);
    }

    private static List<object> EnumerateExeTargets(string directory)
    {
        try
        {
            if (!Directory.Exists(directory))
            {
                return [];
            }

            return Directory.EnumerateFiles(directory, "*.exe", SearchOption.TopDirectoryOnly)
                .Select(path => new { path, name = Path.GetFileName(path) })
                .OrderBy(item => item.name, StringComparer.CurrentCultureIgnoreCase)
                .Take(64)
                .Cast<object>()
                .ToList();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static string? ReadProxyModule(JsonElement payload)
    {
        var value = payload.TryGetProperty("proxy", out var element) ? element.GetString() : null;
        if (string.IsNullOrWhiteSpace(value) || value.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var module = value.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            ? value
            : value + ".dll";
        if (!Dlss5ProxyCatalog.Modules.Contains(module, StringComparer.OrdinalIgnoreCase))
        {
            throw new BridgeException(
                Domain.Errors.ErrorCode.InvalidArgument,
                $"unknown ReShade proxy module: {value}");
        }

        return module;
    }

    private static int? ReadOverlayKey(JsonElement payload)
    {
        if (!payload.TryGetProperty("hotkey", out var element))
        {
            return null;
        }

        var value = element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var number)
            ? number
            : int.TryParse(element.GetString(), out number)
                ? number
                : 0;
        if (value is < 1 or > 255)
        {
            throw new BridgeException(
                Domain.Errors.ErrorCode.InvalidArgument,
                $"invalid ReShade overlay key: {value}");
        }

        return value;
    }

    private static GraphicsApi? ReadGraphicsApi(JsonElement payload)
    {
        var value = payload.TryGetProperty("api", out var element) ? element.GetString() : null;
        if (string.IsNullOrWhiteSpace(value) || value.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return value.ToUpperInvariant() switch
        {
            "DX9" or "D3D9" => GraphicsApi.Direct3D9,
            "DX10" or "D3D10" => GraphicsApi.Direct3D10,
            "DX11" or "D3D11" => GraphicsApi.Direct3D11,
            "DX12" or "D3D12" => GraphicsApi.Direct3D12,
            "VULKAN" => GraphicsApi.Vulkan,
            "OPENGL" or "GL" => GraphicsApi.OpenGl,
            _ => null,
        };
    }
}
