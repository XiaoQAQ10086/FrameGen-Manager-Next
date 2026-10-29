using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using FrameGenManagerNext.Core.Features.Framegen;
using FrameGenManagerNext.Core.Features.Library;
using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Library;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace FrameGenManagerNext.App.Web;

public sealed partial class AppWebBridge
{
    private async Task<object?> BuildGamesStateAsync(string? warning = null)
    {
        var settings = _settings.Load();
        var library = await _library.LoadAsync(CancellationToken.None);
        var visible = library.EnumerateVisibleGames().ToList();
        var entries = new object?[visible.Count];
        var states = new DeploymentState[visible.Count];

        // Inspecting every game and loading covers used to be sequential, which made
        // the library appear several seconds after the window opened.
        using var gate = new SemaphoreSlim(4);
        await Task.WhenAll(visible.Select(async (game, index) =>
        {
            await gate.WaitAsync();
            try
            {
                var inspection = await _frameGen.InspectAsync(
                    game.TargetDirectory,
                    FrameGenOptions.Default,
                    CancellationToken.None);
                var state = inspection.IsSuccess
                    ? inspection.Value.State
                    : DeploymentState.NotDeployed;
                states[index] = state;

                entries[index] = new
                {
                    id = game.Id,
                    name = game.Name,
                    launcher = game.Launcher.ToString(),
                    api = game.GraphicsApi.ToString(),
                    state = state.ToString(),
                    targetDirectory = game.TargetDirectory,
                    renderExe = game.RenderExePath,
                    initial = string.IsNullOrWhiteSpace(game.Name) ? "?" : game.Name[..1].ToUpperInvariant(),
                    cover = settings.ShowCovers
                        ? await GetCoverDataUrlAsync(game)
                        : null,
                };
            }
            finally
            {
                gate.Release();
            }
        }));

        var games = entries.Select(entry => entry!).ToList();
        var installed = states.Count(state => state == DeploymentState.Installed);

        var removed = library.EnumerateRemovedGames()
            .OrderBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(game => new
            {
                id = game.Id,
                name = game.Name,
                launcher = game.Launcher.ToString(),
                api = game.GraphicsApi.ToString(),
                targetDirectory = game.TargetDirectory,
                initial = string.IsNullOrWhiteSpace(game.Name) ? "?" : game.Name[..1].ToUpperInvariant(),
            })
            .ToList();

        return new
        {
            games,
            removed,
            counts = new
            {
                total = games.Count,
                installed,
            },
            options = new
            {
                proxySlots = FrameGenOptionCatalog.ProxySlots,
                optimizedLevels = FrameGenOptionCatalog.OptimizedLevels,
                frameMultipliers = FrameGenOptionCatalog.FrameMultipliers,
                presets = FrameGenOptionCatalog.Presets,
                engines = FrameGenOptionCatalog.Engines,
                superResolutionModes = FrameGenOptionCatalog.SuperResolutionModes,
                xeSsFlowResolutions = FrameGenOptionCatalog.XeSsFlowResolutions,
                xeSsHudlessValues = FrameGenOptionCatalog.XeSsHudlessValues,
            },
            warning,
        };
    }

    private async Task<object?> ScanGamesAsync()
    {
        return await RunExclusiveAsync(async () =>
        {
            _scanCancellation = new CancellationTokenSource();
            try
            {
                var progress = new Progress<GameScanProgress>(update =>
                {
                    ReportProgress(update.Phase, update.Subject, update.Current, update.Total);
                });

                var result = await _library.ScanAsync(progress, _scanCancellation.Token);
                var warning = result.Warnings.Count > 0
                    ? string.Join(Environment.NewLine, result.Warnings)
                    : null;
                return await BuildGamesStateAsync(warning);
            }
            catch (OperationCanceledException)
            {
                return await BuildGamesStateAsync();
            }
            finally
            {
                _scanCancellation?.Dispose();
                _scanCancellation = null;
            }
        });
    }

    private object? CancelScan()
    {
        _scanCancellation?.Cancel();
        return new { cancelled = true };
    }

