using System.Diagnostics;
using System.IO;
using System.Text.Json;
using FrameGenManagerNext.Core.Features.Framegen;
using FrameGenManagerNext.Core.Features.Gpu;
using FrameGenManagerNext.Core.Features.Library;
using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Library;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Win32;

namespace FrameGenManagerNext.App.Web;

public sealed partial class WebFrameGenBridge : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IGameLibraryService _library;
    private readonly IFrameGenDeploymentService _frameGen;
    private readonly IGpuService _gpu;
    private readonly IGpuSpoofService _spoof;
    private readonly IGameCoverService _covers;
    private readonly ILogger<WebFrameGenBridge> _logger;

    private WebView2? _webView;
    private CancellationTokenSource? _scanCancellation;
    private bool _busy;

    public event EventHandler<string>? Failed;

    public WebFrameGenBridge(
        IGameLibraryService library,
        IFrameGenDeploymentService frameGen,
        IGpuService gpu,
        IGpuSpoofService spoof,
        IGameCoverService covers,
        ILogger<WebFrameGenBridge> logger)
    {
        _library = library;
        _frameGen = frameGen;
        _gpu = gpu;
        _spoof = spoof;
        _covers = covers;
        _logger = logger;
    }

    public void Dispose()
    {
        _scanCancellation?.Cancel();
        _scanCancellation?.Dispose();
        _scanCancellation = null;
        try
        {
            if (_webView?.CoreWebView2 is not null)
            {
                _webView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
            }
        }
        catch (InvalidOperationException)
        {
            // The browser process may already be gone during shutdown.
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
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var type = root.TryGetProperty("type", out var typeValue)
                ? typeValue.GetString()
                : null;

            if (type == "ready")
            {
                await SendStateAsync(CancellationToken.None);
                return;
            }

            if (_busy)
            {
                Send("toast", new { message = "上一个操作还没有完成", level = "warn" });
                return;
            }

            switch (type)
            {
                case "scan":
                    await RunScanAsync();
                    break;
                case "refresh":
                    await SendStateAsync(CancellationToken.None);
                    break;
                case "addGame":
                    await AddGameAsync();
                    break;
                case "cancelScan":
                    _scanCancellation?.Cancel();
                    break;
                case "deploy":
                    await DeployAsync(root);
                    break;
                case "restore":
                    await RestoreAsync(root);
                    break;
                case "remove":
                    await RemoveAsync(root);
                    break;
                case "openFolder":
                    await OpenFolderAsync(root);
                    break;
                case "openHags":
                    OpenHagsSettings();
                    break;
                default:
                    _logger.LogDebug("Unknown web message type {Type}", type);
                    break;
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Web bridge message failed");
            Send("error", new { message = exception.Message });
            SetBusy(false);
        }
    }

    private async Task RunScanAsync()
    {
        _scanCancellation = new CancellationTokenSource();
        SetBusy(true);
        Send("progress", new { phase = "扫描", subject = "游戏库", current = 0, total = 1 });
        var progress = new Progress<GameScanProgress>(update =>
        {
            var phase = update.Phase switch
            {
                "launchers" => "正在扫描启动器",
                "games" => "正在探测游戏",
                "done" => "扫描完成",
                _ => update.Phase,
            };
            Send("progress", new
            {
                phase,
                subject = update.Subject,
                current = update.Current,
                total = update.Total,
            });
        });

        try
        {
            var result = await _library.ScanAsync(progress, _scanCancellation.Token);
            await SendStateAsync(CancellationToken.None);
            if (result.Warnings.Count > 0)
            {
                Send("toast", new { message = string.Join(Environment.NewLine, result.Warnings), level = "warn" });
            }
        }
        catch (OperationCanceledException)
        {
            Send("toast", new { message = "扫描已取消", level = "" });
            await SendStateAsync(CancellationToken.None);
        }
        finally
        {
            _scanCancellation?.Dispose();
            _scanCancellation = null;
            SetBusy(false);
        }
    }

    private async Task AddGameAsync()
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
            return;
        }

        SetBusy(true);
        try
        {
            await _library.AddManualAsync(dialog.FileName, CancellationToken.None);
            await SendStateAsync(CancellationToken.None);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task DeployAsync(JsonElement root)
    {
        var game = await FindGameAsync(root);
        if (game is null)
        {
            return;
        }

        var options = ReadOptions(root);
        SetBusy(true);
        try
        {
            var progress = CreateProgress();
            var result = await _frameGen.DeployAsync(
                game.TargetDirectory,
                options,
                progress,
                CancellationToken.None);

            if (result.IsFailure)
            {
                Send("error", new { message = $"{result.Error!.Code}: {result.Error.Diagnostic}" });
                return;
            }

            await SendStateAsync(CancellationToken.None);
            Send("toast", new { message = $"部署完成：{game.Name}", level = "" });
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task RestoreAsync(JsonElement root)
    {
        var game = await FindGameAsync(root);
        if (game is null)
        {
            return;
        }

        SetBusy(true);
        try
        {
            var result = await _frameGen.RestoreAsync(
                game.TargetDirectory,
                CreateProgress(),
                CancellationToken.None);

            if (result.IsFailure)
            {
                Send("error", new { message = $"{result.Error!.Code}: {result.Error.Diagnostic}" });
                return;
            }

            await SendStateAsync(CancellationToken.None);
            Send("toast", new { message = $"还原完成：{game.Name}", level = "" });
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task RemoveAsync(JsonElement root)
    {
        var game = await FindGameAsync(root);
        if (game is null)
        {
            return;
        }

        SetBusy(true);
        try
        {
            await _library.RemoveAsync(game.Id, CancellationToken.None);
            await SendStateAsync(CancellationToken.None);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task OpenFolderAsync(JsonElement root)
    {
        var game = await FindGameAsync(root);
        if (game is null || !Directory.Exists(game.TargetDirectory))
        {
            Send("toast", new { message = "目标目录不存在", level = "warn" });
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{game.TargetDirectory}\"",
            UseShellExecute = true,
        });
    }

    private static void OpenHagsSettings()
    {
        var uri = Environment.OSVersion.Version.Build >= 22000
            ? "ms-settings:display-advancedgraphics-default"
            : "ms-settings:display-advancedgraphics";
        Process.Start(new ProcessStartInfo { FileName = uri, UseShellExecute = true });
    }

    private async Task<GameEntry?> FindGameAsync(JsonElement root)
    {
        var id = GetId(root);
        var library = await _library.LoadAsync(CancellationToken.None);
        var game = library.EnumerateVisibleGames().FirstOrDefault(candidate => candidate.Id == id);
        if (game is null)
        {
            Send("error", new { message = "游戏已不在列表中" });
        }

        return game;
    }

    private static string GetId(JsonElement root)
    {
        return root.TryGetProperty("id", out var id) ? id.GetString() ?? string.Empty : string.Empty;
    }

    private static FrameGenOptions ReadOptions(JsonElement root)
    {
        if (!root.TryGetProperty("options", out var options))
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

    private Progress<DeploymentProgress> CreateProgress()
    {
        return new Progress<DeploymentProgress>(update =>
        {
            var phase = update.Phase switch
            {
                "planning" => "准备",
                "writing" => "写入",
                "restoring" => "还原",
                "done" => "完成",
                _ => update.Phase,
            };
            Send("progress", new
            {
                phase,
                subject = update.Subject,
                current = update.Current,
                total = update.Total,
            });
        });
    }

    private void SetBusy(bool value)
    {
        _busy = value;
        Send("busy", new { value });
    }

    private void Send(string type, object payload)
    {
        if (_webView?.CoreWebView2 is null)
        {
            return;
        }

        var message = JsonSerializer.Serialize(new { type, data = payload }, JsonOptions);
        _webView.CoreWebView2.PostWebMessageAsJson(message);
    }
}
