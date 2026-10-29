using CommunityToolkit.Mvvm.ComponentModel;
using FrameGenManagerNext.App.Localization;
using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Library;

namespace FrameGenManagerNext.App.ViewModels;

public sealed partial class GameEntryViewModel : ObservableObject
{
    private readonly GameEntry _entry;

    public GameEntryViewModel(GameEntry entry)
    {
        _entry = entry;
        Id = entry.Id;
        Name = entry.Name;
        LauncherDisplay = LauncherLabel(entry.Launcher);
        ApiDisplay = ApiLabel(entry.GraphicsApi);
        TargetDirectory = entry.TargetDirectory;
        RenderExeDisplay = entry.RenderExePath ?? L.T("Library.RenderExeMissing");
        IsManual = entry.IsManual;
    }

    public string Id { get; }

    public string Name { get; }

    public string LauncherDisplay { get; }

    public string ApiDisplay { get; }

    public string TargetDirectory { get; }

    public string RenderExeDisplay { get; }

    public bool IsManual { get; }

    public GameEntry ToEntry()
    {
        return _entry;
    }

    [ObservableProperty]
    private string _deploymentStateDisplay = L.T("Common.Checking");

    [ObservableProperty]
    private DeploymentState _deploymentStateValue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCover))]
    private string? _coverPath;

    public bool HasCover => !string.IsNullOrWhiteSpace(CoverPath);

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
            DeploymentState.Installed => L.T("State.Deployed"),
            DeploymentState.Outdated => L.T("State.Outdated"),
            DeploymentState.Partial => L.T("State.Partial"),
            DeploymentState.Conflict => L.T("State.Conflict"),
            DeploymentState.InstalledWithoutManifest => L.T("State.InstalledWithoutManifest"),
            _ => L.T("State.NotDeployed"),
        };
    }

    private static string LauncherLabel(LauncherKind launcher)
    {
        return launcher switch
        {
            LauncherKind.Steam => "Steam",
            LauncherKind.Epic => "Epic",
            LauncherKind.WeGame => "WeGame",
            LauncherKind.Loose => L.T("Common.Loose"),
            LauncherKind.Manual => L.T("Common.Manual"),
            _ => L.T("Common.Unknown"),
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
            _ => L.T("Common.Unknown"),
        };
    }
}
