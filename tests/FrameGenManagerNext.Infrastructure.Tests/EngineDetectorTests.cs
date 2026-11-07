using FrameGenManagerNext.Domain.Library;
using FrameGenManagerNext.Infrastructure.Library;
using Shouldly;

namespace FrameGenManagerNext.Infrastructure.Tests;

public sealed class EngineDetectorTests
{
    [Fact]
    public void ReChunkPakMarksReEngine()
    {
        var directory = CreateDirectory();
        File.WriteAllText(Path.Combine(directory, "re_chunk_000.pak"), string.Empty);
        try
        {
            EngineDetector.Detect(directory, directory).ShouldBe(GameEngine.ReEngine);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void PatchPakAloneMarksReEngine()
    {
        var directory = CreateDirectory();
        File.WriteAllText(Path.Combine(directory, "re_chunk_000.pak.patch_001.pak"), string.Empty);
        try
        {
            EngineDetector.Detect(directory, directory).ShouldBe(GameEngine.ReEngine);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void MarkerInInstallRootIsFoundFromNestedTarget()
    {
        var root = CreateDirectory();
        var target = Path.Combine(root, "Binaries", "Win64");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(root, "re_chunk_000.pak"), string.Empty);
        try
        {
            EngineDetector.Detect(root, target).ShouldBe(GameEngine.ReEngine);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MonsterHunterWorldIsNotTreatedAsReEngine()
    {
        var directory = CreateDirectory();
        var exe = Path.Combine(directory, "mhw.exe");
        File.WriteAllText(exe, string.Empty);
        try
        {
            EngineDetector.Detect(directory, directory, exe).ShouldBe(GameEngine.MtFramework);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void CleanFolderIsUnknown()
    {
        var directory = CreateDirectory();
        try
        {
            EngineDetector.Detect(directory, directory).ShouldBe(GameEngine.Unknown);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"fgm-engine-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
