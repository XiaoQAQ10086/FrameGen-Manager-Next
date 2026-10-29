using System.IO;
using System.Text.Json;
using FrameGenManagerNext.Core.Features.App;
using FrameGenManagerNext.Core.Features.Diagnostics;
using FrameGenManagerNext.Core.Features.Dlss5;
using FrameGenManagerNext.Core.Features.Framegen;
using FrameGenManagerNext.Core.Features.Gpu;
using FrameGenManagerNext.Core.Features.Library;
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
    private readonly IGpuService _gpu;
    private readonly IGpuSpoofService _spoof;
    private readonly IGameCoverService _covers;
    private readonly IDiagnosticsService _diagnostics;
    private readonly ISettingsService _settings;
    private readonly IAppEnvironment _environment;
    private readonly AppPaths _paths;
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly ILogger<AppWebBridge> _logger;

    private WebView2? _webView;
    private CancellationTokenSource? _scanCancellation;

    public event EventHandler<string>? Failed;

    public AppWebBridge(
        IGameLibraryService library,
        IFrameGenDeploymentService frameGen,
        ISmoothMotionDeploymentService smoothMotion,
        IDlss5DeploymentService dlss5,
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
        _gpu = gpu;
        _spoof = spoof;
        _covers = covers;
        _diagnostics = diagnostics;
        _settings = settings;
        _environment = environment;
        _paths = paths;
        _logger = logger;
    }

    public void Dispose()
    {
        _scanCancellation?.Cancel();
        _scanCancellation?.Dispose();
        _scanCancellation = null;
        _operationGate.Dispose();
        try
        {
            if (_webView?.CoreWebView2 is not null)
            {
                _webView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
            }
        }
        catch (InvalidOperationException)
        {
            // Browser process may already be gone during shutdown.
        }

        _webView = null;
    }

    public async Task InitializeAsync(WebView2 webView)
    {
        _webView = webView;
        var userDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FrameGen-Manager-Next",
            "webview2");
        Directory.CreateDirectory(userDataFolder);
        var environment = await CoreWebView2Environment.CreateAsync(
            browserExecutableFolder: null,
            userDataFolder,
            options: null);
        await webView.EnsureCoreWebView2Async(environment);

        var assets = Path.Combine(AppContext.BaseDirectory, "WebAssets");
        webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "fgm.local",
            assets,
            CoreWebView2HostResourceAccessKind.Allow);
        webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
        webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
        webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
        webView.CoreWebView2.ProcessFailed += (_, args) =>
        {
            _logger.LogError("WebView2 process failed: {Kind}", args.ProcessFailedKind);
            Failed?.Invoke(this, args.ProcessFailedKind.ToString());
        };
        webView.Source = new Uri("https://fgm.local/index.html");
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
            "gpu.state" => await BuildGpuStateAsync(),
            "gpu.spoof.apply" => await ApplySpoofAsync(payload),
            "gpu.spoof.restore" => await RestoreSpoofAsync(payload),
            "games.state" => await BuildGamesStateAsync(),
            "games.scan" => await ScanGamesAsync(),
            "games.cancelScan" => CancelScan(),
            "games.add" => await AddGameAsync(),
            "games.remove" => await RemoveGameAsync(payload),
            "games.openFolder" => await OpenGameFolderAsync(payload),
            "framegen.deploy" => await DeployFrameGenAsync(payload),
            "framegen.restore" => await RestoreFrameGenAsync(payload),
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
        if (_webView?.CoreWebView2 is null)
        {
            return;
        }

        _webView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(payload, JsonOptions));
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
