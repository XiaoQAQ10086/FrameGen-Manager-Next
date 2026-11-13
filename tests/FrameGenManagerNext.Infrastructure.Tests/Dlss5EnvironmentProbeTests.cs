using FrameGenManagerNext.Core.Features.Dlss5;
using Shouldly;

namespace FrameGenManagerNext.Infrastructure.Tests;

public sealed class Dlss5EnvironmentProbeTests
{
    [Fact]
    public void TestHostIs64Bit()
    {
        Dlss5EnvironmentProbe.Is64Bit(Environment.ProcessPath!).ShouldBeTrue();
    }

    [Fact]
    public void SysWow64ExecutableIs32Bit()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.SystemX86),
            "notepad.exe");
        if (!File.Exists(path))
        {
            return;
        }

        Dlss5EnvironmentProbe.Is64Bit(path).ShouldBeFalse();
    }

    [Fact]
    public void CleanDirectoryHasNoReShadeModules()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"fgm-reshade-probe-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            Dlss5EnvironmentProbe.FindReShadeModules(directory).ShouldBeEmpty();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
