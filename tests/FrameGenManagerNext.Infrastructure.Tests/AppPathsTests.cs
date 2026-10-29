using FrameGenManagerNext.Domain;
using FrameGenManagerNext.Infrastructure.Paths;
using Shouldly;

namespace FrameGenManagerNext.Infrastructure.Tests;

public sealed class AppPathsTests : IDisposable
{
    private readonly string _rootDirectory = Path.Combine(
        Path.GetTempPath(),
        "FrameGenManagerNextTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void EnsureCreatedCreatesExpectedDirectories()
    {
        var paths = new AppPaths(_rootDirectory);

        paths.EnsureCreated();

        Directory.Exists(paths.RootDirectory).ShouldBeTrue();
        Directory.Exists(paths.LogsDirectory).ShouldBeTrue();
        Directory.Exists(paths.ConfigDirectory).ShouldBeTrue();
        Directory.Exists(paths.LibraryDirectory).ShouldBeTrue();
        Directory.Exists(paths.PacksDirectory).ShouldBeTrue();
        Directory.Exists(paths.BackupsDirectory).ShouldBeTrue();
        Directory.Exists(paths.CacheDirectory).ShouldBeTrue();
        Directory.Exists(paths.StateDirectory).ShouldBeTrue();
    }

    [Fact]
    public void DefaultPathHonorsOverrideEnvironmentVariable()
    {
        var previous = Environment.GetEnvironmentVariable(AppPaths.DataDirectoryEnvironmentVariable);

        try
        {
            Environment.SetEnvironmentVariable(AppPaths.DataDirectoryEnvironmentVariable, _rootDirectory);

            var paths = AppPaths.CreateDefault();

            paths.RootDirectory.ShouldBe(Path.GetFullPath(_rootDirectory));
            paths.IsPortable.ShouldBeFalse();
        }
        finally
        {
            Environment.SetEnvironmentVariable(AppPaths.DataDirectoryEnvironmentVariable, previous);
        }
    }

    [Fact]
    public void PortableMarkerUsesDataDirectoryNextToApplication()
    {
        var previous = ClearOverride();
        try
        {
            Directory.CreateDirectory(_rootDirectory);
            File.WriteAllText(Path.Combine(_rootDirectory, AppPaths.PortableMarkerFileName), "portable");

            var paths = AppPaths.CreateDefault(_rootDirectory);
            paths.EnsureCreated();

            paths.IsPortable.ShouldBeTrue();
            paths.RootDirectory.ShouldBe(Path.GetFullPath(Path.Combine(_rootDirectory, AppPaths.PortableDataDirectoryName)));
            Directory.Exists(Path.Combine(_rootDirectory, AppPaths.PortableDataDirectoryName, "logs")).ShouldBeTrue();
        }
        finally
        {
            RestoreOverride(previous);
        }
    }

    [Fact]
    public void DefaultPathUsesLocalAppDataWithoutPortableMarker()
    {
        var previous = ClearOverride();
        try
        {
            var emptyApplicationDirectory = Path.Combine(_rootDirectory, "app");
            Directory.CreateDirectory(emptyApplicationDirectory);

            var paths = AppPaths.CreateDefault(emptyApplicationDirectory);

            paths.IsPortable.ShouldBeFalse();
            var expectedRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                AppInfo.DataDirectoryName);
            paths.RootDirectory.ShouldBe(Path.GetFullPath(expectedRoot));
        }
        finally
        {
            RestoreOverride(previous);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    private static string? ClearOverride()
    {
        var previous = Environment.GetEnvironmentVariable(AppPaths.DataDirectoryEnvironmentVariable);
        Environment.SetEnvironmentVariable(AppPaths.DataDirectoryEnvironmentVariable, null);
        return previous;
    }

    private static void RestoreOverride(string? previous)
    {
        Environment.SetEnvironmentVariable(AppPaths.DataDirectoryEnvironmentVariable, previous);
    }
}
