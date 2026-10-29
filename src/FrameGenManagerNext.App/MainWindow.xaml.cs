using System.IO;
using System.Windows;
using FrameGenManagerNext.App.Web;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Wpf.Ui.Controls;

namespace FrameGenManagerNext.App;

public partial class MainWindow : FluentWindow
{
    private readonly AppWebBridge _bridge;
    private readonly ILogger<MainWindow> _logger;
    private bool _initialized;

    public MainWindow(AppWebBridge bridge, ILogger<MainWindow> logger)
    {
        _bridge = bridge;
        _logger = logger;
        InitializeComponent();
        _bridge.Failed += OnBridgeFailed;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        try
        {
            await _bridge.InitializeAsync(Browser);
        }
        catch (Exception exception) when (
            exception is WebView2RuntimeNotFoundException or InvalidOperationException or IOException)
        {
            _logger.LogError(exception, "WebView2 initialization failed");
            ShowFallback(exception.Message);
        }
    }

    private void OnBridgeFailed(object? sender, string reason)
    {
        Dispatcher.Invoke(() => ShowFallback($"WebView2 浏览器进程异常退出：{reason}"));
    }

    private void ShowFallback(string message)
    {
        Browser.Visibility = Visibility.Collapsed;
        Fallback.Visibility = Visibility.Visible;
        FallbackText.Text = message;
    }
}
