using CommunityToolkit.Mvvm.ComponentModel;
using FrameGenNext.Domain;

namespace FrameGenNext.App.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _status = "Phase 0 · 基础框架已就绪";

    public string ProductName { get; } = AppInfo.ProductName;

    public string Version { get; } = $"v{AppInfo.Version}";

    public string Tagline { get; } = ".NET 10 · WPF · Wpf.Ui";
}
