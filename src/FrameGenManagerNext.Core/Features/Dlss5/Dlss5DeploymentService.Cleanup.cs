using System.Security.Cryptography;
using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Safety;

namespace FrameGenManagerNext.Core.Features.Dlss5;

public sealed partial class Dlss5DeploymentService
{
    private async Task<IReadOnlyList<PackFileEntry>> LoadPackFileEntriesAsync(
        CancellationToken cancellationToken)
    {
        var location = await _packs.FindAsync(PackId, cancellationToken);
        if (location is null)
        {
            return [];
        }

        return location.Pack.Files
            .Where(file => !string.IsNullOrWhiteSpace(file.Target) &&
                           !string.IsNullOrWhiteSpace(file.Sha256))
            .Select(file =>
            {
                var normalized = SafePath.NormalizeRelativePath(file.Target);
                return normalized.IsSuccess
                    ? new PackFileEntry(normalized.Value, file.Sha256!)
                    : null;
            })
            .Where(file => file is not null)
            .Cast<PackFileEntry>()
            .DistinctBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static async Task DeletePackFilesAsync(
        string target,
        IReadOnlyList<PackFileEntry> files,
        HashSet<string> protectedPaths,
        List<string> removed,
        List<string> notes,
        CancellationToken cancellationToken)
    {
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (protectedPaths.Contains(file.RelativePath))
            {
                continue;
            }

            var path = SafePath.CombineUnderRoot(target, file.RelativePath);
            if (path.IsFailure || !File.Exists(path.Value))
            {
                continue;
            }

            var hash = await TryHashAsync(path.Value, cancellationToken);
            if (hash is null)
            {
                notes.Add($"Could not verify {file.RelativePath}; it was left in place.");
                continue;
            }

            if (!string.Equals(hash, file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                notes.Add($"Modified file left in place: {file.RelativePath}");
                continue;
            }

            TryDeleteFile(path.Value, removed, notes);
        }
    }

    private static HashSet<string> GetProtectedPaths(DeploymentManifest? manifest)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (manifest is null)
        {
            return paths;
        }

        foreach (var record in manifest.Files.Where(record => record.ExistedBefore))
        {
            var normalized = SafePath.NormalizeRelativePath(record.RelativePath);
            if (normalized.IsSuccess)
            {
                paths.Add(normalized.Value);
            }
        }

        return paths;
    }

    private static async Task<string?> TryHashAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 81920,
                useAsync: true);
            var hash = await SHA256.HashDataAsync(stream, cancellationToken);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private sealed record PackFileEntry(string RelativePath, string Sha256);
}
