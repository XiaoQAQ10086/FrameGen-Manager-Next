using FrameGenManagerNext.Domain.Gpu;
using Shouldly;

namespace FrameGenManagerNext.Domain.Tests;

public sealed class GpuCompatibilityTests
{
    [Theory]
    [InlineData(GpuRoute.Sm86, "580.88", GpuFeatureGate.Supported)]
    [InlineData(GpuRoute.Sm75, "580.00", GpuFeatureGate.Supported)]
    [InlineData(GpuRoute.Sm86, "579.99", GpuFeatureGate.DriverTooOld)]
    [InlineData(GpuRoute.Sm86, null, GpuFeatureGate.Unknown)]
    [InlineData(GpuRoute.Gtx16, "580.88", GpuFeatureGate.GpuNotSupported)]
    [InlineData(GpuRoute.NoTensorCore, "580.88", GpuFeatureGate.GpuNotSupported)]
    [InlineData(GpuRoute.NotNeeded, "580.88", GpuFeatureGate.GpuNotNeeded)]
    [InlineData(GpuRoute.Unsupported, "580.88", GpuFeatureGate.GpuNotSupported)]
    [InlineData(GpuRoute.Unknown, "580.88", GpuFeatureGate.Unknown)]
    public void FrameGenerationGateMatchesRules(
        GpuRoute route,
        string? driver,
        GpuFeatureGate expected)
    {
        GpuCompatibility.EvaluateFrameGeneration(route, driver).ShouldBe(expected);
    }

    [Theory]
    [InlineData(GpuRoute.Sm86, "616.64", GpuFeatureGate.Supported)]
    [InlineData(GpuRoute.Sm86, "616.63", GpuFeatureGate.DriverTooOld)]
    [InlineData(GpuRoute.Sm75, "620.10", GpuFeatureGate.Supported)]
    [InlineData(GpuRoute.Sm86, null, GpuFeatureGate.Unknown)]
    [InlineData(GpuRoute.Gtx16, "620.10", GpuFeatureGate.GpuNotSupported)]
    public void SmoothMotionGateMatchesRules(
        GpuRoute route,
        string? driver,
        GpuFeatureGate expected)
    {
        GpuCompatibility.EvaluateSmoothMotion(route, driver).ShouldBe(expected);
    }

    [Fact]
    public void DriverVersionParsingHandlesCommonFormats()
    {
        GpuCompatibility.TryParseDriverVersion("580.88", out var first).ShouldBeTrue();
        first.ShouldBe(new Version(580, 88));

        GpuCompatibility.TryParseDriverVersion("580.88 extra", out var second).ShouldBeTrue();
        second.ShouldBe(new Version(580, 88));

        GpuCompatibility.TryParseDriverVersion("unknown", out _).ShouldBeFalse();
        GpuCompatibility.TryParseDriverVersion(null, out _).ShouldBeFalse();
    }
}
