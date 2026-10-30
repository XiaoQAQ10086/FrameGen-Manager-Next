using FrameGenManagerNext.Core.Features.Framegen;
using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Errors;
using Microsoft.Extensions.DependencyInjection;

namespace FrameGenManagerNext.Cli;

internal static class DeployCommand
{
    public static async Task<int> RunAsync(
        IServiceProvider services,
        string action,
        string targetDirectory,
        FrameGenOptions options,
        CancellationToken cancellationToken)
    {
        var service = services.GetRequiredService<IFrameGenDeploymentService>();

        if (action.Equals("restore", StringComparison.OrdinalIgnoreCase))
        {
            var restore = await service.RestoreAsync(
                targetDirectory,
                CreateProgress(),
                cancellationToken);

            if (restore.IsFailure)
            {
                PrintError(restore.Error!);
                return 1;
            }

            var report = restore.Value;
            Console.WriteLine($"restored={report.RestoredFiles} removed={report.RemovedFiles} conflicts={report.Conflicts}");
            PrintWarnings(report.Warnings);
            PrintList("leftovers", report.Leftovers);
            return 0;
        }

        if (action.Equals("clean", StringComparison.OrdinalIgnoreCase))
        {
            var clean = await service.CleanAsync(
                targetDirectory,
                CreateProgress(),
                cancellationToken);

            if (clean.IsFailure)
            {
                PrintError(clean.Error!);
                return 1;
            }

            var cleanReport = clean.Value;
            Console.WriteLine($"removed={cleanReport.RemovedFiles} conflicts={cleanReport.Conflicts}");
            PrintWarnings(cleanReport.Warnings);
            PrintList("leftovers", cleanReport.Leftovers);
            return 0;
        }

        if (action.Equals("inspect", StringComparison.OrdinalIgnoreCase))
        {
            var inspection = await service.InspectAsync(targetDirectory, options, cancellationToken);
            if (inspection.IsFailure)
            {
                PrintError(inspection.Error!);
                return 1;
            }

            Console.WriteLine($"state={inspection.Value.State}");
            Console.WriteLine($"packVersion={inspection.Value.PackVersion ?? "unknown"}");
            Console.WriteLine($"hasManifest={inspection.Value.HasManifest}");
            PrintList("details", inspection.Value.Details);
            return 0;
        }

        var deployment = await service.DeployAsync(
            targetDirectory,
            options,
            CreateProgress(),
            cancellationToken);

        if (deployment.IsFailure)
        {
            PrintError(deployment.Error!);
            return 1;
        }

        var result = deployment.Value;
        Console.WriteLine($"state={result.State} written={result.FilesWritten} backedUp={result.FilesBackedUp}");
        PrintWarnings(result.Warnings);
        return 0;
    }

    private static Progress<DeploymentProgress> CreateProgress()
    {
        return new Progress<DeploymentProgress>(update =>
        {
            Console.Error.WriteLine($"[{update.Phase}] {update.Subject} {update.Current}/{update.Total}");
        });
    }

    private static void PrintError(AppError error)
    {
        Console.Error.WriteLine($"{error.Code}: {error.Diagnostic ?? "operation failed"}");
    }

    private static void PrintWarnings(IReadOnlyList<string> warnings)
    {
        foreach (var warning in warnings)
        {
            Console.Error.WriteLine($"warning: {warning}");
        }
    }

    private static void PrintList(string label, IReadOnlyList<string> values)
    {
        if (values.Count == 0)
        {
            return;
        }

        Console.WriteLine($"{label}:");
        foreach (var value in values)
        {
            Console.WriteLine($"  {value}");
        }
    }
}
