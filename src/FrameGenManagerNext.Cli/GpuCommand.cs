using FrameGenManagerNext.Core.Features.Gpu;
using Microsoft.Extensions.DependencyInjection;

namespace FrameGenManagerNext.Cli;

internal static class GpuCommand
{
    public static async Task<int> RunAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var gpuService = services.GetRequiredService<IGpuService>();
        var gpu = await gpuService.GetAsync(cancellationToken);

        Console.WriteLine($"GPU: {gpu.Name}");
        Console.WriteLine($"Driver: {gpu.DriverVersion ?? "unknown"}");
        Console.WriteLine($"Route: {gpu.Route}");
        Console.WriteLine($"FrameGeneration: {gpu.FrameGeneration}");
        Console.WriteLine($"SmoothMotion: {gpu.SmoothMotion}");
        Console.WriteLine(
            "HAGS: " +
            (gpu.HardwareAcceleratedGpuScheduling is bool hags
                ? hags ? "enabled" : "disabled"
                : "unknown"));

        if (!string.IsNullOrWhiteSpace(gpu.Diagnostic))
        {
            Console.WriteLine($"Diagnostic: {gpu.Diagnostic}");
        }

        return 0;
    }
}
