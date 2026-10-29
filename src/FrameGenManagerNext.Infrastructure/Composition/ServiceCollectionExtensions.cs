using FrameGenManagerNext.Core.Features.App;
using FrameGenManagerNext.Core.Features.Deployment;
using FrameGenManagerNext.Core.Features.Diagnostics;
using FrameGenManagerNext.Core.Features.Framegen;
using FrameGenManagerNext.Core.Features.Gpu;
using FrameGenManagerNext.Core.Features.Library;
using FrameGenManagerNext.Core.Features.Packs;
using FrameGenManagerNext.Core.Features.SmoothMotion;
using FrameGenManagerNext.Infrastructure.Deployment;
using FrameGenManagerNext.Infrastructure.Diagnostics;
using FrameGenManagerNext.Infrastructure.Gpu;
using FrameGenManagerNext.Infrastructure.Library;
using FrameGenManagerNext.Infrastructure.Packs;
using FrameGenManagerNext.Infrastructure.Paths;
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

        services.AddSingleton<IGameLibraryStore, GameLibraryStore>();
        services.AddSingleton<IGameScanner, SteamGameScanner>();
        services.AddSingleton<IGameScanner, EpicGameScanner>();
        services.AddSingleton<IGameScanner, WeGameGameScanner>();
        services.AddSingleton<IGameScanner, LooseGameScanner>();
        services.AddSingleton<IGameProbe, GameProbe>();
        services.AddSingleton<IGameLibraryService, GameLibraryService>();

        services.AddSingleton<IGpuProbe, NvidiaSmiGpuProbe>();
        services.AddSingleton<IGpuService, GpuService>();
        services.AddSingleton<IDiagnosticsService, DiagnosticsService>();

        services.AddSingleton<IPackRepository, PackRepository>();
        services.AddSingleton<BackupStore>();
        services.AddSingleton<IDeploymentEngine, DeploymentEngine>();
        services.AddSingleton<IFrameGenDeploymentService, FrameGenDeploymentService>();

        services.AddSingleton<SmProfileStore>();
        services.AddSingleton<INvApiSmoothMotion, NvApiSmoothMotion>();
        services.AddSingleton<ISmoothMotionDeploymentService, SmoothMotionDeploymentService>();

        return services;
    }
}
