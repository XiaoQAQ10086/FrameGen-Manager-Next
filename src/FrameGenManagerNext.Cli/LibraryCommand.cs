using System.Text.Json;
using FrameGenManagerNext.Core.Features.Library;
using FrameGenManagerNext.Domain.Library;
using Microsoft.Extensions.DependencyInjection;

namespace FrameGenManagerNext.Cli;

internal static class LibraryCommand
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static async Task<int> RunAsync(
        IServiceProvider services,
        string action,
        bool json,
        CancellationToken cancellationToken)
    {
        var libraryService = services.GetRequiredService<IGameLibraryService>();
        var library = action == "scan"
            ? await ScanAsync(libraryService, cancellationToken)
            : await libraryService.LoadAsync(cancellationToken);

        var games = library.EnumerateVisibleGames().ToList();

        if (json)
        {
            var payload = new
            {
                schemaVersion = library.SchemaVersion,
                lastScanUtc = library.LastScanUtc,
                scanned = library.Scanned.Count,
                manual = library.Manual.Count,
                ignored = library.IgnoredIds,
                games = games.Select(game => new
                {
                    game.Id,
                    game.Name,
                    launcher = game.Launcher.ToString(),
                    api = game.GraphicsApi.ToString(),
                    game.InstallDirectory,
                    game.TargetDirectory,
                    game.RenderExePath,
                    game.IsManual,
                }),
            };

            Console.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
            return 0;
        }

        if (games.Count == 0)
        {
            Console.WriteLine("No games found.");
            return 0;
        }

        Console.WriteLine($"{"Name",-40} {"Launcher",-10} {"API",-9} Target");
        Console.WriteLine(new string('-', 100));
        foreach (var game in games)
        {
            Console.WriteLine(
                $"{Truncate(game.Name, 40),-40} " +
                $"{game.Launcher,-10} " +
                $"{game.GraphicsApi,-9} " +
                $"{game.TargetDirectory}");
        }

        Console.WriteLine();
        Console.WriteLine($"{games.Count} game(s).");
        return 0;
    }

    private static async Task<GameLibrary> ScanAsync(
        IGameLibraryService libraryService,
        CancellationToken cancellationToken)
    {
        var progress = new Progress<GameScanProgress>(update =>
        {
            Console.Error.WriteLine($"[{update.Phase}] {update.Subject} {update.Current}/{update.Total}");
        });

        var result = await libraryService.ScanAsync(progress, cancellationToken);
        foreach (var warning in result.Warnings)
        {
            Console.Error.WriteLine($"warning: {warning}");
        }

        return result.Library;
    }

    private static string Truncate(string value, int length)
    {
        return value.Length <= length ? value : value[..(length - 1)] + "…";
    }
}
