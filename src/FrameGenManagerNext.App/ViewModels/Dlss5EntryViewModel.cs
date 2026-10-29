using CommunityToolkit.Mvvm.ComponentModel;
using FrameGenManagerNext.App.Localization;
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

    public string ExeDisplay => ExePath ?? L.T("Library.UnidentifiedExe");

    public bool HasExecutable => !string.IsNullOrWhiteSpace(ExePath);

    [ObservableProperty]
    private string _apiDisplay = "—";

    [ObservableProperty]
    private string _fileDisplay = L.T("Common.Checking");

    [ObservableProperty]
    private string _reShadeDisplay = "—";

    [ObservableProperty]
    private string _statusDisplay = L.T("Common.Checking");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInstall))]
    [NotifyPropertyChangedFor(nameof(CanUninstall))]
    private bool _legacyRenoDxPresent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUninstall))]
    private bool _hasLeftovers;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUninstall))]
    private bool _reShadePresent;

    public bool CanInstall => HasExecutable && !LegacyRenoDxPresent;

    public bool CanUninstall => HasExecutable && (ReShadePresent || LegacyRenoDxPresent || HasLeftovers);

    public void ApplyInspection(Dlss5Inspection inspection)
    {
        ApiDisplay = ApiLabel(inspection.Api);
        FileDisplay = FileLabel(inspection.FileState);
        LegacyRenoDxPresent = inspection.LegacyRenoDxPresent;
        HasLeftovers = inspection.HasLeftovers;
        ReShadePresent = inspection.ReShadePresent;

        ReShadeDisplay = inspection.LegacyRenoDxPresent
            ? L.T("Dlss5.Legacy")
            : inspection.ReShadePresent
                ? inspection.ReShadeModule ?? L.T("Common.Installed")
                : L.T("Common.NotInstalled");

        StatusDisplay = inspection.LegacyRenoDxPresent
            ? L.T("Dlss5.RequiresUninstall")
            : FileLabel(inspection.FileState);
    }

    public void SetUnavailable(string message)
    {
        ApiDisplay = "—";
        FileDisplay = "—";
        ReShadeDisplay = "—";
        StatusDisplay = message;
        LegacyRenoDxPresent = false;
        HasLeftovers = false;
        ReShadePresent = false;
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

    private static string FileLabel(DeploymentState state)
    {
        return state switch
        {
            DeploymentState.Installed => L.T("Common.Installed"),
            DeploymentState.Outdated => L.T("State.Outdated"),
            DeploymentState.Partial => L.T("State.Partial"),
            DeploymentState.Conflict => L.T("State.Conflict"),
            DeploymentState.InstalledWithoutManifest => L.T("State.Exists"),
            _ => L.T("Common.NotInstalled"),
        };
    }
}
