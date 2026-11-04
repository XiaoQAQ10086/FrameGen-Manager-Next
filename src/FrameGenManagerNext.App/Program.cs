using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using FrameGenManagerNext.App.Localization;
using FrameGenManagerNext.Infrastructure.Installation;
using FrameGenManagerNext.Infrastructure.Paths;

namespace FrameGenManagerNext.App;

public static class Program
{
    private const string InstanceMutexName = @"Local\FrameGenManagerNext.App";

    private static Mutex? _instanceMutex;

    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Any(argument => string.Equals(argument, "--uninstall-cleanup", StringComparison.OrdinalIgnoreCase)))
        {
            UninstallCleanup.Run();
            return;
        }

        LocalizationService.Current.SetLanguage(LocalizationService.DetectSystemLanguage());
        try
        {
            Run();
        }
        catch (Exception exception)
        {
            WriteStartupFailure(exception);
        }
    }

    private static void Run()
    {
        // WebView2 keeps a SQLite-backed profile per user. Two hosts sharing it can
        // kill each other's browser process, so only one instance is allowed.
        _instanceMutex = new Mutex(initiallyOwned: false, InstanceMutexName);
        try
        {
            if (!_instanceMutex.WaitOne(TimeSpan.Zero))
            {
                MessageBox.Show(
                    L.T("Startup.AlreadyRunning"),
                    L.T("Startup.FailedTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }
        }
        catch (AbandonedMutexException)
        {
            // The previous process exited without releasing the mutex; we own it now.
        }

        var application = new App();
        application.InitializeComponent();
        application.Run();
    }

    private static void WriteStartupFailure(Exception exception)
    {
        var logPath = "(unavailable)";
        try
        {
            var paths = AppPaths.CreateDefault();
            Directory.CreateDirectory(paths.LogsDirectory);
            logPath = Path.Combine(paths.LogsDirectory, "startup-failure.log");
            File.AppendAllText(
                logPath,
                $"{DateTimeOffset.Now:O} {RuntimeInformation.OSDescription} {RuntimeInformation.ProcessArchitecture}{Environment.NewLine}" +
                $"{exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (Exception logException) when (
            logException is IOException or UnauthorizedAccessException)
        {
            // The error dialog below still carries the exception message.
        }

        MessageBox.Show(
            $"{L.F("Startup.FailedMessage", logPath)}{Environment.NewLine}{Environment.NewLine}{exception.Message}",
            L.T("Startup.FailedTitle"),
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
