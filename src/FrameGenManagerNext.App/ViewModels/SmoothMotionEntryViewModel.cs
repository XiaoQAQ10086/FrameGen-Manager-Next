using CommunityToolkit.Mvvm.ComponentModel;
using FrameGenManagerNext.App.Localization;
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

    public string ExeDisplay => ExePath ?? L.T("Library.UnidentifiedExe");

    [ObservableProperty]
    private string _routeDisplay = "—";

    [ObservableProperty]
    private string _componentDisplay = L.T("Common.Checking");

    [ObservableProperty]
    private string _driverDisplay = L.T("Common.Checking");

    [ObservableProperty]
    private string _statusDisplay = L.T("Common.Checking");

    public bool HasExecutable => !string.IsNullOrWhiteSpace(ExePath);

    public void ApplyInspection(SmoothMotionInspection inspection)
    {
        Route = inspection.Route;
        RouteDisplay = RouteLabel(inspection.Route);
        ComponentDisplay = DeploymentLabel(inspection.FileState);
        DriverDisplay = inspection.Setting.Enabled switch
        {
            true => L.T("SmoothMotion.Driver.On"),
            false => L.T("SmoothMotion.Driver.Off"),
            _ => L.T("SmoothMotion.Driver.Unknown"),
        };
        StatusDisplay = inspection.Setting.Enabled == true ? L.T("Common.Enabled") : L.T("Common.Disabled");
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
            GpuRoute.NoTensorCore => L.T("SmoothMotion.Route.NoTensorCore"),
            GpuRoute.NotNeeded => "RTX 40/50",
            GpuRoute.Unsupported => L.T("SmoothMotion.Route.Unsupported"),
            _ => L.T("Common.Unknown"),
        };
    }

    private static string DeploymentLabel(DeploymentState state)
    {
        return state switch
        {
            DeploymentState.Installed => L.T("SmoothMotion.Component.Deployed"),
            DeploymentState.Outdated => L.T("SmoothMotion.Component.Outdated"),
            DeploymentState.Partial => L.T("SmoothMotion.Component.Partial"),
            DeploymentState.Conflict => L.T("SmoothMotion.Component.Conflict"),
            DeploymentState.InstalledWithoutManifest => L.T("SmoothMotion.Component.Present"),
            _ => L.T("State.NotDeployed"),
        };
    }
}
