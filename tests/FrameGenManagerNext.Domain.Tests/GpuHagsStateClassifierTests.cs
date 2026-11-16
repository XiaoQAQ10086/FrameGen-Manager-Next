using FrameGenManagerNext.Domain.Gpu;
using Shouldly;

namespace FrameGenManagerNext.Domain.Tests;

public sealed class GpuHagsStateClassifierTests
{
    [Theory]
    [InlineData(true, true, null, GpuHagsState.Enabled)]
    [InlineData(true, true, 1, GpuHagsState.Enabled)]
    [InlineData(false, true, 2, GpuHagsState.PendingReboot)]
    [InlineData(false, true, 1, GpuHagsState.Disabled)]
    [InlineData(false, true, null, GpuHagsState.Disabled)]
    [InlineData(false, false, null, GpuHagsState.Unsupported)]
    [InlineData(null, true, 2, GpuHagsState.Enabled)]
    [InlineData(null, true, 1, GpuHagsState.Disabled)]
    [InlineData(null, true, null, GpuHagsState.Unknown)]
    public void ClassifiesLiveAndRegistryState(
        bool? liveEnabled,
        bool liveSupported,
        int? registryMode,
        GpuHagsState expected)
    {
        var state = GpuHagsStateClassifier.Classify(
            liveEnabled,
            liveSupported,
            registryMode);

        state.ShouldBe(expected);
    }
}
