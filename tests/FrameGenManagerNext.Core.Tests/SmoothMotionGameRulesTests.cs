using FrameGenManagerNext.Core.Features.SmoothMotion;
using Shouldly;

namespace FrameGenManagerNext.Core.Tests;

public sealed class SmoothMotionGameRulesTests
{
    [Fact]
    public void BattlEyeDirectoryBlocksDeployment()
    {
        using var folder = new TempFolder();
        Directory.CreateDirectory(Path.Combine(folder.Path, "BattlEye"));

        SmoothMotionGameRules.HasAntiCheat(folder.Path).ShouldBeTrue();
    }

    [Fact]
    public void EasyAntiCheatEosDirectoryBlocksDeployment()
    {
        using var folder = new TempFolder();
        Directory.CreateDirectory(Path.Combine(folder.Path, "EasyAntiCheat_EOS"));

        SmoothMotionGameRules.HasAntiCheat(folder.Path).ShouldBeTrue();
    }

    [Fact]
    public void BattlEyeClientDllBlocksDeployment()
    {
        using var folder = new TempFolder();
        File.WriteAllText(Path.Combine(folder.Path, "BEClient_x64.dll"), string.Empty);

        SmoothMotionGameRules.HasAntiCheat(folder.Path).ShouldBeTrue();
    }

    [Fact]
    public void EasyAntiCheatDriverBlocksDeployment()
    {
        using var folder = new TempFolder();
        File.WriteAllText(Path.Combine(folder.Path, "EasyAntiCheat.sys"), string.Empty);

        SmoothMotionGameRules.HasAntiCheat(folder.Path).ShouldBeTrue();
    }

    [Fact]
    public void BepInExIsNotMistakenForBattlEye()
    {
        using var folder = new TempFolder();
        File.WriteAllText(Path.Combine(folder.Path, "BepInEx.dll"), string.Empty);

        SmoothMotionGameRules.HasAntiCheat(folder.Path).ShouldBeFalse();
    }

    [Fact]
    public void CleanGameFolderIsAllowed()
    {
        using var folder = new TempFolder();
        File.WriteAllText(Path.Combine(folder.Path, "Game.exe"), string.Empty);

        SmoothMotionGameRules.HasAntiCheat(folder.Path).ShouldBeFalse();
    }

    [Fact]
    public void MissingFolderIsNotTreatedAsAntiCheat()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fgm-missing-{Guid.NewGuid():N}");

        SmoothMotionGameRules.HasAntiCheat(path).ShouldBeFalse();
    }

    private sealed class TempFolder : IDisposable
    {
        public TempFolder()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"fgm-sm-rules-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
