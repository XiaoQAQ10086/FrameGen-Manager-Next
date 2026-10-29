using FrameGenManagerNext.Domain.Gpu;
using Shouldly;

namespace FrameGenManagerNext.Domain.Tests;

public sealed class GpuRouteClassifierTests
{
    [Theory]
    [InlineData("NVIDIA GeForce RTX 3060", GpuRoute.Sm86)]
    [InlineData("NVIDIA GeForce RTX 3080 Ti Laptop GPU", GpuRoute.Sm86)]
    [InlineData("NVIDIA RTX A4000", GpuRoute.Sm86)]
    [InlineData("NVIDIA GeForce RTX 2080 SUPER", GpuRoute.Sm75)]
    [InlineData("NVIDIA GeForce RTX 2050", GpuRoute.Sm86)]
    [InlineData("NVIDIA Quadro RTX 5000", GpuRoute.Sm75)]
    [InlineData("NVIDIA TITAN RTX", GpuRoute.Sm75)]
    [InlineData("NVIDIA GeForce GTX 1660 SUPER", GpuRoute.Gtx16)]
    [InlineData("NVIDIA GeForce GTX 1080 Ti", GpuRoute.NoTensorCore)]
    [InlineData("NVIDIA GeForce MX150", GpuRoute.NoTensorCore)]
    [InlineData("NVIDIA Quadro P2000", GpuRoute.NoTensorCore)]
    [InlineData("NVIDIA GeForce RTX 4090", GpuRoute.NotNeeded)]
    [InlineData("NVIDIA GeForce RTX 4050 Laptop GPU", GpuRoute.NotNeeded)]
    [InlineData("NVIDIA GeForce RTX 5090", GpuRoute.NotNeeded)]
    [InlineData("NVIDIA RTX 4000 Ada Generation", GpuRoute.NotNeeded)]
    [InlineData("NVIDIA RTX 6000 Ada Generation", GpuRoute.NotNeeded)]
    [InlineData("NVIDIA RTX 4000", GpuRoute.Unknown)]
    [InlineData("NVIDIA GeForce RTX 9090", GpuRoute.Unknown)]
    [InlineData("AMD Radeon RX 7900 XTX", GpuRoute.Unsupported)]
    [InlineData("Intel Arc A770", GpuRoute.Unsupported)]
    [InlineData("Microsoft Basic Display Adapter", GpuRoute.Unknown)]
    [InlineData("", GpuRoute.Unknown)]
    [InlineData(null, GpuRoute.Unknown)]
    public void ClassifyMatchesKnownGpuNames(string? name, GpuRoute expected)
    {
        GpuRouteClassifier.Classify(name).ShouldBe(expected);
    }
}
