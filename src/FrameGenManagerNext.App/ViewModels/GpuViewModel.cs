using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrameGenManagerNext.Core.Features.Gpu;
using FrameGenManagerNext.Domain.Gpu;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.App.ViewModels;

public sealed partial class GpuViewModel : ObservableObject
{
    private readonly IGpuService _gpuService;
    private readonly ILogger<GpuViewModel> _logger;
    private bool _initialized;

    public GpuViewModel(IGpuService gpuService, ILogger<GpuViewModel> logger)
    {
        _gpuService = gpuService;
        _logger = logger;
    }

    [ObservableProperty]
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
