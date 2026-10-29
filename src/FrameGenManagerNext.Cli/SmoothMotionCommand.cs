using System.Globalization;
using FrameGenManagerNext.Core.Features.SmoothMotion;
using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Errors;
using Microsoft.Extensions.DependencyInjection;

namespace FrameGenManagerNext.Cli;

internal static class SmoothMotionCommand
{
    public static async Task<int> RunAsync(
        IServiceProvider services,
        string action,
        string targetDirectory,
        string exePath,
        CancellationToken cancellationToken)
    {
        var service = services.GetRequiredService<ISmoothMotionDeploymentService>();
        var game = Path.GetFileNameWithoutExtension(exePath);

        switch (action)
        {
            case "inspect":
                {
                    var result = await service.InspectAsync(game, targetDirectory, exePath, cancellationToken);
                    if (result.IsFailure)
                    {
                        PrintError(result.Error!);
                        return 1;
                    }

                    var value = result.Value;
                    Console.WriteLine($"files={value.FileState}");
                    Console.WriteLine($"route={value.Route}");
                    Console.WriteLine($"pack={value.PackId}");
                    Console.WriteLine($"smEnabled={value.Setting.Enabled?.ToString() ?? "unknown"}");
                    Console.WriteLine($"profile={value.Setting.ProfileName}");
                    Console.WriteLine($"ours={value.Setting.IsOurs}");
                    if (!string.IsNullOrWhiteSpace(value.Setting.Detail))
                    {
                        Console.WriteLine($"detail={value.Setting.Detail}");
                    }

                    return 0;
                }

            case "enable":
                {
                    var result = await service.EnableAsync(
                        game,
                        targetDirectory,
                        exePath,
                        CreateProgress(),
                        cancellationToken);
                    if (result.IsFailure)
                    {
                        PrintError(result.Error!);
                        return 1;
                    }

                    var value = result.Value;
                    Console.WriteLine($"files={value.FileReport.State} written={value.FileReport.FilesWritten} backedUp={value.FileReport.FilesBackedUp}");
                    Console.WriteLine($"smOk={value.Setting.Ok} profile={value.Setting.ProfileName} readBack={value.Setting.ReadBack?.ToString(CultureInfo.InvariantCulture) ?? "none"}");
                    PrintWarnings(value.FileReport.Warnings);
                    return value.Setting.Ok ? 0 : 1;
                }

            case "disable":
                {
                    var result = await service.DisableAsync(game, targetDirectory, exePath, cancellationToken);
                    if (result.IsFailure)
                    {
                        PrintError(result.Error!);
                        return 1;
                    }

                    Console.WriteLine($"smOk={result.Value.Ok} profile={result.Value.ProfileName} readBack={result.Value.ReadBack?.ToString(CultureInfo.InvariantCulture) ?? "none"}");
                    return result.Value.Ok ? 0 : 1;
                }

            case "restore":
                {
                    var result = await service.RestoreAsync(
                        game,
                        targetDirectory,
                        exePath,
                        CreateProgress(),
                        cancellationToken);
                    if (result.IsFailure)
                    {
                        PrintError(result.Error!);
                        return 1;
                    }

                    var value = result.Value;
                    Console.WriteLine($"filesRestored={value.FileRestore.RestoredFiles} removed={value.FileRestore.RemovedFiles} conflicts={value.FileRestore.Conflicts}");
                    Console.WriteLine($"smOk={value.Setting.Ok} profile={value.Setting.ProfileName} readBack={value.Setting.ReadBack?.ToString(CultureInfo.InvariantCulture) ?? "none"}");
                    PrintWarnings(value.FileRestore.Warnings);
                    foreach (var leftover in value.FileRestore.Leftovers)
                    {
                        Console.Error.WriteLine($"leftover: {leftover}");
                    }

                    return value.Setting.Ok && value.FileRestore.Conflicts == 0 ? 0 : 1;
                }

            default:
                Console.Error.WriteLine($"Unknown Smooth Motion action: {action}");
                return 2;
        }
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

    private static Progress<DeploymentProgress> CreateProgress()
    {
        return new Progress<DeploymentProgress>(update =>
        {
            Console.Error.WriteLine($"[{update.Phase}] {update.Subject} {update.Current}/{update.Total}");
        });
    }
}
