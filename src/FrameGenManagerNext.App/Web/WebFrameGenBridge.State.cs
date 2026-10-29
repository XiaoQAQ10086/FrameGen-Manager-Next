using System.IO;
using FrameGenManagerNext.Core.Features.Framegen;
using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Gpu;
using FrameGenManagerNext.Domain.Library;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.App.Web;

public sealed partial class WebFrameGenBridge
{
    private async Task SendStateAsync(CancellationToken cancellationToken)
    {
        var gpu = await _gpu.GetAsync(cancellationToken);
        var spoof = await _spoof.InspectAsync(cancellationToken);
        var library = await _library.LoadAsync(cancellationToken);
        var games = new List<object>();

        foreach (var game in library.EnumerateVisibleGames())
        {
            var inspection = await _frameGen.InspectAsync(
                game.TargetDirectory,
                FrameGenOptions.Default,
                cancellationToken);
            games.Add(new
            {
                id = game.Id,
                name = game.Name,
                launcher = LauncherLabel(game.Launcher),
                api = ApiLabel(game.GraphicsApi),
                state = StateLabel(inspection.IsSuccess ? inspection.Value.State : DeploymentState.NotDeployed),
                stateClass = StateClass(inspection.IsSuccess ? inspection.Value.State : DeploymentState.NotDeployed),
                targetDirectory = game.TargetDirectory,
                renderExe = game.RenderExePath,
                initial = string.IsNullOrWhiteSpace(game.Name) ? "?" : game.Name[..1].ToUpperInvariant(),
                cover = await GetCoverDataUrlAsync(game, cancellationToken),
            });
        }

        Send("state", new
        {
            gpu = new
            {
                name = gpu.Name,
                driver = gpu.DriverVersion,
                route = RouteLabel(gpu.Route),
                frameGeneration = GateLabel(gpu.FrameGeneration),
                smoothMotion = GateLabel(gpu.SmoothMotion),
                hags = HagsLabel(gpu.HardwareAcceleratedGpuScheduling),
                status = "已刷新",
                spoofCurrent = spoof.Primary?.CurrentName,
                spoofDriver = spoof.Primary?.DriverName,
            },
            games,
            options = new
            {
                proxySlots = FrameGenOptionCatalog.ProxySlots,
                optimizedLevels = FrameGenOptionCatalog.OptimizedLevels,
                frameMultipliers = FrameGenOptionCatalog.FrameMultipliers,
                presets = FrameGenOptionCatalog.Presets,
                engines = FrameGenOptionCatalog.Engines,
                superResolutionModes = FrameGenOptionCatalog.SuperResolutionModes,
                xeSsFlowResolutions = FrameGenOptionCatalog.XeSsFlowResolutions,
                xeSsHudlessValues = FrameGenOptionCatalog.XeSsHudlessValues,
            },
            status = $"已加载 {games.Count} 个游戏",
        });
    }

    private async Task<string?> GetCoverDataUrlAsync(GameEntry game, CancellationToken cancellationToken)
    {
        try
        {
            var path = await _covers.GetCoverPathAsync(game, cancellationToken);
            if (path is null || !File.Exists(path))
            {
                return null;
            }

            var info = new FileInfo(path);
            if (info.Length is <= 0 or > 3 * 1024 * 1024)
            {
                return null;
            }

            var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
            var mime = Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase)
                ? "image/png"
                : "image/jpeg";
            return $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug(exception, "Could not load cover for {Game}", game.Name);
            return null;
        }
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

    private static string StateLabel(DeploymentState state)
    {
        return state switch
        {
            DeploymentState.Installed => "已部署",
            DeploymentState.Outdated => "需更新",
            DeploymentState.Partial => "不完整",
            DeploymentState.Conflict => "文件冲突",
            DeploymentState.InstalledWithoutManifest => "已存在",
            _ => "未部署",
        };
    }

    private static string StateClass(DeploymentState state)
    {
        return state switch
        {
            DeploymentState.Installed => "ok",
            DeploymentState.Outdated or DeploymentState.Partial or DeploymentState.Conflict => "warn",
            _ => string.Empty,
        };
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
