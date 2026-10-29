using FrameGenManagerNext.Core.Features.Dlss5;
using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Errors;
using Microsoft.Extensions.DependencyInjection;

namespace FrameGenManagerNext.Cli;

internal static class Dlss5Command
{
    public static async Task<int> RunAsync(
        IServiceProvider services,
        string action,
        string targetDirectory,
        string exePath,
        CancellationToken cancellationToken)
    {
        var service = services.GetRequiredService<IDlss5DeploymentService>();

        if (action == "inspect")
        {
            var result = await service.InspectAsync(targetDirectory, exePath, cancellationToken);
            if (result.IsFailure)
            {
                PrintError(result.Error!);
                return 1;
            }

            Console.WriteLine($"files={result.Value.FileState}");
            Console.WriteLine($"api={result.Value.Api}");
            Console.WriteLine($"reShadePresent={result.Value.ReShadePresent}");
            return 0;
        }

        if (action == "restore")
        {
            var result = await service.RestoreAsync(
                targetDirectory,
                CreateProgress(),
                cancellationToken);
            if (result.IsFailure)
            {
                PrintError(result.Error!);
                return 1;
            }

            var report = result.Value.FileRestore;
            Console.WriteLine($"restored={report.RestoredFiles} removed={report.RemovedFiles} conflicts={report.Conflicts}");
            Console.Error.WriteLine(result.Value.Message);
            foreach (var warning in report.Warnings)
            {
                Console.Error.WriteLine($"warning: {warning}");
            }

            return report.Conflicts == 0 ? 0 : 1;
        }

        var install = await service.InstallAsync(
            targetDirectory,
            exePath,
            CreateProgress(),
            cancellationToken);
        if (install.IsFailure)
        {
            PrintError(install.Error!);
            return 1;
        }

        Console.WriteLine($"files={install.Value.FileReport.State} written={install.Value.FileReport.FilesWritten} backedUp={install.Value.FileReport.FilesBackedUp}");
        Console.WriteLine($"api={install.Value.Api}");
        if (!string.IsNullOrWhiteSpace(install.Value.InstallerOutput))
        {
            Console.WriteLine("installerOutput:");
            Console.WriteLine(install.Value.InstallerOutput);
        }

        foreach (var warning in install.Value.FileReport.Warnings)
        {
            Console.Error.WriteLine($"warning: {warning}");
        }

        return 0;
    }

    private static void PrintError(AppError error)
    {
        Console.Error.WriteLine($"{error.Code}: {error.Diagnostic ?? "operation failed"}");
    }

    private static Progress<DeploymentProgress> CreateProgress()
    {
        return new Progress<DeploymentProgress>(update =>
        {
            Console.Error.WriteLine($"[{update.Phase}] {update.Subject} {update.Current}/{update.Total}");
        });
    }
}
