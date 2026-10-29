using System.Text.Json;
using FrameGenManagerNext.Core.Features.Library;
using FrameGenManagerNext.Domain.Library;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.Infrastructure.Library;

public sealed class EpicGameScanner : IGameScanner
{
    private readonly ILogger<EpicGameScanner> _logger;

    public EpicGameScanner(ILogger<EpicGameScanner> logger)
    {
        _logger = logger;
    }

    public LauncherKind Launcher => LauncherKind.Epic;

    public Task<IReadOnlyList<GameCandidate>> ScanAsync(CancellationToken cancellationToken)
    {
        var manifestDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Epic",
            "EpicGamesLauncher",
            "Data",
            "Manifests");

        if (!Directory.Exists(manifestDirectory))
        {
            return Task.FromResult<IReadOnlyList<GameCandidate>>([]);
        }

        return Task.Run(() => ScanFromDirectory(manifestDirectory), cancellationToken);
    }

    public IReadOnlyList<GameCandidate> ScanFromDirectory(string manifestDirectory)
    {
        var results = new List<GameCandidate>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        IEnumerable<string> manifests;
        try
        {
            manifests = Directory.EnumerateFiles(manifestDirectory, "*.item");
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not enumerate Epic manifests");
            return results;
        }

        foreach (var manifest in manifests)
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(manifest));
                var root = document.RootElement;

                if (root.TryGetProperty("bIsApplication", out var isApplication) &&
                    isApplication.ValueKind == JsonValueKind.False)
                {
                    continue;
                }

                if (!TryGetString(root, "DisplayName", out var name) ||
                    !TryGetString(root, "InstallLocation", out var installDirectory))
                {
                    continue;
                }

                var appName = TryGetString(root, "AppName", out var app) ? app : null;
                if (string.Equals(appName, "UnrealEngine", StringComparison.OrdinalIgnoreCase) ||
                    !Directory.Exists(installDirectory) ||
                    !seen.Add(Path.GetFullPath(installDirectory)))
                {
                    continue;
                }

                results.Add(new GameCandidate(name, installDirectory, LauncherKind.Epic, appName));
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Could not parse Epic manifest {Manifest}", manifest);
            }
        }

        return results
            .OrderBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static bool TryGetString(JsonElement element, string property, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(property, out var propertyValue) ||
            propertyValue.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = propertyValue.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }
}
