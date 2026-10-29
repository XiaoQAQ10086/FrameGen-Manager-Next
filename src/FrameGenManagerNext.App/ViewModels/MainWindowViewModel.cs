using FrameGenManagerNext.Domain;

namespace FrameGenManagerNext.App.ViewModels;

public sealed class MainWindowViewModel
{
    public string ProductName { get; } = AppInfo.ProductName;

    public string Version { get; } = $"v{AppInfo.Version}";
}
