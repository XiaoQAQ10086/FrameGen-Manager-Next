using System.Text.Json;
using FrameGenManagerNext.Core.Features.Maintenance;
using Microsoft.Extensions.DependencyInjection;

namespace FrameGenManagerNext.Cli;

internal static class AssetsCommand
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static async Task<int> RunAsync(
        IServiceProvider services,
        string action,
        bool json,
        CancellationToken cancellationToken)
    {
        var maintenance = services.GetRequiredService<IAssetMaintenanceService>();
        var report = await maintenance.DetectAsync(cancellationToken);

        if (action == "check")
        {
            if (json)
            {
                Console.WriteLine(JsonSerializer.Serialize(report, JsonOptions));
                return 0;
            }

            if (!report.HasUpdates)
            {
                Console.WriteLine("No asset updates detected.");
                return 0;
            }

            Console.WriteLine($"Asset updates: {report.TotalGames} game(s)");
            foreach (var group in report.Groups)
            {
                Console.WriteLine($"{LocalizedFamily(group.Family)} ({group.Games.Count})");
                foreach (var game in group.Games)
                {
                    Console.WriteLine(
                        $"  {game.Name}: {game.InstalledVersion ?? "unknown"} -> {game.CurrentVersion ?? "missing pack"}");
                }
            }

            return 0;
        }

        if (!report.HasUpdates)
        {
            Console.WriteLine("No asset updates detected.");
            return 0;
        }

        var progress = new Progress<AssetMaintenanceProgress>(update =>
        {
            if (update.Phase == "clearing")
            {
                Console.WriteLine($"[{update.Current}/{update.Total}] {LocalizedFamily(update.Family)}: {update.GameName}");
            }
        });

        var result = await maintenance.ClearAsync(report, progress, cancellationToken);
        Console.WriteLine(
            $"Cleared {result.ClearedGames} game(s); {result.FailedGames} failed; cancelled={result.Cancelled}.");

        foreach (var failure in result.Failures)
        {
            Console.WriteLine(
                $"FAILED {failure.GameName} [{failure.Code}] {failure.TargetDirectory}: {failure.Detail}");
            foreach (var remaining in failure.Remaining)
            {
                Console.WriteLine($"  {remaining}");
            }
        }

        return result.FailedGames == 0 && !result.Cancelled ? 0 : 1;
    }

    private static string LocalizedFamily(AssetFamily family)
    {
        return family switch
        {
            AssetFamily.FrameGen => "Frame generation",
            AssetFamily.SmoothMotion => "Smooth Motion",
            AssetFamily.Dlss5 => "DLSS5",
            _ => family.ToString(),
        };
    }
}
