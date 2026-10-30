using FrameGenManagerNext.Core.Features.App;
using FrameGenManagerNext.Core.Features.Deployment;
using FrameGenManagerNext.Core.Features.Diagnostics;
using FrameGenManagerNext.Core.Features.Dlss5;
using FrameGenManagerNext.Core.Features.Framegen;
using FrameGenManagerNext.Core.Features.Gpu;
using FrameGenManagerNext.Core.Features.Library;
using FrameGenManagerNext.Core.Features.Maintenance;
using FrameGenManagerNext.Core.Features.Packs;
using FrameGenManagerNext.Core.Features.Settings;
using FrameGenManagerNext.Core.Features.SmoothMotion;
using FrameGenManagerNext.Infrastructure.Deployment;
using FrameGenManagerNext.Infrastructure.Diagnostics;
using FrameGenManagerNext.Infrastructure.Dlss5;
using FrameGenManagerNext.Infrastructure.Gpu;
using FrameGenManagerNext.Infrastructure.Legacy;
using FrameGenManagerNext.Infrastructure.Library;
using FrameGenManagerNext.Infrastructure.Packs;
using FrameGenManagerNext.Infrastructure.Paths;
using FrameGenManagerNext.Infrastructure.Platform;
using FrameGenManagerNext.Infrastructure.Processes;
using FrameGenManagerNext.Infrastructure.Settings;
using FrameGenManagerNext.Infrastructure.SmoothMotion;
using Microsoft.Extensions.DependencyInjection;

namespace FrameGenManagerNext.Infrastructure.Composition;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFrameGenServices(
        this IServiceCollection services,
        AppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(paths);

        services.AddSingleton(paths);
        services.AddSingleton<IAppEnvironment, AppEnvironment>();
        services.AddSingleton<IEfficiencyModeService, EfficiencyModeService>();
        services.AddSingleton<ILegacyDeploymentReader, LegacyDeploymentReader>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IGameLibraryStore, GameLibraryStore>();
        services.AddSingleton<IGameScanner, SteamGameScanner>();
        services.AddSingleton<IGameScanner, EpicGameScanner>();
        services.AddSingleton<IGameScanner, WeGameGameScanner>();
        services.AddSingleton<IGameScanner, LooseGameScanner>();
        services.AddSingleton<IGameProbe, GameProbe>();
        services.AddSingleton<IGameCoverService, SteamCoverService>();
        services.AddSingleton<IGameLibraryService, GameLibraryService>();

        services.AddSingleton<IGpuProbe, NvidiaSmiGpuProbe>();
        services.AddSingleton<IGpuService, GpuService>();
        services.AddSingleton<IGpuSpoofService, GpuSpoofService>();
        services.AddSingleton<IDiagnosticsService, DiagnosticsService>();

        services.AddSingleton<IPackRepository, PackRepository>();
        services.AddSingleton<BackupStore>();
        services.AddSingleton<IDeploymentEngine, DeploymentEngine>();
        services.AddSingleton<IFrameGenDeploymentService, FrameGenDeploymentService>();

        services.AddSingleton<SmProfileStore>();
        services.AddSingleton<INvApiSmoothMotion, NvApiSmoothMotion>();
        services.AddSingleton<ISmoothMotionDeploymentService, SmoothMotionDeploymentService>();

        services.AddSingleton<IGraphicsApiProbe, GraphicsApiProbeService>();
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IReShadeOfflineInstaller, ReShadeOfflineInstaller>();
        services.AddSingleton<IReShadeConfigService, ReShadeConfigService>();
        services.AddSingleton<IDlss5DeploymentService, Dlss5DeploymentService>();
        services.AddSingleton<IAssetMaintenanceService, AssetMaintenanceService>();

        return services;
    }
}
