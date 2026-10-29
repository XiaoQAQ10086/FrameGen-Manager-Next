using FrameGenManagerNext.Core.Features.Gpu;
using FrameGenManagerNext.Domain.Gpu;
using FrameGenManagerNext.Domain.Results;
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

    public static async Task<int> RunSpoofAsync(
        IServiceProvider services,
        string action,
        string? value,
        CancellationToken cancellationToken)
    {
        var service = services.GetRequiredService<IGpuSpoofService>();

        if (action == "inspect")
        {
            var state = await service.InspectAsync(cancellationToken);
            Console.WriteLine($"elevated={state.IsElevated}");
            if (!string.IsNullOrWhiteSpace(state.Error))
            {
                Console.Error.WriteLine($"error: {state.Error}");
            }

            Console.WriteLine($"adapters={state.Adapters.Count}");
            foreach (var adapter in state.Adapters)
            {
                Console.WriteLine($"adapter: {adapter.CurrentName}");
                Console.WriteLine($"  driverName={adapter.DriverName}");
                Console.WriteLine($"  spoofed={adapter.Spoofed}");
                Console.WriteLine($"  hardwareId={adapter.HardwareId}");
                Console.WriteLine($"  enumKey={adapter.EnumKey}");
                Console.WriteLine($"  hasOriginalBackup={adapter.HasOriginalBackup}");
            }

            return state.Error is null ? 0 : 1;
        }

        if (action == "apply")
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                Console.Error.WriteLine(
                    "Usage: fgm-next gpu spoof apply <\"NVIDIA GeForce RTX 5090\"|\"NVIDIA GeForce RTX 5060\">");
                return 2;
            }

            var result = await service.ApplyAsync(value, cancellationToken);
            return PrintResult(result);
        }

        if (action == "restore")
        {
            var target = value?.ToLowerInvariant() switch
            {
                "driver" => GpuSpoofRestoreTarget.DriverName,
                "backup" => GpuSpoofRestoreTarget.OriginalValue,
                _ => (GpuSpoofRestoreTarget?)null,
            };

            if (target is null)
            {
                Console.Error.WriteLine("Usage: fgm-next gpu spoof restore <driver|backup>");
                return 2;
            }

            var result = await service.RestoreAsync(target.Value, cancellationToken);
            return PrintResult(result);
        }

        Console.Error.WriteLine("Usage: fgm-next gpu spoof <inspect|apply|restore> [value]");
        return 2;
    }

    private static int PrintResult(Result<GpuSpoofOperationReport> result)
    {
        if (result.IsFailure)
        {
            Console.Error.WriteLine($"{result.Error!.Code}: {result.Error.Diagnostic ?? "operation failed"}");
            return 1;
        }

        Console.WriteLine(result.Value.Message);
        foreach (var adapter in result.Value.State.Adapters)
        {
            Console.WriteLine($"adapter: {adapter.CurrentName} (spoofed={adapter.Spoofed})");
        }

        return 0;
    }
}
