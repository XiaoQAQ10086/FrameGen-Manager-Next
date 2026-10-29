using FrameGenManagerNext.Core.Features.App;

namespace FrameGenManagerNext.Infrastructure.Paths;

public sealed class AppEnvironment : IAppEnvironment
{
    private readonly AppPaths _paths;

    public AppEnvironment(AppPaths paths)
    {
        _paths = paths;
    }

    public string DataRoot => _paths.RootDirectory;

    public string LogsDirectory => _paths.LogsDirectory;

    public bool IsPortable => _paths.IsPortable;
}
