using System.IO;
using System.Windows.Controls;
using FrameGenManagerNext.App.Web;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;

namespace FrameGenManagerNext.App.Views;

public partial class WebFrameGenPage : UserControl
{
    private readonly WebFrameGenBridge _bridge;
    private readonly ILogger<WebFrameGenPage> _logger;
    private bool _initialized;

    public WebFrameGenPage(WebFrameGenBridge bridge, ILogger<WebFrameGenPage> logger)
    {
        InitializeComponent();
        _bridge = bridge;
        _logger = logger;
        _bridge.Failed += OnBridgeFailed;
        Loaded += OnLoaded;
    }

    private void OnBridgeFailed(object? sender, string reason)
    {
        Dispatcher.Invoke(() =>
        {
            Browser.Visibility = System.Windows.Visibility.Collapsed;
            Fallback.Visibility = System.Windows.Visibility.Visible;
            FallbackText.Text = $"WebView2 浏览器进程异常退出：{reason}";
        });
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
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
            Browser.Visibility = System.Windows.Visibility.Collapsed;
            Fallback.Visibility = System.Windows.Visibility.Visible;
            FallbackText.Text = exception.Message;
        }
    }
}
