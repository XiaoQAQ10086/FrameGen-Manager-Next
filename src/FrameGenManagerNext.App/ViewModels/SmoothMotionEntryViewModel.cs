using CommunityToolkit.Mvvm.ComponentModel;
using FrameGenManagerNext.Core.Features.SmoothMotion;
using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Gpu;
using FrameGenManagerNext.Domain.Library;

namespace FrameGenManagerNext.App.ViewModels;

public sealed partial class SmoothMotionEntryViewModel : ObservableObject
{
    public SmoothMotionEntryViewModel(GameEntry entry)
    {
        GameName = entry.Name;
        TargetDirectory = entry.TargetDirectory;
        ExePath = entry.RenderExePath;
        Route = GpuRoute.Unknown;
    }

    public string GameName { get; }

    public string TargetDirectory { get; }

    public string? ExePath { get; }

    public GpuRoute Route { get; private set; }

    public string ExeDisplay => ExePath ?? "未识别渲染 EXE";

    [ObservableProperty]
    private string _routeDisplay = "—";

    [ObservableProperty]
    private string _componentDisplay = "检查中";

    [ObservableProperty]
    private string _driverDisplay = "检查中";

    [ObservableProperty]
    private string _statusDisplay = "检查中";

    public bool HasExecutable => !string.IsNullOrWhiteSpace(ExePath);

    public void ApplyInspection(SmoothMotionInspection inspection)
    {
        Route = inspection.Route;
        RouteDisplay = RouteLabel(inspection.Route);
        ComponentDisplay = DeploymentLabel(inspection.FileState);
        DriverDisplay = inspection.Setting.Enabled switch
        {
            true => "驱动开启",
            false => "驱动关闭",
            _ => "驱动未知/未设置",
        };
        StatusDisplay = inspection.Setting.Enabled == true ? "已开启" : "未开启";
    }

    public void SetUnavailable(string message)
    {
        RouteDisplay = "—";
        ComponentDisplay = "—";
        DriverDisplay = "—";
        StatusDisplay = message;
    }

    private static string RouteLabel(GpuRoute route)
    {
        return route switch
        {
            GpuRoute.Sm86 => "RTX 30 (SM86)",
            GpuRoute.Sm75 => "RTX 20 (SM75)",
            GpuRoute.Gtx16 => "GTX 16",
            GpuRoute.NoTensorCore => "无 Tensor Core",
            GpuRoute.NotNeeded => "RTX 40/50",
            GpuRoute.Unsupported => "非 NVIDIA",
            _ => "未知",
        };
    }

    private static string DeploymentLabel(DeploymentState state)
    {
        return state switch
        {
            DeploymentState.Installed => "组件已部署",
            DeploymentState.Outdated => "组件需更新",
            DeploymentState.Partial => "组件不完整",
            DeploymentState.Conflict => "文件冲突",
            DeploymentState.InstalledWithoutManifest => "组件已存在",
            _ => "未部署",
        };
    }
}
