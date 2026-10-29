using FrameGenManagerNext.Infrastructure.Composition;
using FrameGenManagerNext.Infrastructure.Paths;
using Microsoft.Extensions.DependencyInjection;

namespace FrameGenManagerNext.Cli;

internal static class CliServices
{
    public static ServiceProvider Create()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFrameGenServices(AppPaths.CreateDefault());
        return services.BuildServiceProvider();
    }
}
