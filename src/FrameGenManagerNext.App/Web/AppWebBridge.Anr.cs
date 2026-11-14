using System.IO;
using System.Text.Json;
using FrameGenManagerNext.Core.Features.Anr;
using FrameGenManagerNext.Core.Features.Framegen;
using FrameGenManagerNext.Domain.Deployment;
using Microsoft.Win32;

namespace FrameGenManagerNext.App.Web;

public sealed partial class AppWebBridge
{
    private static readonly string[] AnrProxyOrder = ["dxgi.dll", "winmm.dll", "version.dll"];
    private const string AnrBaiduUrl = "https://pan.baidu.com/s/1DWG8f4767zpjD0Rk4HBX2w?pwd=1234";
    private const string AnrQuarkUrl = "https://pan.quark.cn/s/5708e53de73b";

    private async Task<object?> BuildAnrStateAsync()
    {
        var package = await _anrPackages.GetCachedAsync(CancellationToken.None);
        var settings = _settings.Load();
        var gpu = await _gpu.GetAsync(CancellationToken.None);
        var library = await _library.LoadAsync(CancellationToken.None);
        var visibleGames = library.EnumerateVisibleGames().ToList();
        var allowCoverDownload = visibleGames.Count <= 40;
        var games = new List<object>();

        foreach (var game in visibleGames)
        {
            var inspection = await _anr.InspectAsync(game.TargetDirectory, CancellationToken.None);
            var occupied = await GetOccupiedProxiesAsync(game.TargetDirectory);
            var isAddon = package?.Kind == AnrPackageKind.Addon;
            var proxy = isAddon
                ? null
                : inspection.Proxy ?? ChooseAnrProxy(occupied);
            var exe = game.RenderExePath;
            var hasExe = !string.IsNullOrWhiteSpace(exe) && File.Exists(exe);
            var hasOptiScaler = Directory.Exists(Path.Combine(game.TargetDirectory, "OptiScaler"));
            var hasOptiScalerIni = File.Exists(Path.Combine(game.TargetDirectory, "OptiScaler.ini"));
            var frameGen = await _frameGen.InspectAsync(
                game.TargetDirectory,
                FrameGenOptions.Default,
                CancellationToken.None);
            var frameGenDeployed = frameGen.IsSuccess && frameGen.Value.State is
                DeploymentState.Installed or
                DeploymentState.Outdated or
                DeploymentState.Partial or
                DeploymentState.Conflict or
                DeploymentState.InstalledWithoutManifest;
            var smoothMotionDeployed = false;
            if (hasExe)
            {
                try
                {
                    var smoothMotion = await _smoothMotion.InspectAsync(
                        game.Name,
                        game.TargetDirectory,
                        exe!,
                        CancellationToken.None);
                    smoothMotionDeployed = smoothMotion.IsSuccess && smoothMotion.Value.FileState is
                        DeploymentState.Installed or
                        DeploymentState.Outdated or
                        DeploymentState.Partial or
                        DeploymentState.Conflict or
                        DeploymentState.InstalledWithoutManifest;
                }
                catch (Exception)
                {
                    smoothMotionDeployed = false;
                }
            }

            var cover = settings.ShowCovers
                ? await GetCoverUrlAsync(game, allowCoverDownload)
                : null;

            games.Add(new
            {
                id = game.Id,
                name = game.Name,
                launcher = game.Launcher.ToString(),
                api = game.GraphicsApi.ToString(),
                launchMode = LaunchModeOf(game),
                targetDirectory = game.TargetDirectory,
                renderExe = exe,
                initial = string.IsNullOrWhiteSpace(game.Name) ? "?" : game.Name[..1].ToUpperInvariant(),
                cover,
                state = inspection.State.ToString(),
                version = inspection.Version,
                proxy,
                available = hasExe && package is not null && (isAddon ? hasOptiScalerIni : proxy is not null),
                hasOptiScaler,
                hasOptiScalerIni,
                frameGenDeployed,
                smoothMotionDeployed,
                occupied = occupied.Select(item => new { module = item.Module, owner = item.Owner }),
            });
        }

        return new
        {
            package = package is null
                ? null
                : new { version = package.Version, path = package.ZipPath, kind = package.Kind.ToString() },
            gpu = new
            {
                name = gpu.Name,
                driver = gpu.DriverVersion,
                route = gpu.Route.ToString(),
            },
            cloud = new
            {
                baidu = AnrBaiduUrl,
                quark = AnrQuarkUrl,
            },
            games,
            removed = await BuildRemovedGamesAsync(library),
        };
    }