    private async Task<object?> AddGameAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择游戏渲染 EXE",
            Filter = "可执行文件 (*.exe)|*.exe",
            CheckFileExists = true,
            Multiselect = false,
        };

        if (dialog.ShowDialog() != true)
        {
            return null;
        }

        return await RunExclusiveAsync(async () =>
        {
            await _library.AddManualAsync(dialog.FileName, CancellationToken.None);
            return await BuildGamesStateAsync();
        });
    }

    private async Task<object?> RemoveGameAsync(System.Text.Json.JsonElement payload)
    {
        var id = IdOf(payload);
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new BridgeException(Domain.Errors.ErrorCode.InvalidArgument, "missing game id");
        }

        return await RunExclusiveAsync(async () =>
        {
            await _library.RemoveAsync(id, CancellationToken.None);
            return await BuildGamesStateAsync();
        });
    }

    private async Task<object?> RestoreGameAsync(System.Text.Json.JsonElement payload)
    {
        var id = IdOf(payload);
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new BridgeException(Domain.Errors.ErrorCode.InvalidArgument, "missing game id");
        }

        return await RunExclusiveAsync(async () =>
        {
            await _library.SetIgnoredAsync(id, ignored: false, CancellationToken.None);
            return await BuildGamesStateAsync();
        });
    }

    private async Task<object?> OpenGameFolderAsync(System.Text.Json.JsonElement payload)
    {
        var game = await FindGameAsync(IdOf(payload));
        if (game is null || !Directory.Exists(game.TargetDirectory))
        {
            throw new BridgeException(Domain.Errors.ErrorCode.InvalidPath, "target directory not found");
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{game.TargetDirectory}\"",
            UseShellExecute = true,
        });
        return new { opened = true };
    }

    private async Task<GameEntry?> FindGameAsync(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var library = await _library.LoadAsync(CancellationToken.None);
        return library.EnumerateVisibleGames().FirstOrDefault(game => game.Id == id);
    }

    private async Task<string?> GetCoverDataUrlAsync(GameEntry game)
    {
        try
        {
            var path = await _covers.GetCoverPathAsync(game, CancellationToken.None);
            if (path is null || !File.Exists(path))
            {
                return await GetExecutableIconDataUrlAsync(game.RenderExePath);
            }

            var info = new FileInfo(path);
            if (info.Length is <= 0 or > 3 * 1024 * 1024)
            {
                return await GetExecutableIconDataUrlAsync(game.RenderExePath);
            }

            var bytes = await File.ReadAllBytesAsync(path, CancellationToken.None);
            var mime = Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase)
                ? "image/png"
                : "image/jpeg";
            return $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug(exception, "Could not load cover for {Game}", game.Name);
            return null;
        }
    }
    private async Task<string?> GetExecutableIconDataUrlAsync(string? exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            return null;
        }

        try
        {
            var cacheDirectory = Path.Combine(_paths.CacheDirectory, "covers", "icons");
            Directory.CreateDirectory(cacheDirectory);
            var key = Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes($"{exePath.ToLowerInvariant()}|{File.GetLastWriteTimeUtc(exePath).Ticks}")));
            var cachePath = Path.Combine(cacheDirectory, $"{key}.png");

            if (!File.Exists(cachePath))
            {
                await Task.Run(() =>
                {
                    using var icon = Icon.ExtractAssociatedIcon(exePath);
                    if (icon is null)
                    {
                        return;
                    }

                    using var bitmap = icon.ToBitmap();
                    bitmap.Save(cachePath, ImageFormat.Png);
                });
            }

            if (!File.Exists(cachePath) || new FileInfo(cachePath).Length > 3 * 1024 * 1024)
            {
                return null;
            }

            var bytes = await File.ReadAllBytesAsync(cachePath, CancellationToken.None);
            return $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or ExternalException)
        {
            _logger.LogDebug(exception, "Could not extract executable icon for {Exe}", exePath);
            return null;
        }
    }

}
