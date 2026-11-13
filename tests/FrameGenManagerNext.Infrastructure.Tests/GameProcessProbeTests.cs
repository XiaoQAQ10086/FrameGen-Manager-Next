using FrameGenManagerNext.Infrastructure.Processes;
using Shouldly;

namespace FrameGenManagerNext.Infrastructure.Tests;

public sealed class GameProcessProbeTests
{
    [Fact]
    public void CurrentProcessIsDetected()
    {
        var probe = new GameProcessProbe();

        probe.IsRunning(Environment.ProcessPath).ShouldBeTrue();
    }

    [Fact]
    public void UnknownExecutableIsNotDetected()
    {
        var probe = new GameProcessProbe();
        var missing = Path.Combine(Path.GetTempPath(), $"fgm-not-running-{Guid.NewGuid():N}.exe");

        probe.IsRunning(missing).ShouldBeFalse();
    }

    [Fact]
    public void BlankPathIsNotDetected()
    {
        new GameProcessProbe().IsRunning(null).ShouldBeFalse();
    }
}
