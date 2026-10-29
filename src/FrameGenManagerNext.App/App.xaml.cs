using System.Globalization;
using System.IO;
using System.Windows;
using FrameGenManagerNext.App.ViewModels;
using FrameGenManagerNext.App.Views;
using FrameGenManagerNext.Infrastructure.Composition;
using FrameGenManagerNext.Infrastructure.Paths;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace FrameGenManagerNext.App;

public partial class App : Application
{
    private readonly IHost _host;

    public App()
    {
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

        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices(services =>
            {
                services.AddFrameGenServices(paths);

                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<LibraryViewModel>();
                services.AddSingleton<GpuViewModel>();
                services.AddSingleton<SmoothMotionViewModel>();
                services.AddSingleton<LogsViewModel>();
                services.AddSingleton<AboutViewModel>();

                services.AddSingleton<LibraryPage>();
                services.AddSingleton<GpuPage>();
                services.AddSingleton<SmoothMotionPage>();
                services.AddSingleton<LogsPage>();
                services.AddSingleton<AboutPage>();
                services.AddSingleton<MainWindow>();
            })
            .Build();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            _host.Start();
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
