using FrameGenManagerNext.Infrastructure.Installation;
using Velopack;

namespace FrameGenManagerNext.App;

public static class Program
{
    private const string InstanceMutexName = @"Local\FrameGenManagerNext.App";

    private static Mutex? _instanceMutex;

    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build()
            .OnBeforeUninstallFastCallback(_ => UninstallCleanup.Run())
            .Run();

        // WebView2 keeps a SQLite-backed profile per user. Two hosts sharing it can
        // kill each other's browser process, so only one instance is allowed.
        _instanceMutex = new Mutex(initiallyOwned: false, InstanceMutexName);
        try
        {
            if (!_instanceMutex.WaitOne(TimeSpan.Zero))
            {
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
}
