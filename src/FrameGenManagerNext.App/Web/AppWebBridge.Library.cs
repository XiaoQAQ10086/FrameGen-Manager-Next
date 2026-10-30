using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using FrameGenManagerNext.App.Localization;
using FrameGenManagerNext.Core.Features.Framegen;
using FrameGenManagerNext.Core.Features.Library;
using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Library;
using Microsoft.Win32;

namespace FrameGenManagerNext.App.Web;

public sealed partial class AppWebBridge
{
    private readonly SemaphoreSlim _addGameGate = new(1, 1);
    private readonly ConcurrentDictionary<string, List<object>> _exeTargetsCache =
        new(StringComparer.OrdinalIgnoreCase);

    private List<object> GetExeTargets(string directory)
    {
        string key;
        try
        {
            key = string.IsNullOrWhiteSpace(directory)
                ? string.Empty
                : Path.GetFullPath(directory);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return [];
        }

        return _exeTargetsCache.GetOrAdd(key, EnumerateExeTargets);
    }

    private static object? MapFrameGenOptions(IReadOnlyDictionary<string, string>? values)
    {
        if (values is null || values.Count == 0)
        {
            return null;
        }

        return new
        {
            proxySlot = Read("proxySlot"),
            optimized = Read("optimized"),
            frames = Read("maxGeneratedFrames"),
            preset = Read("preset"),
            engine = Read("engine"),
            superResolutionMode = Read("superResolutionMode"),
            xeSsFlow = Read("xessFlowResolution"),
            xeSsHudless = Read("xessHudless"),
        };

        string? Read(string key) => values.TryGetValue(key, out var value) ? value : null;
    }

    private async Task<object?> BuildGamesStateAsync(string? warning = null)
    {
        var settings = _settings.Load();
        var library = await _library.LoadAsync(CancellationToken.None);
        var visible = library.EnumerateVisibleGames().ToList();
        var entries = new object?[visible.Count];
        var states = new DeploymentState[visible.Count];

        // Inspecting every game and loading covers used to be sequential, which made
        // the library appear several seconds after the window opened.
        using var gate = new SemaphoreSlim(Math.Max(1, Environment.ProcessorCount));
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

                var smoothMotionOn = false;
                if (!string.IsNullOrWhiteSpace(game.RenderExePath))
                {
                    try
                    {
                        var setting = await _nvapiSmoothMotion.GetStateAsync(
                            game.RenderExePath,
                            CancellationToken.None);
                        smoothMotionOn = setting.Enabled == true;
                    }
                    catch (Exception)
                    {
                        smoothMotionOn = false;
                    }
                }

                entries[index] = new
                {
                    id = game.Id,
                    name = game.Name,
                    launcher = game.Launcher.ToString(),
                    api = game.GraphicsApi.ToString(),
                    state = state.ToString(),
                    smoothMotionOn,
                    packVersion = inspection.IsSuccess
                        ? inspection.Value.PackVersion
                        : null,
                    legacyTool = inspection.IsSuccess && inspection.Value.FromLegacyTool,
                    options = inspection.IsSuccess
                        ? MapFrameGenOptions(inspection.Value.Options)
                        : null,
                    targetDirectory = game.TargetDirectory,
                    renderExe = game.RenderExePath,
                    targets = GetExeTargets(game.TargetDirectory),
                    initial = string.IsNullOrWhiteSpace(game.Name) ? "?" : game.Name[..1].ToUpperInvariant(),
                    cover = settings.ShowCovers
                        ? await GetCoverUrlAsync(game)
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

        var removed = await BuildRemovedGamesAsync(library);

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
                _exeTargetsCache.Clear();
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
        if (!await _addGameGate.WaitAsync(0))
        {
            return null;
        }

        try
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
                _exeTargetsCache.Clear();
                return await BuildGamesStateAsync();
            });
        }
        finally
        {
            _addGameGate.Release();
        }
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

    private async Task<object?> PickGameExecutableAsync(System.Text.Json.JsonElement payload)
    {
        var game = await FindGameAsync(IdOf(payload));
        if (game is null)
        {
            throw new BridgeException(Domain.Errors.ErrorCode.FileNotFound, "game not found");
        }

        if (!await _addGameGate.WaitAsync(0))
        {
            return null;
        }

        try
        {
            var dialog = new OpenFileDialog
            {
                Title = L.T("Deploy.SelectExe"),
                Filter = "EXE (*.exe)|*.exe",
                CheckFileExists = true,
                Multiselect = false,
            };

            if (Directory.Exists(game.TargetDirectory))
            {
                dialog.InitialDirectory = game.TargetDirectory;
            }

            if (dialog.ShowDialog() != true)
            {
                return null;
            }

            return new
            {
                path = dialog.FileName,
                name = Path.GetFileName(dialog.FileName),
            };
        }
        finally
        {
            _addGameGate.Release();
        }
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

    private async Task<List<object>> BuildRemovedGamesAsync(GameLibrary library)
    {
        var removed = new List<object>();
        foreach (var game in library.EnumerateRemovedGames()
                     .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            removed.Add(new
            {
                id = game.Id,
                name = game.Name,
                launcher = game.Launcher.ToString(),
                api = game.GraphicsApi.ToString(),
                targetDirectory = game.TargetDirectory,
                initial = string.IsNullOrWhiteSpace(game.Name) ? "?" : game.Name[..1].ToUpperInvariant(),
                cover = await GetCoverUrlAsync(game),
            });
        }

        return removed;
    }

}
