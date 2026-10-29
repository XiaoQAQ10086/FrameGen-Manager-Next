using System.Security.Cryptography;
using FrameGenManagerNext.Domain.Packs;
using FrameGenManagerNext.Domain.Safety;

namespace FrameGenManagerNext.Infrastructure.Packs;

/// <summary>
/// Verifies that pack payload files exist and match their declared size and SHA-256.
/// </summary>
public static class PackPayloadValidator
{
    public static async Task<PackValidationResult> ValidateAsync(
        AssetPack pack,
        string payloadDirectory,
        bool verifyHashes = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadDirectory);

        var root = Path.GetFullPath(payloadDirectory);
        var errors = new List<PackValidationError>();

        if (pack.Files is null || pack.Files.Count == 0)
        {
            return new PackValidationResult(
                [new PackValidationError("PACK-101", "files", "Pack has no files to validate.")]);
        }

        for (var index = 0; index < pack.Files.Count; index++)
        {
            var file = pack.Files[index];
            var path = $"files[{index}]";

            if (file is null)
            {
                errors.Add(new PackValidationError("PACK-022", path, "File entry must not be null."));
                continue;
            }

            var combined = SafePath.CombineUnderRoot(root, file.Source);
            if (combined.IsFailure)
            {
                errors.Add(new PackValidationError(
                    "PACK-101",
                    $"{path}.source",
                    combined.Error?.Diagnostic ?? "Invalid source path."));
                continue;
            }

            var fullPath = combined.Value;
            if (!File.Exists(fullPath))
            {
                errors.Add(new PackValidationError("PACK-101", $"{path}.source", $"File not found: {fullPath}"));
                continue;
            }

            try
            {
                var length = new FileInfo(fullPath).Length;
                if (file.Size is not null && file.Size.Value != length)
                {
                    errors.Add(new PackValidationError(
                        "PACK-102",
                        $"{path}.size",
                        $"Expected {file.Size.Value} bytes but found {length}."));
                }

                if (!verifyHashes)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(file.Sha256))
                {
                    errors.Add(new PackValidationError("PACK-103", $"{path}.sha256", "SHA-256 is missing."));
                    continue;
                }

                await using var stream = File.OpenRead(fullPath);
                var hash = await SHA256.HashDataAsync(stream, cancellationToken);
                var actual = Convert.ToHexString(hash);

                if (!string.Equals(actual, file.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(new PackValidationError(
                        "PACK-103",
                        $"{path}.sha256",
                        $"Expected {file.Sha256} but found {actual}."));
                }
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or CryptographicException)
            {
                errors.Add(new PackValidationError("PACK-105", $"{path}.source", exception.Message));
            }
        }

        if (pack.Ini is not null)
        {
            await ValidateIniTemplateAsync(
                pack.Ini,
                root,
                verifyHashes,
                errors,
                cancellationToken);
        }

        await ValidateInstallerStepsAsync(
            pack,
            root,
            verifyHashes,
            errors,
            cancellationToken);

        return new PackValidationResult(errors);
    }

    private static async Task ValidateIniTemplateAsync(
        AssetPackIni ini,
        string root,
        bool verifyHashes,
        List<PackValidationError> errors,
        CancellationToken cancellationToken)
    {
        var template = SafePath.CombineUnderRoot(root, ini.Template);
        if (template.IsFailure || !File.Exists(template.Value))
        {
            errors.Add(new PackValidationError(
                "PACK-104",
                "ini.template",
                template.IsFailure
                    ? template.Error?.Diagnostic ?? "Invalid INI template path."
                    : $"INI template not found: {template.Value}"));
            return;
        }

        try
        {
            var length = new FileInfo(template.Value).Length;
            if (ini.TemplateSize is not null && ini.TemplateSize.Value != length)
            {
                errors.Add(new PackValidationError(
                    "PACK-106",
                    "ini.templateSize",
                    $"Expected {ini.TemplateSize.Value} bytes but found {length}."));
            }

            if (!verifyHashes)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(ini.TemplateSha256))
            {
                errors.Add(new PackValidationError("PACK-106", "ini.templateSha256", "SHA-256 is missing."));
                return;
            }

            var actual = await HashFileAsync(template.Value, cancellationToken);
            if (!string.Equals(actual, ini.TemplateSha256, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new PackValidationError(
                    "PACK-106",
                    "ini.templateSha256",
                    $"Expected {ini.TemplateSha256} but found {actual}."));
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or CryptographicException)
        {
            errors.Add(new PackValidationError("PACK-105", "ini.template", exception.Message));
        }
    }

    private static async Task ValidateInstallerStepsAsync(
        AssetPack pack,
        string root,
        bool verifyHashes,
        List<PackValidationError> errors,
        CancellationToken cancellationToken)
    {
        if (pack.Steps is null)
        {
            return;
        }

        for (var index = 0; index < pack.Steps.Count; index++)
        {
            var step = pack.Steps[index];
            if (step is null || !string.Equals(step.Type, "runInstaller", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var arguments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (step.Arguments is not null)
            {
                foreach (var pair in step.Arguments)
                {
                    arguments[pair.Key] = pair.Value;
                }
            }

            if (!arguments.TryGetValue("source", out var source) || string.IsNullOrWhiteSpace(source))
            {
                continue;
            }

            var path = $"steps[{index}].source";
            var combined = SafePath.CombineUnderRoot(root, source);
            if (combined.IsFailure || !File.Exists(combined.Value))
            {
                errors.Add(new PackValidationError(
                    "PACK-107",
                    path,
                    combined.IsFailure
                        ? combined.Error?.Diagnostic ?? "Invalid installer path."
                        : $"Installer not found: {combined.Value}"));
                continue;
            }

            if (!verifyHashes)
            {
                continue;
            }

            if (!arguments.TryGetValue("sha256", out var expected) || string.IsNullOrWhiteSpace(expected))
            {
                errors.Add(new PackValidationError("PACK-107", $"{path}.sha256", "Installer SHA-256 is missing."));
                continue;
            }

            try
            {
                var actual = await HashFileAsync(combined.Value, cancellationToken);
                if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(new PackValidationError(
                        "PACK-107",
                        $"{path}.sha256",
                        $"Expected {expected} but found {actual}."));
                }
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or CryptographicException)
            {
                errors.Add(new PackValidationError("PACK-107", path, exception.Message));
            }
        }
    }

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }
}
