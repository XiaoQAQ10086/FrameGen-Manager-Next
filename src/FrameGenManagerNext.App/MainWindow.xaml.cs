using System.Diagnostics;
using System.IO;
using System.Windows;
using FrameGenManagerNext.App.Localization;
using FrameGenManagerNext.App.Platform;
using FrameGenManagerNext.App.Web;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Wpf;
using Wpf.Ui.Controls;

namespace FrameGenManagerNext.App;

public partial class MainWindow : FluentWindow
{
    private const string SoftwareRenderingArguments =
        "--disable-gpu --disable-gpu-compositing --disable-software-rasterizer";

    private const int MaxRecoveryAttempts = 3;

    private readonly AppWebBridge _bridge;
    private readonly ILogger<MainWindow> _logger;

    private WebViewFailure? _lastFailure;
    private string? _lastDetail;
    private int _recoveryAttempts;
    private bool _recovering;
    private bool _closing;

    public MainWindow(AppWebBridge bridge, ILogger<MainWindow> logger)
    {
        _bridge = bridge;
        _logger = logger;
        InitializeComponent();
        LoadingTitle.Text = L.T("Startup.Title");
        LoadingText.Text = L.T("Startup.Loading");
        _bridge.Failed += OnBridgeFailed;
        Loaded += OnLoaded;
        SizeChanged += OnWindowSizeChanged;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var rtss = RtssExclusion.Ensure();
        if (rtss.Changed)
        {
            _logger.LogInformation("RTSS WebView2 exclusion created: {Detail}", rtss.Detail);
        }
        else if (!rtss.Success)
        {
            _logger.LogWarning("Could not create RTSS WebView2 exclusion: {Detail}", rtss.Detail);
        }

        UpdateLoadingSize();
        LoadingPopup.IsOpen = true;
        if (!await TryInitializeAsync(Browser, additionalBrowserArguments: null))
        {
            await RecoverAsync();
        }
    }

    private async Task<bool> TryInitializeAsync(
        WebView2 webView,
        string? additionalBrowserArguments)
    {
        try
        {
            await _bridge.InitializeAsync(webView, additionalBrowserArguments);
            LoadingPopup.IsOpen = false;
            BrowserHost.Visibility = Visibility.Visible;
            Fallback.Visibility = Visibility.Collapsed;
            RetryButton.IsEnabled = true;
            return true;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "WebView2 initialization failed");
            _lastDetail = exception.Message;
            ShowFallback(L.T("Fallback.Generic"), exception.Message, canRetry: true);
            return false;
        }
    }

    private void OnBridgeFailed(object? sender, WebViewFailure failure)
    {
        if (_closing)
        {
            return;
        }

        _lastFailure = failure;
        _lastDetail = failure.Diagnostic;
        Dispatcher.BeginInvoke(new Action(() => _ = RecoverAsync()));
    }

    private async Task RecoverAsync()
    {
        if (_recovering || _closing)
        {
            return;
        }

        _recovering = true;
        try
        {
            while (!_closing && _recoveryAttempts < MaxRecoveryAttempts)
            {
                _recoveryAttempts++;
                var arguments = _recoveryAttempts == 1
                    ? null
                    : SoftwareRenderingArguments;

                _logger.LogWarning(
                    "Recreating WebView2 after a browser failure. Attempt={Attempt}/{Max}; Arguments={Arguments}",
                    _recoveryAttempts,
                    MaxRecoveryAttempts,
                    arguments ?? "-");

                ShowFallback(L.T("Fallback.Reloading"), _lastDetail, canRetry: false);
                if (_recoveryAttempts > 1)
                {
                    await Task.Delay(350);
                }

                if (_closing)
                {
                    return;
                }

                if (await RecreateBrowserAsync(arguments))
                {
                    return;
                }
            }

            ShowFailure();
        }
        finally
        {
            _recovering = false;
        }
    }

    private async Task<bool> RecreateBrowserAsync(string? additionalBrowserArguments)
    {
        var previous = Browser;
        if (WebViewHost.Children.Contains(previous))
        {
            WebViewHost.Children.Remove(previous);
        }

        var replacement = new WebView2
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Visibility = Visibility.Visible,
        };
        WebViewHost.Children.Insert(0, replacement);
        Browser = replacement;

        try
        {
            previous.Dispose();
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ObjectDisposedException)
        {
            // The old browser is already gone together with its process.
        }

        return await TryInitializeAsync(replacement, additionalBrowserArguments);
    }

    private async void OnRetryClick(object sender, RoutedEventArgs e)
    {
        if (_recovering)
        {
            return;
        }

        _recoveryAttempts = 0;
        await RecoverAsync();
    }

    private void ShowFailure()
    {
        var message = _lastFailure?.MentionsRivaTuner == true || IsRivaTunerActive()
            ? L.T("Fallback.Rtss")
            : L.T("Fallback.Generic");
        ShowFallback(message, _lastDetail, canRetry: true);
    }

    private void ShowFallback(string message, string? detail, bool canRetry)
    {
        DetachBrowser();
        LoadingPopup.IsOpen = false;
        BrowserHost.Visibility = Visibility.Collapsed;
        Fallback.Visibility = Visibility.Visible;
        FallbackTitle.Text = L.T("Fallback.Title");
        FallbackText.Text = message;
        FallbackDetail.Text = string.IsNullOrWhiteSpace(detail)
            ? string.Empty
            : L.F("Fallback.Detail", detail);
        RetryButton.Content = L.T("Fallback.Retry");
        RetryButton.IsEnabled = canRetry;
        RetryButton.Visibility = canRetry ? Visibility.Visible : Visibility.Collapsed;
    }

    private void DetachBrowser()
    {
        var browser = Browser;
        if (WebViewHost.Children.Contains(browser))
        {
            WebViewHost.Children.Remove(browser);
        }

        try
        {
            browser.Dispose();
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ObjectDisposedException)
        {
            // The browser process may already be gone.
        }
    }

    private static bool IsRivaTunerActive()
    {
        try
        {
            var hooks = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "RivaTuner Statistics Server",
                "RTSSHooks64.dll");
            if (!File.Exists(hooks))
            {
                return false;
            }

            var processes = Process.GetProcessesByName("RTSS");
            try
            {
                return processes.Length > 0;
            }
            finally
            {
                foreach (var process in processes)
                {
                    process.Dispose();
                }
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return false;
        }
    }

    private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateLoadingSize();
    }

    private void UpdateLoadingSize()
    {
        if (BrowserHost.ActualWidth > 0 && BrowserHost.ActualHeight > 0)
        {
            Loading.Width = BrowserHost.ActualWidth;
            Loading.Height = BrowserHost.ActualHeight;
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _closing = true;
        LoadingPopup.IsOpen = false;
        SizeChanged -= OnWindowSizeChanged;
        _bridge.Failed -= OnBridgeFailed;
    }
}
