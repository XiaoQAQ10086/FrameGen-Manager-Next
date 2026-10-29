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

    public string PacksDirectory => _paths.PacksDirectory;

    public string BackupsDirectory => _paths.BackupsDirectory;

    public string ConfigDirectory => _paths.ConfigDirectory;

    public bool IsPortable => _paths.IsPortable;
}
