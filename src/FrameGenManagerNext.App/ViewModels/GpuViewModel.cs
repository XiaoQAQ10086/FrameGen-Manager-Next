using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrameGenManagerNext.Core.Features.Gpu;
using FrameGenManagerNext.Domain.Gpu;
using FrameGenManagerNext.Domain.Results;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.App.ViewModels;

public sealed partial class GpuViewModel : ObservableObject
{
    private const string SpoofWarning =
        "改名后显卡名与硬件 ID 不一致，内核级反作弊可能判定异常；NVIDIA App/驱动更新可能识别错误。\n" +
        "只修改注册表中的 DeviceDesc，需要管理员权限，重启后生效。";

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

    public string SpoofWarningText { get; } = SpoofWarning;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplySpoofCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestoreDriverCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestoreOriginalCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "尚未读取 GPU 信息";

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
    private string _spoofStatus = "未检测";

    [ObservableProperty]
    private string _selectedSpoofPreset = GpuSpoofPresets.UiChoices[0];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestoreOriginalCommand))]
    private bool _hasOriginalBackup;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ElevationDisplay))]
    private bool _isElevated;

    public string ElevationDisplay => IsElevated ? "已提升" : "普通用户";

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
        StatusText = "正在读取 GPU 信息...";

        try
        {
            var gpu = await _gpuService.GetAsync(CancellationToken.None);
            GpuName = gpu.Name;
            DriverVersion = string.IsNullOrWhiteSpace(gpu.DriverVersion) ? "未知" : gpu.DriverVersion;
            RouteDisplay = RouteLabel(gpu.Route);
            FrameGenerationDisplay = GateLabel(gpu.FrameGeneration);
            SmoothMotionDisplay = GateLabel(gpu.SmoothMotion);
            HagsDisplay = HagsLabel(gpu.HardwareAcceleratedGpuScheduling);
            Diagnostic = gpu.Diagnostic ?? string.Empty;
            HasDiagnostic = !string.IsNullOrWhiteSpace(Diagnostic);
            await RefreshSpoofStateAsync();
            StatusText = "已刷新";
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not read GPU information");
            StatusText = "读取 GPU 信息失败";
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

        if (!Confirm($"把显卡名改为：\n{SelectedSpoofPreset}\n\n{SpoofWarning}\n\n继续？"))
        {
            return;
        }

        await RunSpoofOperationAsync(
            () => _spoofService.ApplyAsync(SelectedSpoofPreset, CancellationToken.None),
            "正在修改显卡名称（会弹出一次 UAC）...");
    }

    [RelayCommand(CanExecute = nameof(CanModify))]
    private async Task RestoreDriverAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (!Confirm("将显卡名写回驱动记录的名称，等于彻底去掉伪装。重启后生效。继续？"))
        {
            return;
        }

        await RunSpoofOperationAsync(
            () => _spoofService.RestoreAsync(GpuSpoofRestoreTarget.DriverName, CancellationToken.None),
            "正在还原显卡名称（会弹出一次 UAC）...");
    }

    [RelayCommand(CanExecute = nameof(CanRestoreOriginal))]
    private async Task RestoreOriginalAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (!Confirm("将显卡名恢复为本工具第一次修改之前的值。重启后生效。继续？"))
        {
            return;
        }

        await RunSpoofOperationAsync(
            () => _spoofService.RestoreAsync(GpuSpoofRestoreTarget.OriginalValue, CancellationToken.None),
            "正在恢复原始显卡名称（会弹出一次 UAC）...");
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
                StatusText = "显卡名称操作失败";
                return;
            }

            SpoofMessage = result.Value.Message;
            await RefreshSpoofStateAsync();
            StatusText = "显卡名称操作完成";
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "GPU spoof operation failed");
            SpoofMessage = exception.Message;
            StatusText = "显卡名称操作失败";
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
            SpoofStatus = state.Error ?? "未找到 NVIDIA 显示适配器";
            HasOriginalBackup = false;
            return;
        }

        SpoofCurrent = adapter.CurrentName;
        SpoofDriver = adapter.DriverName;
        SpoofStatus = adapter.Spoofed ? "已伪装" : "未伪装";
        HasOriginalBackup = adapter.HasOriginalBackup;
    }

    private static bool Confirm(string message)
    {
        return MessageBox.Show(
            message,
            "显卡名称伪装",
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
            GpuRoute.Sm86 => "RTX 30 系 / RTX A 系（SM86）",
            GpuRoute.Sm75 => "RTX 20 系 / Quadro RTX（SM75）",
            GpuRoute.Gtx16 => "GTX 16 系（无 Tensor Core）",
            GpuRoute.NoTensorCore => "Pascal / MX 等（无 Tensor Core）",
            GpuRoute.NotNeeded => "RTX 40 / 50 系（原生帧生成）",
            GpuRoute.Unsupported => "非 NVIDIA 显卡",
            _ => "未能识别",
        };
    }

    private static string GateLabel(GpuFeatureGate gate)
    {
        return gate switch
        {
            GpuFeatureGate.Supported => "支持",
            GpuFeatureGate.DriverTooOld => "驱动过旧",
            GpuFeatureGate.GpuNotSupported => "GPU 不支持",
            GpuFeatureGate.GpuNotNeeded => "无需部署",
            _ => "未知",
        };
    }

    private static string HagsLabel(bool? hags)
    {
        return hags switch
        {
            true => "已开启",
            false => "已关闭",
            _ => "未知",
        };
    }
}
