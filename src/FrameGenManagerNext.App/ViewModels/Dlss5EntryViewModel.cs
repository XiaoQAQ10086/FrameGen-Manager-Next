using CommunityToolkit.Mvvm.ComponentModel;
using FrameGenManagerNext.Core.Features.Dlss5;
using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Library;

namespace FrameGenManagerNext.App.ViewModels;

public sealed partial class Dlss5EntryViewModel : ObservableObject
{
    public Dlss5EntryViewModel(GameEntry entry)
    {
        GameName = entry.Name;
        TargetDirectory = entry.TargetDirectory;
        ExePath = entry.RenderExePath;
    }

    public string GameName { get; }

    public string TargetDirectory { get; }

    public string? ExePath { get; }

    public string ExeDisplay => ExePath ?? "未识别渲染 EXE";

    public bool HasExecutable => !string.IsNullOrWhiteSpace(ExePath);

    [ObservableProperty]
    private string _apiDisplay = "—";

    [ObservableProperty]
    private string _fileDisplay = "检查中";

    [ObservableProperty]
    private string _reShadeDisplay = "—";

    [ObservableProperty]
    private string _statusDisplay = "检查中";

    public void ApplyInspection(Dlss5Inspection inspection)
    {
        ApiDisplay = ApiLabel(inspection.Api);
        FileDisplay = FileLabel(inspection.FileState);
        ReShadeDisplay = inspection.ReShadePresent ? "已安装" : "未安装";
        StatusDisplay = inspection.FileState == DeploymentState.Installed ? "已安装" : "未安装";
    }

    public void SetUnavailable(string message)
    {
        ApiDisplay = "—";
        FileDisplay = "—";
        ReShadeDisplay = "—";
        StatusDisplay = message;
    }

    private static string ApiLabel(GraphicsApi api)
    {
        return api switch
        {
            GraphicsApi.Direct3D12 => "DX12",
            GraphicsApi.Direct3D11 => "DX11",
            GraphicsApi.Direct3D10 => "DX10",
            GraphicsApi.Direct3D9 => "DX9",
            GraphicsApi.Vulkan => "Vulkan",
            GraphicsApi.OpenGl => "OpenGL",
            _ => "未知",
        };
    }

    private static string FileLabel(DeploymentState state)
    {
        return state switch
        {
            DeploymentState.Installed => "已安装",
            DeploymentState.Outdated => "需更新",
            DeploymentState.Partial => "不完整",
            DeploymentState.Conflict => "文件冲突",
            DeploymentState.InstalledWithoutManifest => "已存在",
            _ => "未安装",
        };
    }
}
