using CommunityToolkit.Mvvm.ComponentModel;
using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Library;

namespace FrameGenManagerNext.App.ViewModels;

public sealed partial class GameEntryViewModel : ObservableObject
{
    public GameEntryViewModel(GameEntry entry)
    {
        Id = entry.Id;
        Name = entry.Name;
        LauncherDisplay = LauncherLabel(entry.Launcher);
        ApiDisplay = ApiLabel(entry.GraphicsApi);
        TargetDirectory = entry.TargetDirectory;
        RenderExeDisplay = entry.RenderExePath ?? "未识别";
        IsManual = entry.IsManual;
    }

    public string Id { get; }

    public string Name { get; }

    public string LauncherDisplay { get; }

    public string ApiDisplay { get; }

    public string TargetDirectory { get; }

    public string RenderExeDisplay { get; }

    public bool IsManual { get; }

    [ObservableProperty]
    private string _deploymentStateDisplay = "检查中";

    [ObservableProperty]
    private DeploymentState _deploymentStateValue;

    public void ApplyInspection(DeploymentInspection inspection)
    {
        DeploymentStateValue = inspection.State;
        DeploymentStateDisplay = StateLabel(inspection.State);
    }

    public void SetState(DeploymentState state)
    {
        DeploymentStateValue = state;
        DeploymentStateDisplay = StateLabel(state);
    }

    private static string StateLabel(DeploymentState state)
    {
        return state switch
        {
            DeploymentState.Installed => "已部署",
            DeploymentState.Outdated => "需更新",
            DeploymentState.Partial => "不完整",
            DeploymentState.Conflict => "文件冲突",
            DeploymentState.InstalledWithoutManifest => "已安装（无记录）",
            _ => "未部署",
        };
    }

    private static string LauncherLabel(LauncherKind launcher)
    {
        return launcher switch
        {
            LauncherKind.Steam => "Steam",
            LauncherKind.Epic => "Epic",
            LauncherKind.WeGame => "WeGame",
            LauncherKind.Loose => "散装",
            LauncherKind.Manual => "手动",
            _ => "未知",
        };
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
}
