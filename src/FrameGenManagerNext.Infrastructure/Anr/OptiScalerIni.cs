using System.Text;
using FrameGenManagerNext.Domain.Errors;
using FrameGenManagerNext.Domain.Results;
using FrameGenManagerNext.Domain.Safety;

namespace FrameGenManagerNext.Infrastructure.Anr;

/// <summary>
/// Mirrors the official addon installer: put the custom provider in OptiScaler's
/// custom folder and point [FrameGen] at it while preserving the file's own
/// encoding, BOM and line endings.
/// </summary>
internal static class OptiScalerIni
{
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    public static Result<string> ResolveMainDirectory(string targetDirectory, byte[] iniBytes)
    {
        var text = Decode(iniBytes, out _, out _);
        var value = ReadFirstValue(text, "OptiDllPath");
        string main;
        if (string.IsNullOrWhiteSpace(value) ||
            value.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            var optiScaler = Path.Combine(targetDirectory, "OptiScaler");
            main = Directory.Exists(optiScaler) ? optiScaler : targetDirectory;
        }
        else
        {
            try
            {
                main = Path.IsPathRooted(value)
                    ? Path.GetFullPath(value)
                    : Path.GetFullPath(Path.Combine(targetDirectory, value));
            }
            catch (Exception exception) when (
                exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return Result<string>.Fail(new AppError(ErrorCode.InvalidPath, exception.Message));
            }
        }

        if (!SafePath.IsUnderRoot(targetDirectory, main))
        {
            return Result<string>.Fail(new AppError(
                ErrorCode.UnsafePath,
                "OptiDllPath points outside the game directory"));
        }

        if (!Directory.Exists(main))
        {
            return Result<string>.Fail(new AppError(
                ErrorCode.FileNotFound,
                "the OptiScaler directory from OptiDllPath does not exist"));
        }

        return Result<string>.Ok(main);
    }

    public static Result<byte[]> ApplyFrameGeneration(byte[] iniBytes)
    {
        var text = Decode(iniBytes, out var bom, out var newline);
        var lines = text.Split(["\r\n", "\n"], StringSplitOptions.None).ToList();
        lines = SetValue(lines, "FrameGen", "FGInput", "nvngxfg");
        lines = SetValue(lines, "FrameGen", "FGNvngxReplacement", "Apex Neural Rendering FG");
        var body = Encoding.UTF8.GetBytes(string.Join(newline, lines));
        if (!bom)
        {
            return Result<byte[]>.Ok(body);
        }

        var withBom = new byte[Utf8Bom.Length + body.Length];
        Utf8Bom.CopyTo(withBom, 0);
        body.CopyTo(withBom, Utf8Bom.Length);
        return Result<byte[]>.Ok(withBom);
    }

    private static string Decode(byte[] bytes, out bool bom, out string newline)
    {
        bom = bytes.Length >= 3 &&
              bytes[0] == Utf8Bom[0] &&
              bytes[1] == Utf8Bom[1] &&
              bytes[2] == Utf8Bom[2];
        var offset = bom ? Utf8Bom.Length : 0;
        var text = Encoding.UTF8.GetString(bytes, offset, bytes.Length - offset);
        newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        return text;
    }

    private static string? ReadFirstValue(string text, string key)
    {
        foreach (var line in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            var separator = trimmed.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            if (trimmed[..separator].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return trimmed[(separator + 1)..].Trim();
            }
        }

        return null;
    }

    private static List<string> SetValue(
        List<string> lines,
        string section,
        string key,
        string value)
    {
        var output = new List<string>(lines.Count + 2);
        var inSection = false;
        var done = false;
        var seen = false;
        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith('['))
            {
                if (inSection && !done)
                {
                    output.Add($"{key}={value}");
                    done = true;
                }

                var end = trimmed.IndexOf(']');
                var name = end > 1 ? trimmed[1..end] : string.Empty;
                inSection = name.Equals(section, StringComparison.OrdinalIgnoreCase);
                seen |= inSection;
            }
            else if (inSection && !done && IsKey(line, key))
            {
                output.Add($"{key}={value}");
                done = true;
                continue;
            }

            output.Add(line);
        }

        if (!done)
        {
            if (!seen)
            {
                output.Add($"[{section}]");
            }

            output.Add($"{key}={value}");
        }

        return output;
    }

    private static bool IsKey(string line, string key)
    {
        var trimmed = line.TrimStart();
        var separator = trimmed.IndexOf('=');
        return separator > 0 &&
               trimmed[..separator].Trim().Equals(key, StringComparison.OrdinalIgnoreCase);
    }
}
