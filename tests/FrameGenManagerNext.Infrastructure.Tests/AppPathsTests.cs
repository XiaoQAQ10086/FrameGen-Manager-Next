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
        }
        finally
        {
            Environment.SetEnvironmentVariable(AppPaths.DataDirectoryEnvironmentVariable, previous);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }
}
