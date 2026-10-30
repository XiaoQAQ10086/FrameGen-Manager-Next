using FrameGenManagerNext.Core.Features.Gpu;
using FrameGenManagerNext.Domain.Gpu;
using Shouldly;

namespace FrameGenManagerNext.Core.Tests;

public sealed class GpuServiceTests
{
    [Fact]
    public async Task ProbeIsSharedAcrossCalls()
    {
        var probe = new CountingProbe();
        var service = new GpuService(probe);

        var first = await service.GetAsync(TestContext.Current.CancellationToken);
        var second = await service.GetAsync(TestContext.Current.CancellationToken);

        first.Name.ShouldBe("Test GPU");
        second.Name.ShouldBe("Test GPU");
        probe.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task CancellingOneCallerDoesNotCancelSharedProbe()
    {
        var probe = new BlockingProbe();
        var service = new GpuService(probe);
        using var cancellation = new CancellationTokenSource();

        var cancelled = service.GetAsync(cancellation.Token);
        cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(async () => await cancelled);

        probe.Release();
        var result = await service.GetAsync(TestContext.Current.CancellationToken);

        result.Name.ShouldBe("Test GPU");
        probe.Calls.ShouldBe(1);
    }

    private static GpuInfo CreateInfo()
    {
        return new GpuInfo(
            "Test GPU",
            "1.0.0",
            GpuRoute.Unknown,
            HardwareAcceleratedGpuScheduling: null,
            GpuFeatureGate.Unknown,
            GpuFeatureGate.Unknown);
    }

    private sealed class CountingProbe : IGpuProbe
    {
        public int Calls { get; private set; }

        public Task<GpuInfo> GetAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(CreateInfo());
        }
    }

    private sealed class BlockingProbe : IGpuProbe
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Calls { get; private set; }

        public async Task<GpuInfo> GetAsync(CancellationToken cancellationToken)
        {
            Calls++;
            await _release.Task;
            return CreateInfo();
        }

        public void Release() => _release.SetResult();
    }
}
