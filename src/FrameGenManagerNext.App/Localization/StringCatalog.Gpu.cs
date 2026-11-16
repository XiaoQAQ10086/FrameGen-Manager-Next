namespace FrameGenManagerNext.App.Localization;

internal static partial class StringCatalog
{
    private static void AddGpuEntries()
    {
        Add("Gpu.Route.Sm86", "RTX 30 系（SM86）", "RTX 30 series (SM86)");
        Add("Gpu.Route.Sm75", "RTX 20 系 / Quadro RTX（SM75）", "RTX 20 / Quadro RTX (SM75)");
        Add("Gpu.Route.Gtx16", "GTX 16 系（无 Tensor Core）", "GTX 16 series (no Tensor Cores)");
        Add("Gpu.Route.NoTensorCore", "Pascal / MX 等（无 Tensor Core）", "Pascal / MX and similar (no Tensor Cores)");
        Add("Gpu.Route.NotNeeded", "RTX 40 / 50 系（原生帧生成）", "RTX 40 / 50 series (native frame generation)");
        Add("Gpu.Route.Unsupported", "非 NVIDIA 显卡", "Non-NVIDIA GPU");
        Add("Gpu.Route.Unknown", "未能识别", "Not recognized");
        Add("Gpu.Gate.Supported", "支持", "Supported");
        Add("Gpu.Gate.DriverTooOld", "驱动过旧", "Driver too old");
        Add("Gpu.Gate.GpuNotSupported", "GPU 不支持", "GPU not supported");
        Add("Gpu.Gate.GpuNotNeeded", "无需部署", "No deployment needed");
        Add("Gpu.Gate.Unknown", "未知", "Unknown");
        Add("Gpu.Hags.On", "已开启", "Enabled");
        Add("Gpu.Hags.Off", "已关闭", "Disabled");
        Add("Gpu.Hags.Pending", "待重启生效", "Restart required");
        Add("Gpu.Hags.Unsupported", "不支持", "Unsupported");
        Add("Gpu.Hags.Unknown", "未知", "Unknown");
    }
}
