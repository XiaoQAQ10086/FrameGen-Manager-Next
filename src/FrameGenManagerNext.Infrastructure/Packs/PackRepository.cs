using FrameGenManagerNext.Core.Features.Packs;
using FrameGenManagerNext.Domain;
using FrameGenManagerNext.Domain.Packs;
using FrameGenManagerNext.Domain.Safety;
using FrameGenManagerNext.Infrastructure.Paths;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.Infrastructure.Packs;

public sealed class PackRepository : IPackRepository
{
    public const string PackRootEnvironmentVariable = "FGM_PACKS_DIR";

    private readonly AppPaths _paths;
    private readonly ILogger<PackRepository> _logger;

    public PackRepository(AppPaths paths, ILogger<PackRepository> logger)
    {
        _paths = paths;
        _logger = logger;
    }

    public Task<PackLocation?> FindAsync(string packId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        PackLocation? fallback = null;
        PackLocation? best = null;

        foreach (var root in EnumerateRoots())
        {
            var packDirectory = Path.Combine(root, packId);
            var manifestPath = Path.Combine(packDirectory, "pack.json");
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            var loaded = PackJsonSerializer.LoadFromFile(manifestPath);
            if (loaded.IsFailure)
            {
                _logger.LogWarning(
                    "Could not load pack {Pack} from {Manifest}: {Error}",
                    packId,
                    manifestPath,
                    loaded.Error?.Diagnostic);
                continue;
            }

            var payloadDirectory = PayloadDirectoryFor(packId, packDirectory, loaded.Value);
            if (payloadDirectory is null)
            {
                continue;
            }

            var location = new PackLocation(loaded.Value, manifestPath, payloadDirectory);
            fallback ??= location;

            // Root order used to decide which pack won, so a stale downloaded pack
            // could shadow a newer bundled one. Pick the highest compatible version.
            if (!AssetPackValidator.ValidateSchema(loaded.Value, AppInfo.Version).IsValid)
            {
                continue;
            }

            if (best is null ||
                CompareVersions(loaded.Value.Version, best.Pack.Version) > 0)
            {
                best = location;
            }
        }

        return Task.FromResult(best ?? fallback);
    }

    private static int CompareVersions(string left, string right)
    {
        return ParseVersion(left).CompareTo(ParseVersion(right));
    }

    private static Version ParseVersion(string value)
    {
        var core = value.Split('-', '+')[0];
        return Version.TryParse(core, out var version) ? version : new Version(0, 0, 0);
    }

    private IEnumerable<string> EnumerateRoots()
    {
        var roots = new List<string>();

        var configured = Environment.GetEnvironmentVariable(PackRootEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            AddRoot(roots, configured);
        }

        AddRoot(roots, Path.Combine(AppContext.BaseDirectory, "packs"));
        AddRoot(roots, _paths.PacksDirectory);

        var repositoryRoot = FindRepositoryRoot();
        if (repositoryRoot is not null)
        {
            AddRoot(roots, Path.Combine(repositoryRoot, "packs"));
        }

        return roots.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private string? PayloadDirectoryFor(string packId, string packDirectory, AssetPack pack)
    {
        if (PayloadLooksPresent(pack, packDirectory))
        {
            return packDirectory;
        }

        var repositoryRoot = FindRepositoryRoot();
        if (repositoryRoot is not null)
        {
            var assetPayload = Path.Combine(repositoryRoot, "assets", "packs", packId);
            if (Directory.Exists(assetPayload) && PayloadLooksPresent(pack, assetPayload))
            {
                return assetPayload;
            }
        }

        _logger.LogDebug("Pack {Pack} was found but its payload is incomplete", packId);
        return null;
    }

    private static bool PayloadLooksPresent(AssetPack pack, string directory)
    {
        var sample = pack.Files.FirstOrDefault();
        if (sample is null)
        {
            return false;
        }

        var file = SafePath.CombineUnderRoot(directory, sample.Source);
        if (file.IsFailure || !File.Exists(file.Value))
        {
            return false;
        }

        if (pack.Ini is not null)
        {
            var template = SafePath.CombineUnderRoot(directory, pack.Ini.Template);
            return template.IsSuccess && File.Exists(template.Value);
        }

        return true;
    }

    private static void AddRoot(List<string> roots, string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return;
        }

        try
        {
            var fullPath = Path.GetFullPath(candidate);
            if (Directory.Exists(fullPath))
            {
                roots.Add(fullPath);
            }
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // Ignore invalid candidate roots.
        }
    }

    private static string? FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        for (var depth = 0; depth < 8 && directory is not null; depth++)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FrameGenManagerNext.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
