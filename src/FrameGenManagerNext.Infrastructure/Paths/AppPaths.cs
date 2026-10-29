using FrameGenManagerNext.Domain;

namespace FrameGenManagerNext.Infrastructure.Paths;

/// <summary>
/// Single source of truth for all writable application paths.
/// </summary>
public sealed class AppPaths
{
    public const string DataDirectoryEnvironmentVariable = "FGM_DATA_DIR";
    public const string PortableMarkerFileName = "portable.flag";
    public const string PortableDataDirectoryName = "data";

    public AppPaths(string rootDirectory)
        : this(rootDirectory, isPortable: false)
    {
    }

    private AppPaths(string rootDirectory, bool isPortable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        RootDirectory = Path.GetFullPath(rootDirectory);
        IsPortable = isPortable;
    }

    public string RootDirectory { get; }

    public bool IsPortable { get; }

    public string LogsDirectory => Path.Combine(RootDirectory, "logs");

    public string ConfigDirectory => Path.Combine(RootDirectory, "config");

    public string LibraryDirectory => Path.Combine(RootDirectory, "library");

    public string PacksDirectory => Path.Combine(RootDirectory, "packs");

    public string BackupsDirectory => Path.Combine(RootDirectory, "backups");

    public string CacheDirectory => Path.Combine(RootDirectory, "cache");

    public string StateDirectory => Path.Combine(RootDirectory, "state");

    public static AppPaths CreateDefault(string? applicationDirectory = null)
    {
        var overrideRoot = Environment.GetEnvironmentVariable(DataDirectoryEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overrideRoot))
        {
            return new AppPaths(overrideRoot);
        }

        var baseDirectory = string.IsNullOrWhiteSpace(applicationDirectory)
            ? AppContext.BaseDirectory
            : applicationDirectory;

        var markerPath = Path.Combine(baseDirectory, PortableMarkerFileName);
        if (File.Exists(markerPath))
        {
            return new AppPaths(Path.Combine(baseDirectory, PortableDataDirectoryName), isPortable: true);
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new AppPaths(Path.Combine(localAppData, AppInfo.DataDirectoryName));
    }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(ConfigDirectory);
        Directory.CreateDirectory(LibraryDirectory);
        Directory.CreateDirectory(PacksDirectory);
        Directory.CreateDirectory(BackupsDirectory);
        Directory.CreateDirectory(CacheDirectory);
        Directory.CreateDirectory(StateDirectory);
    }
}
