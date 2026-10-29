using System.Buffers;
using FrameGenManagerNext.Domain.Errors;
using FrameGenManagerNext.Domain.Results;

namespace FrameGenManagerNext.Domain.Safety;

/// <summary>
/// Validates relative paths from packs and configuration before they touch the file system.
/// </summary>
public static class SafePath
{
    public const int MaxRelativePathLength = 240;
    public const int MaxSegmentLength = 255;

    private static readonly SearchValues<char> ForbiddenCharacters =
        SearchValues.Create(['<', '>', ':', '"', '|', '?', '*']);

    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON",
        "PRN",
        "AUX",
        "NUL",
        "CONIN$",
        "CONOUT$",
        "COM1",
        "COM2",
        "COM3",
        "COM4",
        "COM5",
        "COM6",
        "COM7",
        "COM8",
        "COM9",
        "LPT1",
        "LPT2",
        "LPT3",
        "LPT4",
        "LPT5",
        "LPT6",
        "LPT7",
        "LPT8",
        "LPT9",
    };

    /// <summary>
    /// Normalizes a relative path to forward slashes and validates every segment.
    /// </summary>
    public static Result<string> NormalizeRelativePath(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return Fail(ErrorCode.InvalidPath, relativePath, "path is empty");
        }

        if (!string.Equals(relativePath, relativePath.Trim(), StringComparison.Ordinal))
        {
            return Fail(ErrorCode.InvalidPath, relativePath, "path has leading or trailing whitespace");
        }

        var value = relativePath.Replace('\\', '/');

        if (value.StartsWith('/'))
        {
            return Fail(ErrorCode.UnsafePath, relativePath, "absolute, rooted or UNC paths are not allowed");
        }

        if (value.Contains(':'))
        {
            return Fail(ErrorCode.UnsafePath, relativePath, "drive letters and alternate data streams are not allowed");
        }

        if (value.AsSpan().IndexOfAny(ForbiddenCharacters) >= 0)
        {
            return Fail(ErrorCode.InvalidPath, relativePath, "path contains characters that are invalid on Windows");
        }

        foreach (var character in value)
        {
            if (char.IsControl(character))
            {
                return Fail(ErrorCode.InvalidPath, relativePath, "path contains control characters");
            }
        }

        var segments = value.Split('/');
        foreach (var segment in segments)
        {
            if (segment.Length == 0 || segment is "." or "..")
            {
                return Fail(ErrorCode.UnsafePath, relativePath, "empty, '.' and '..' segments are not allowed");
            }

            if (segment.Length > MaxSegmentLength)
            {
                return Fail(ErrorCode.InvalidPath, relativePath, "path segment is too long");
            }

            if (segment[0] == ' ' || segment.EndsWith(' ') || segment.EndsWith('.'))
            {
                return Fail(ErrorCode.InvalidPath, relativePath, "segment has an unsafe leading space or trailing dot/space");
            }

            if (IsReservedDeviceName(segment))
            {
                return Fail(ErrorCode.InvalidPath, relativePath, "path uses a reserved Windows device name");
            }
        }

        var normalized = string.Join('/', segments);
        if (normalized.Length > MaxRelativePathLength)
        {
            return Fail(ErrorCode.InvalidPath, relativePath, "path is too long");
        }

        return Result<string>.Ok(normalized);
    }

    /// <summary>
    /// Combines a relative path with an absolute root and guarantees the result stays under that root.
    /// </summary>
    public static Result<string> CombineUnderRoot(string rootDirectory, string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            return Result<string>.Fail(new AppError(ErrorCode.InvalidPath, "root directory is empty"));
        }

        if (!Path.IsPathFullyQualified(rootDirectory))
        {
            return Result<string>.Fail(new AppError(ErrorCode.InvalidPath, "root directory must be an absolute path"));
        }

        string fullRoot;
        try
        {
            fullRoot = Path.GetFullPath(rootDirectory);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Result<string>.Fail(new AppError(ErrorCode.InvalidPath, exception.Message));
        }

        var normalized = NormalizeRelativePath(relativePath);
        if (normalized.IsFailure)
        {
            return Result<string>.Fail(normalized.Error!);
        }

        var segments = new List<string> { fullRoot };
        segments.AddRange(normalized.Value.Split('/'));

        string combined;
        try
        {
            combined = Path.GetFullPath(Path.Combine([.. segments]));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Result<string>.Fail(new AppError(ErrorCode.InvalidPath, exception.Message));
        }

        if (!IsUnderRoot(fullRoot, combined))
        {
            return Result<string>.Fail(new AppError(ErrorCode.UnsafePath, "path escapes its root directory"));
        }

        return Result<string>.Ok(combined);
    }

    public static bool IsUnderRoot(string rootDirectory, string fullPath)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootDirectory));
        var candidate = Path.GetFullPath(fullPath);

        if (string.Equals(root, candidate, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var prefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Rejects drive roots and the Windows / Program Files / System directories.
    /// </summary>
    public static bool IsProtectedSystemDirectory(string fullPath)
    {
        string candidate;
        try
        {
            candidate = Path.GetFullPath(fullPath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        var pathRoot = Path.GetPathRoot(candidate);
        if (!string.IsNullOrEmpty(pathRoot) &&
            string.Equals(
                Path.TrimEndingDirectorySeparator(candidate),
                Path.TrimEndingDirectorySeparator(pathRoot),
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var protectedDirectories = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        };

        foreach (var directory in protectedDirectories)
        {
            if (!string.IsNullOrWhiteSpace(directory) && IsUnderRoot(directory, candidate))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsReservedDeviceName(string segment)
    {
        var dotIndex = segment.IndexOf('.');
        var baseName = dotIndex >= 0 ? segment[..dotIndex] : segment;
        return ReservedDeviceNames.Contains(baseName);
    }

    private static Result<string> Fail(ErrorCode code, string? path, string reason)
    {
        return Result<string>.Fail(
            AppError.Create(code, ("path", path ?? string.Empty), ("reason", reason)));
    }
}