    private Progress<AnrDownloadProgress> CreateAnrProgress()
    {
        return new Progress<AnrDownloadProgress>(update =>
        {
            var percent = update.TotalBytes > 0
                ? Math.Clamp(update.BytesReceived * 100.0 / update.TotalBytes, 0, 100)
                : 0;
            SendEvent("anr.progress", new
            {
                phase = update.Phase,
                received = update.BytesReceived,
                total = update.TotalBytes,
                percent,
                speed = update.BytesPerSecond,
                source = update.Source,
            });
        });
    }

    private async Task<object?> CheckAnrUpdateAsync()
    {
        var cached = await _anrPackages.GetCachedAsync(CancellationToken.None);
        if (cached is null || cached.Kind != AnrPackageKind.Full)
        {
            return new { available = false };
        }

        var latest = await _anrPackages.CheckLatestAsync(CancellationToken.None);
        if (latest.IsFailure)
        {
            return new { available = false };
        }

        var available = !string.Equals(
            cached.Version,
            latest.Value.Version,
            StringComparison.OrdinalIgnoreCase);
        return new
        {
            available,
            current = cached.Version,
            latest = latest.Value.Version,
        };
    }

    private async Task<object?> UpdateAnrAsync()
    {
        return await RunExclusiveAsync(async () =>
        {
            var cached = await _anrPackages.GetCachedAsync(CancellationToken.None);
            if (cached is null)
            {
                throw new BridgeException(
                    Domain.Errors.ErrorCode.PackNotFound,
                    "download or import ANR before updating");
            }

            if (cached.Kind != AnrPackageKind.Full)
            {
                throw new BridgeException(
                    Domain.Errors.ErrorCode.InvalidArgument,
                    "automatic ANR updates only support the full package");
            }

            var package = await _anrPackages.EnsureLatestAsync(
                CreateAnrProgress(),
                CancellationToken.None);
            if (package.IsFailure)
            {
                throw new BridgeException(
                    package.Error!.Code,
                    package.Error.Diagnostic ?? "ANR update failed");
            }

            if (string.Equals(package.Value.Version, cached.Version, StringComparison.OrdinalIgnoreCase))
            {
                throw new BridgeException(
                    Domain.Errors.ErrorCode.NetworkError,
                    "the newer ANR package could not be downloaded");
            }

            var library = await _library.LoadAsync(CancellationToken.None);
            var updated = 0;
            var failures = new List<string>();
            foreach (var game in library.EnumerateVisibleGames())
            {
                var inspection = await _anr.InspectAsync(game.TargetDirectory, CancellationToken.None);
                if (inspection.State is not (DeploymentState.Installed or DeploymentState.Partial))
                {
                    continue;
                }

                var exe = game.RenderExePath;
                if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
                {
                    failures.Add($"{game.Name}: render executable not found");
                    continue;
                }

                try
                {
                    EnsureGameStopped(game, exe);
                }
                catch (BridgeException exception)
                {
                    failures.Add($"{game.Name}: {exception.Code}");
                    continue;
                }

                var proxy = inspection.Proxy;
                if (proxy is null)
                {
                    proxy = ChooseAnrProxy(await GetOccupiedProxiesAsync(game.TargetDirectory));
                }

                var deployed = await _anr.DeployAsync(
                    game.TargetDirectory,
                    exe,
                    proxy,
                    progress: null,
                    CancellationToken.None);
                if (deployed.IsFailure)
                {
                    failures.Add($"{game.Name}: {deployed.Error!.Code}");
                }
                else
                {
                    updated++;
                }
            }

            SendEvent("anr.updated", new
            {
                updated,
                failed = failures.Count,
                failures,
            });
            return await BuildAnrStateAsync();
        });
    }

    private async Task<object?> DownloadAnrAsync(JsonElement payload)
    {
        return await RunExclusiveAsync(async () =>
        {
            var result = await _anrPackages.EnsureLatestAsync(
                CreateAnrProgress(),
                CancellationToken.None);
            if (result.IsFailure)
            {
                throw new BridgeException(
                    result.Error!.Code,
                    result.Error.Diagnostic ?? "ANR download failed");
            }

            return await BuildAnrStateAsync();
        });
    }

