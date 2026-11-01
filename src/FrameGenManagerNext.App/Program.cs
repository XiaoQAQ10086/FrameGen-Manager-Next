using FrameGenManagerNext.Infrastructure.Installation;

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
