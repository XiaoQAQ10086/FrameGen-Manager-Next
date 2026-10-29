using System.Diagnostics;
using System.IO;
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
        var games = new List<object>();
        var installed = 0;

        foreach (var game in library.EnumerateVisibleGames())
        {
            var inspection = await _frameGen.InspectAsync(
                game.TargetDirectory,
                FrameGenOptions.Default,
                CancellationToken.None);
            var state = inspection.IsSuccess ? inspection.Value.State : DeploymentState.NotDeployed;
            if (state == DeploymentState.Installed)
            {
                installed++;
            }

            games.Add(new
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
            });
        }

        return new
        {
            games,
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
                return null;
            }

            var info = new FileInfo(path);
            if (info.Length is <= 0 or > 3 * 1024 * 1024)
            {
                return null;
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
}
