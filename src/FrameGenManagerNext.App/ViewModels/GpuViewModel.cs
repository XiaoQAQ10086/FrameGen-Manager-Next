using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrameGenManagerNext.App.Localization;
using FrameGenManagerNext.Core.Features.Gpu;
using FrameGenManagerNext.Domain.Gpu;
using FrameGenManagerNext.Domain.Results;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.App.ViewModels;

public sealed partial class GpuViewModel : ObservableObject
{
    private readonly IGpuService _gpuService;
    private readonly IGpuSpoofService _spoofService;
    private readonly ILogger<GpuViewModel> _logger;
    private bool _initialized;

    public GpuViewModel(
        IGpuService gpuService,
        IGpuSpoofService spoofService,
        ILogger<GpuViewModel> logger)
    {
        _gpuService = gpuService;
        _spoofService = spoofService;
        _logger = logger;
    }

    public IReadOnlyList<string> SpoofPresets { get; } = GpuSpoofPresets.UiChoices;

    public string SpoofWarningText { get; } = L.T("Gpu.Spoof.Warning");

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplySpoofCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestoreDriverCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestoreOriginalCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = L.T("Gpu.Status.NotLoaded");

    [ObservableProperty]
    private string _gpuName = "—";

    [ObservableProperty]
    private string _driverVersion = "—";

    [ObservableProperty]
    private string _routeDisplay = "—";

    [ObservableProperty]
    private string _frameGenerationDisplay = "—";

    [ObservableProperty]
    private string _smoothMotionDisplay = "—";

    [ObservableProperty]
    private string _hagsDisplay = "—";

    [ObservableProperty]
    private string _diagnostic = string.Empty;

    [ObservableProperty]
    private bool _hasDiagnostic;

    [ObservableProperty]
    private string _spoofCurrent = "—";

    [ObservableProperty]
    private string _spoofDriver = "—";

    [ObservableProperty]
    private string _spoofStatus = L.T("Gpu.Spoof.NotChecked");