    private async Task<object?> ImportAnrAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "导入 ANR 压缩包",
            Filter = "ANR 压缩包 (*.zip)|*.zip",
            CheckFileExists = true,
            Multiselect = false,
        };

        if (dialog.ShowDialog() != true)
        {
            return null;
        }

        return await RunExclusiveAsync(async () =>
        {
            var result = await _anrPackages.ImportAsync(dialog.FileName, CancellationToken.None);
            if (result.IsFailure)
            {
                throw new BridgeException(
                    result.Error!.Code,
                    result.Error.Diagnostic ?? "ANR import failed");
            }

            return await BuildAnrStateAsync();
        });
    }

    private async Task<object?> DeployAnrAsync(JsonElement payload)
    {
        var game = await FindGameAsync(IdOf(payload));
        if (game is null)
        {
            throw new BridgeException(Domain.Errors.ErrorCode.FileNotFound, "game not found");
        }

        var exe = game.RenderExePath;
        if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
        {
            throw new BridgeException(Domain.Errors.ErrorCode.FileNotFound, "render executable not found");
        }

        var package = await _anrPackages.GetCachedAsync(CancellationToken.None);
        if (package is null)
        {
            throw new BridgeException(
                Domain.Errors.ErrorCode.PackNotFound,
                "download or import ANR before deploying");
        }

        string? proxy = null;
        if (package.Kind != AnrPackageKind.Addon)
        {
            var occupied = await GetOccupiedProxiesAsync(game.TargetDirectory);
            var requested = payload.TryGetProperty("proxy", out var proxyValue) &&
                            proxyValue.ValueKind == JsonValueKind.String
                ? proxyValue.GetString()
                : null;
            if (!string.IsNullOrWhiteSpace(requested))
            {
                proxy = AnrProxyOrder.FirstOrDefault(
                    slot => string.Equals(slot, requested, StringComparison.OrdinalIgnoreCase));
                if (proxy is null)
                {
                    throw new BridgeException(
                        Domain.Errors.ErrorCode.InvalidArgument,
                        $"unknown proxy slot: {requested}");
                }

                var conflict = occupied.FirstOrDefault(item =>
                    item.Owner != "ANR" &&
                    string.Equals(item.Module, proxy, StringComparison.OrdinalIgnoreCase));
                if (conflict is not null)
                {
                    throw new BridgeException(
                        Domain.Errors.ErrorCode.Conflict,
                        $"proxy slot {proxy} is used by {conflict.Owner}");
                }
            }
            else
            {
                proxy = ChooseAnrProxy(occupied);
            }

            if (proxy is null)
            {
                throw new BridgeException(
                    Domain.Errors.ErrorCode.Conflict,
                    "dxgi.dll, winmm.dll and version.dll are all in use");
            }
        }

        EnsureGameStopped(game, exe);

        return await RunExclusiveAsync(async () =>
        {
            var progress = new Progress<DeploymentProgress>(update =>
            {
                ReportProgress(update.Phase, update.Subject, update.Current, update.Total);
            });
            var result = await _anr.DeployAsync(
                game.TargetDirectory,
                exe,
                proxy,
                progress,
                CancellationToken.None);
            if (result.IsFailure)
            {
                throw new BridgeException(
                    result.Error!.Code,
                    result.Error.Diagnostic ?? "ANR deployment failed");
            }

            return await BuildAnrStateAsync();
        });
    }

    private async Task<object?> RestoreAnrAsync(JsonElement payload)
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
            var result = await _anr.RestoreAsync(
                game.TargetDirectory,
                progress,
                CancellationToken.None);
            if (result.IsFailure)
            {
                throw new BridgeException(
                    result.Error!.Code,
                    result.Error.Diagnostic ?? "ANR restore failed");
            }

            return await BuildAnrStateAsync();
        });
    }

    private static string? ChooseAnrProxy(IReadOnlyList<ProxyOccupancy> occupied)
    {
        foreach (var module in AnrProxyOrder)
        {
            if (occupied.All(item =>
                    !string.Equals(item.Module, module, StringComparison.OrdinalIgnoreCase)))
            {
                return module;
            }
        }

        return null;
    }
}
