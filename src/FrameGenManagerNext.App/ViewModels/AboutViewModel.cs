using System.Diagnostics;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrameGenManagerNext.Core.Features.App;
using FrameGenManagerNext.Domain;

namespace FrameGenManagerNext.App.ViewModels;

public sealed partial class AboutViewModel : ObservableObject
{
    private readonly IAppEnvironment _environment;

    public AboutViewModel(IAppEnvironment environment)
    {
        _environment = environment;
    }

    public string ProductName { get; } = AppInfo.ProductName;

    public string Version { get; } = $"v{AppInfo.Version}";

    public string DataRoot => _environment.DataRoot;

    public string PortableDisplay => _environment.IsPortable ? "是（数据写在程序目录）" : "否（使用本机用户数据目录）";

    public string RuntimeDisplay { get; } = RuntimeInformation.FrameworkDescription;

    public string RepositoryUrl { get; } = "https://github.com/XiaoQAQ10086/FrameGen-Manager-Next";

    [RelayCommand]
    private void OpenDataFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{_environment.DataRoot}\"",
                UseShellExecute = true,
            });
        }
        catch (Exception)
        {
            // The folder may not exist yet or Explorer may be unavailable.
        }
    }

    [RelayCommand]
    private void OpenRepository()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = RepositoryUrl,
                UseShellExecute = true,
            });
        }
        catch (Exception)
        {
            // Opening the default browser is best-effort.
        }
    }
}
