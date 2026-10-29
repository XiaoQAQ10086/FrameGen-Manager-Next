using FrameGenManagerNext.Core.Features.Packs;
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

            return Task.FromResult<PackLocation?>(new PackLocation(
                loaded.Value,
                manifestPath,
                payloadDirectory));
        }

        return Task.FromResult<PackLocation?>(null);
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
