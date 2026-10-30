using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FrameGenManagerNext.Core.Features.App;
using FrameGenManagerNext.Core.Features.Diagnostics;
using FrameGenManagerNext.Core.Features.Dlss5;
using FrameGenManagerNext.Core.Features.Framegen;
using FrameGenManagerNext.Core.Features.Gpu;
using FrameGenManagerNext.Core.Features.Library;
using FrameGenManagerNext.Core.Features.Maintenance;
using FrameGenManagerNext.Core.Features.Settings;
using FrameGenManagerNext.Core.Features.SmoothMotion;
using FrameGenManagerNext.Infrastructure.Paths;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace FrameGenManagerNext.App.Web;

public sealed partial class AppWebBridge : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IGameLibraryService _library;
    private readonly IFrameGenDeploymentService _frameGen;
    private readonly ISmoothMotionDeploymentService _smoothMotion;
    private readonly IDlss5DeploymentService _dlss5;
    private readonly IAssetMaintenanceService _maintenance;
    private readonly IGpuService _gpu;
    private readonly IGpuSpoofService _spoof;
    private readonly IGameCoverService _covers;
    private readonly IDiagnosticsService _diagnostics;
    private readonly ISettingsService _settings;
    private readonly IAppEnvironment _environment;
    private readonly AppPaths _paths;
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly ILogger<AppWebBridge> _logger;

    private CoreWebView2? _core;
    private CancellationTokenSource? _scanCancellation;
    private CancellationTokenSource? _assetClearCancellation;
    private AssetUpdateReport? _lastAssetReport;

    public event EventHandler<WebViewFailure>? Failed;

    public AppWebBridge(
        IGameLibraryService library,
        IFrameGenDeploymentService frameGen,
        ISmoothMotionDeploymentService smoothMotion,
        IDlss5DeploymentService dlss5,
        IAssetMaintenanceService maintenance,
        IGpuService gpu,
        IGpuSpoofService spoof,
        IGameCoverService covers,
        IDiagnosticsService diagnostics,
        ISettingsService settings,
        IAppEnvironment environment,
        AppPaths paths,
        ILogger<AppWebBridge> logger)
    {
        _library = library;
        _frameGen = frameGen;
        _smoothMotion = smoothMotion;
        _dlss5 = dlss5;
        _maintenance = maintenance;
        _gpu = gpu;
        _spoof = spoof;
        _covers = covers;
        _diagnostics = diagnostics;
        _settings = settings;
        _environment = environment;
        _paths = paths;
        _logger = logger;
        _ = PrewarmGpuAsync();
    }

    private async Task PrewarmGpuAsync()
    {
        try
        {
            // Start the single per-process GPU probe while WebView2 is still
            // initializing so the first page paint does not wait for it.
            await _gpu.GetAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "GPU prewarm failed");
        }
    }

    public void Dispose()
    {
        _scanCancellation?.Cancel();
        _scanCancellation?.Dispose();
        _scanCancellation = null;
        _assetClearCancellation?.Cancel();
        _assetClearCancellation?.Dispose();
        _assetClearCancellation = null;
        DetachCoreEvents();
        _operationGate.Dispose();
        _addGameGate.Dispose();
    }

    private const string MemoryLimitArguments = "--js-flags=--max-old-space-size=256";

    public async Task InitializeAsync(WebView2 webView, string? additionalBrowserArguments = null)
    {
        DetachCoreEvents();

        // Keep the renderer heap bounded; the UI is a small module app, not a page
        // with large in-memory data sets. This lowers the WebView2 baseline footprint.
        var arguments = string.IsNullOrWhiteSpace(additionalBrowserArguments)
            ? MemoryLimitArguments
            : $"{additionalBrowserArguments} {MemoryLimitArguments}";

        var userDataFolder = ResolveUserDataFolder();
        Directory.CreateDirectory(userDataFolder);
        _logger.LogInformation(
            "Creating WebView2 environment. Profile={Profile}; Runtime={Runtime}; Arguments={Arguments}",
            userDataFolder,
            CoreWebView2Environment.GetAvailableBrowserVersionString(),
            arguments);

        var options = new CoreWebView2EnvironmentOptions(
            additionalBrowserArguments: arguments);
        var environment = await CoreWebView2Environment.CreateAsync(
            browserExecutableFolder: null,
            userDataFolder,
            options);
        await webView.EnsureCoreWebView2Async(environment);

        var core = webView.CoreWebView2;
        _core = core;

        var assets = Path.Combine(AppContext.BaseDirectory, "WebAssets");
        core.SetVirtualHostNameToFolderMapping(
            "fgm.local",
            assets,
            CoreWebView2HostResourceAccessKind.Allow);
        Directory.CreateDirectory(_paths.CacheDirectory);
        core.SetVirtualHostNameToFolderMapping(
            "fgm-covers.local",
            _paths.CacheDirectory,
            CoreWebView2HostResourceAccessKind.Allow);
        core.Settings.AreDevToolsEnabled = true;
        core.Settings.IsStatusBarEnabled = false;
        core.WebMessageReceived += OnWebMessageReceived;
        core.ProcessFailed += OnProcessFailed;
        webView.Source = new Uri("https://fgm.local/index.html");
    }

    private static string ResolveUserDataFolder()
    {
        // A separate profile per installation keeps dev, portable copies and different
        // update folders from fighting over the same WebView2 lock files.
        var location = AppContext.BaseDirectory.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(location)))
            .ToLowerInvariant()[..12];

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FrameGen-Manager-Next",
            "webview2",
            hash);
    }

    private void OnProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs args)
    {
        var failure = new WebViewFailure(
            args.ProcessFailedKind,
            args.Reason,
            args.ExitCode,
            args.ProcessDescription,
            args.FailureSourceModulePath);

        _logger.LogError(
            "WebView2 process failed: kind={Kind}; reason={Reason}; exit={ExitCode}; " +
            "module={Module}; process={Process}",
            failure.Kind,
            failure.Reason,
            failure.ExitCode,
            failure.FailureSourceModulePath ?? "-",
            failure.ProcessDescription ?? "-");

        Failed?.Invoke(this, failure);
    }

    private void DetachCoreEvents()
    {
        var core = _core;
        _core = null;
        if (core is null)
        {
            return;
        }

        try
        {
            core.WebMessageReceived -= OnWebMessageReceived;
            core.ProcessFailed -= OnProcessFailed;
        }
        catch (Exception exception) when (exception is InvalidOperationException or COMException)
        {
            // The browser process may already be gone.
        }
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        _ = HandleMessageAsync(e.WebMessageAsJson);
    }

    private async Task HandleMessageAsync(string json)
    {
        string? id = null;
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            id = root.TryGetProperty("id", out var idValue) ? idValue.GetString() : null;
            var command = root.TryGetProperty("command", out var commandValue)
                ? commandValue.GetString()
                : null;
            var payload = root.TryGetProperty("payload", out var payloadValue)
                ? payloadValue
                : default;

            var data = await DispatchAsync(command, payload);
            SendResponse(id, data);
        }
        catch (BridgeException exception)
        {
            SendError(id, exception.Code.ToString(), exception.Message);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Web bridge command failed");
            SendError(id, "InternalError", exception.Message);
        }
    }

    private async Task<object?> DispatchAsync(string? command, JsonElement payload)
    {
        return command switch
        {
            "app.info" => BuildAppInfo(),
            "app.saveSettings" => await SaveSettingsAsync(payload),
            "app.openData" => OpenDataFolder(),
            "app.openRepository" => OpenRepository(),
            "app.restartComputer" => RestartComputer(),
            "updates.check" => await CheckForUpdatesAsync(),
            "updates.download" => await DownloadUpdateAsync(payload),
            "updates.cancel" => CancelUpdate(),
            "updates.openPage" => OpenUpdatePage(payload),
            "gpu.state" => await BuildGpuStateAsync(),
            "gpu.spoof.apply" => await ApplySpoofAsync(payload),
            "gpu.spoof.restore" => await RestoreSpoofAsync(payload),
            "games.state" => await BuildGamesStateAsync(),
            "games.scan" => await ScanGamesAsync(),
            "games.cancelScan" => CancelScan(),
            "games.add" => await AddGameAsync(),
            "games.remove" => await RemoveGameAsync(payload),
            "games.restore" => await RestoreGameAsync(payload),
            "games.openFolder" => await OpenGameFolderAsync(payload),
            "games.pickExecutable" => await PickGameExecutableAsync(payload),
            "assets.check" => await CheckAssetUpdatesAsync(),
            "assets.clear" => await ClearAssetUpdatesAsync(),
            "assets.cancel" => CancelAssetClear(),
            "framegen.deploy" => await DeployFrameGenAsync(payload),
            "framegen.restore" => await RestoreFrameGenAsync(payload),
            "framegen.clean" => await CleanFrameGenAsync(payload),
            "sm.state" => await BuildSmoothMotionStateAsync(),
            "sm.enable" => await EnableSmoothMotionAsync(payload),
            "sm.disable" => await DisableSmoothMotionAsync(payload),
            "sm.restore" => await RestoreSmoothMotionAsync(payload),
            "dlss5.state" => await BuildDlss5StateAsync(),
            "dlss5.install" => await InstallDlss5Async(payload),
            "dlss5.restore" => await RestoreDlss5Async(payload),
            "dlss5.uninstall" => await UninstallDlss5Async(payload),
            "logs.tail" => await ReadLogsAsync(payload),
            "logs.export" => await ExportLogsAsync(),
            "logs.openFolder" => OpenLogsFolder(),
            "settings.openHags" => OpenHagsSettings(),
            "settings.openFolder" => OpenSettingsFolder(payload),
            _ => throw new BridgeException(Domain.Errors.ErrorCode.InvalidArgument, $"Unknown command: {command}"),
        };
    }

    private async Task<object?> RunExclusiveAsync(Func<Task<object?>> operation)
    {
        if (!await _operationGate.WaitAsync(0))
        {
            throw new BridgeException(Domain.Errors.ErrorCode.FileLocked, "另一个操作正在进行");
        }

        try
        {
            return await operation();
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private void SendResponse(string? id, object? data)
    {
        Send(new { id, ok = true, data });
    }

    private void SendError(string? id, string code, string message)
    {
        Send(new { id, ok = false, error = new { code, message } });
    }

    private void SendEvent(string eventName, object data)
    {
        Send(new { @event = eventName, data });
    }

    private void Send(object payload)
    {
        var core = _core;
        if (core is null)
        {
            return;
        }

        try
        {
            core.PostWebMessageAsJson(JsonSerializer.Serialize(payload, JsonOptions));
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or COMException or ObjectDisposedException)
        {
            // Commands can finish after the browser process has exited. Dropping the
            // response is harmless; MainWindow will recreate the browser.
            _logger.LogDebug("Dropped WebView2 message because the browser is unavailable");
        }
    }

    private void ReportProgress(string phase, string subject, int current, int total)
    {
        SendEvent("progress", new { phase, subject, current, total });
    }

    private static string? IdOf(JsonElement payload)
    {
        return payload.ValueKind == JsonValueKind.Object &&
               payload.TryGetProperty("id", out var id)
            ? id.GetString()
            : null;
    }

    private sealed class BridgeException(Domain.Errors.ErrorCode code, string message) : Exception(message)
    {
        public Domain.Errors.ErrorCode Code { get; } = code;
    }
}