    [ObservableProperty]
    private string _selectedSpoofPreset = GpuSpoofPresets.UiChoices[0];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestoreOriginalCommand))]
    private bool _hasOriginalBackup;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ElevationDisplay))]
    private bool _isElevated;

    public string ElevationDisplay => IsElevated ? L.T("Gpu.Elevation.Admin") : L.T("Gpu.Elevation.User");

    [ObservableProperty]
    private string _spoofMessage = string.Empty;

    public async Task LoadAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusText = L.T("Gpu.Status.Reading");

        try
        {
            var gpu = await _gpuService.GetAsync(CancellationToken.None);
            GpuName = gpu.Name;
            DriverVersion = string.IsNullOrWhiteSpace(gpu.DriverVersion) ? L.T("Gpu.Status.UnknownDriver") : gpu.DriverVersion;
            RouteDisplay = RouteLabel(gpu.Route);
            FrameGenerationDisplay = GateLabel(gpu.FrameGeneration);
            SmoothMotionDisplay = GateLabel(gpu.SmoothMotion);
            HagsDisplay = HagsLabel(gpu.HardwareAcceleratedGpuScheduling);
            Diagnostic = gpu.Diagnostic ?? string.Empty;
            HasDiagnostic = !string.IsNullOrWhiteSpace(Diagnostic);
            await RefreshSpoofStateAsync();
            StatusText = L.T("Gpu.Status.Refreshed");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not read GPU information");
            StatusText = L.T("Gpu.Status.ReadFailed");
            Diagnostic = exception.Message;
            HasDiagnostic = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanModify))]
    private async Task ApplySpoofAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (!Confirm(L.F("Gpu.Spoof.ConfirmApply", SelectedSpoofPreset, SpoofWarningText)))
        {
            return;
        }

        await RunSpoofOperationAsync(
            () => _spoofService.ApplyAsync(SelectedSpoofPreset, CancellationToken.None),
            L.T("Gpu.Spoof.Applying"));
    }

    [RelayCommand(CanExecute = nameof(CanModify))]
    private async Task RestoreDriverAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (!Confirm(L.T("Gpu.Spoof.ConfirmDriver")))
        {
            return;
        }

        await RunSpoofOperationAsync(
            () => _spoofService.RestoreAsync(GpuSpoofRestoreTarget.DriverName, CancellationToken.None),
            L.T("Gpu.Spoof.RestoringDriver"));
    }

    [RelayCommand(CanExecute = nameof(CanRestoreOriginal))]
    private async Task RestoreOriginalAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (!Confirm(L.T("Gpu.Spoof.ConfirmOriginal")))
        {
            return;
        }

        await RunSpoofOperationAsync(
            () => _spoofService.RestoreAsync(GpuSpoofRestoreTarget.OriginalValue, CancellationToken.None),
            L.T("Gpu.Spoof.RestoringOriginal"));
    }

    private async Task RunSpoofOperationAsync(
        Func<Task<Result<GpuSpoofOperationReport>>> operation,
        string status)
    {
        IsBusy = true;
        StatusText = status;
        SpoofMessage = string.Empty;

        try
        {
            var result = await operation();
            if (result.IsFailure)
            {
                SpoofMessage = $"{result.Error!.Code}: {result.Error.Diagnostic}";
                StatusText = L.T("Gpu.Spoof.Failed");
                return;
            }

            SpoofMessage = result.Value.Message;
            await RefreshSpoofStateAsync();
            StatusText = L.T("Gpu.Spoof.Done");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "GPU spoof operation failed");
            SpoofMessage = exception.Message;
            StatusText = L.T("Gpu.Spoof.Failed");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshSpoofStateAsync()
    {
        var state = await _spoofService.InspectAsync(CancellationToken.None);
        IsElevated = state.IsElevated;

        var adapter = state.Primary;
        if (adapter is null)
        {
            SpoofCurrent = "—";
            SpoofDriver = "—";
            SpoofStatus = state.Error ?? L.T("Common.NoAdapter");
            HasOriginalBackup = false;
            return;
        }

        SpoofCurrent = adapter.CurrentName;
        SpoofDriver = adapter.DriverName;
        SpoofStatus = adapter.Spoofed ? L.T("Gpu.Spoof.Spoofed") : L.T("Gpu.Spoof.NotSpoofed");
        HasOriginalBackup = adapter.HasOriginalBackup;
    }

    private static bool Confirm(string message)
    {
        return MessageBox.Show(
            message,
            L.T("Gpu.Spoof.DialogTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    private bool CanModify()
    {
        return !IsBusy;
    }

    private bool CanRestoreOriginal()
    {
        return !IsBusy && HasOriginalBackup;
    }

    private static string RouteLabel(GpuRoute route)
    {
        return route switch
        {
            GpuRoute.Sm86 => L.T("Gpu.Route.Sm86"),
            GpuRoute.Sm75 => L.T("Gpu.Route.Sm75"),
            GpuRoute.Gtx16 => L.T("Gpu.Route.Gtx16"),
            GpuRoute.NoTensorCore => L.T("Gpu.Route.NoTensorCore"),
            GpuRoute.NotNeeded => L.T("Gpu.Route.NotNeeded"),
            GpuRoute.Unsupported => L.T("Gpu.Route.Unsupported"),
            _ => L.T("Gpu.Route.Unknown"),
        };
    }

    private static string GateLabel(GpuFeatureGate gate)
    {
        return gate switch
        {
            GpuFeatureGate.Supported => L.T("Gpu.Gate.Supported"),
            GpuFeatureGate.DriverTooOld => L.T("Gpu.Gate.DriverTooOld"),
            GpuFeatureGate.GpuNotSupported => L.T("Gpu.Gate.GpuNotSupported"),
            GpuFeatureGate.GpuNotNeeded => L.T("Gpu.Gate.GpuNotNeeded"),
            _ => L.T("Gpu.Gate.Unknown"),
        };
    }

    private static string HagsLabel(bool? hags)
    {
        return hags switch
        {
            true => L.T("Gpu.Hags.On"),
            false => L.T("Gpu.Hags.Off"),
            _ => L.T("Gpu.Gate.Unknown"),
        };
    }
}
