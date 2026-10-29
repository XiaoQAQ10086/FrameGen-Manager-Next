using System.Security.Cryptography;
using System.Text;

namespace FrameGenManagerNext.Infrastructure.Deployment;

internal static class DeploymentKey
{
    public static string ForPath(string path)
    {
        var normalized = Path
            .GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .ToLowerInvariant();

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }
}
