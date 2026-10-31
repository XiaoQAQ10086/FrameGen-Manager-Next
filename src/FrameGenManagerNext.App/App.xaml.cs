using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using FrameGenManagerNext.App.Localization;
using FrameGenManagerNext.App.Web;
using FrameGenManagerNext.Core.Features.App;
using FrameGenManagerNext.Core.Features.Settings;
using FrameGenManagerNext.Infrastructure.Composition;
using FrameGenManagerNext.Infrastructure.Paths;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace FrameGenManagerNext.App;

public partial class App : Application
{
    internal static Stopwatch StartupClock { get; } = Stopwatch.StartNew();

    private readonly IHost _host;

    public App()
    {
        // The WPF side is only window chrome; WebView2 renders the real UI. Keeping
        // WPF on software rendering avoids a second D3D device (and RTSS hooking it).
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

        var paths = AppPaths.CreateDefault();
        paths.EnsureCreated();

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.File(
                Path.Combine(paths.LogsDirectory, "app-.log"),
                formatProvider: CultureInfo.InvariantCulture,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true)
            .CreateLogger();

        DispatcherUnhandledException += (_, eventArgs) =>
        {
            Log.Fatal(eventArgs.Exception, "Unhandled UI exception");
            MessageBox.Show(
                $"FrameGen Manager-Next hit an unexpected error.\n\n{eventArgs.Exception.Message}",
                "FrameGen Manager-Next",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            eventArgs.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            if (eventArgs.ExceptionObject is Exception exception)
            {
                Log.Fatal(exception, "Unhandled application exception");
            }
        };

        Log.Information("startup.hostBuilding elapsed={Elapsed}ms", StartupClock.ElapsedMilliseconds);
        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices(services =>
            {
                services.AddFrameGenServices(paths);
                services.AddSingleton<AppWebBridge>();
                services.AddSingleton<MainWindow>();
            })
            .Build();
        Log.Information("startup.hostBuilt elapsed={Elapsed}ms", StartupClock.ElapsedMilliseconds);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            _host.Start();
            Log.Information("startup.hostStarted elapsed={Elapsed}ms", StartupClock.ElapsedMilliseconds);
            var settings = _host.Services.GetRequiredService<ISettingsService>().Load();
            LocalizationService.Current.SetLanguage(settings.Language);
            _host.Services.GetRequiredService<IEfficiencyModeService>().SetBackground(true);
        }
        catch (Exception exception)
        {
            Log.Fatal(exception, "Application host failed to start");
            MessageBox.Show(
                $"FrameGen Manager-Next failed to start.\n\n{exception.Message}",
                "FrameGen Manager-Next",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
            return;
        }

        var window = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Show();

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        _host.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
