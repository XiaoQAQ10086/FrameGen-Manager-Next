namespace FrameGenManagerNext.Domain.Library;

/// <summary>
/// Picks a render executable using the old manager's weighted signals.
/// Returns null when no candidate has a positive signal instead of guessing.
/// </summary>
public static class RenderExeLocator
{
    private static readonly string[] ExcludedNameParts =
    [
        "unins",
        "vcredist",
        "dxsetup",
        "dxwebsetup",
        "crashreport",
        "unitycrashhandler",
        "unrealcefsubprocess",
        "epicwebhelper",
        "eossdk",
        "easyanticheat",
        "eaclauncher",
        "beservice",
        "prereq",
        "installer",
        "subprocess",
        "helper",
        "launcher",
        "vconsole",
    ];

    public static GameExeCandidate? FindBest(
        IReadOnlyList<GameExeCandidate> candidates,
        string? installDirectoryName = null)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        GameExeCandidate? best = null;
        var bestScore = -1;
        var bestSize = -1L;

        foreach (var candidate in candidates)
        {
            if (candidate is null || string.IsNullOrWhiteSpace(candidate.FullPath))
            {
                continue;
            }

            var fileName = candidate.FileName.ToLowerInvariant();
            if (IsExcluded(fileName))
            {
                continue;
            }

            var score = Score(candidate, installDirectoryName);
            if (score > bestScore || (score == bestScore && candidate.Size > bestSize))
            {
                best = candidate;
                bestScore = score;
                bestSize = candidate.Size;
            }
        }

        return bestScore > 0 ? best : null;
    }

    public static int Score(GameExeCandidate candidate, string? installDirectoryName = null)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var fileName = candidate.FileName.ToLowerInvariant();
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var relative = candidate.RelativeDirectory.Replace('\\', '/');
        var parts = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);

        var score = 0;

        if (fileName.EndsWith("-win64-shipping.exe", StringComparison.Ordinal))
        {
            score += 50;
        }
        else if (fileName.EndsWith("-win32-shipping.exe", StringComparison.Ordinal))
        {
            score += 20;
        }

        if (parts.Length > 0 && string.Equals(parts[^1], "Win64", StringComparison.OrdinalIgnoreCase))
        {
            score += 10;

            if (parts.Length > 1 && string.Equals(parts[^2], "Binaries", StringComparison.OrdinalIgnoreCase))
            {
                if (parts.Length > 2 && string.Equals(parts[^3], stem, StringComparison.OrdinalIgnoreCase))
                {
                    score += 30;
                }
            }
            else if (parts.Length > 1 && string.Equals(parts[^2], stem, StringComparison.OrdinalIgnoreCase))
            {
                score += 30;
            }
        }

        if (!string.IsNullOrWhiteSpace(installDirectoryName) &&
            string.Equals(stem, installDirectoryName.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            score += 20;
        }

        if (candidate.Size > 10 * 1024 * 1024)
        {
            score += 15;
        }
        else if (candidate.Size > 2 * 1024 * 1024)
        {
            score += 3;
        }

        return score;
    }

    private static bool IsExcluded(string fileName)
    {
        return ExcludedNameParts.Any(part => fileName.Contains(part, StringComparison.Ordinal));
    }
}
