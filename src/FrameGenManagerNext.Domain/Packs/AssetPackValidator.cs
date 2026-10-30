using System.Text.RegularExpressions;
using FrameGenManagerNext.Domain.Safety;

namespace FrameGenManagerNext.Domain.Packs;

/// <summary>
/// Pure schema validation for pack.json. Payload hashes are checked by the infrastructure validator.
/// </summary>
public static class AssetPackValidator
{
    public const int SupportedSchemaVersion = 1;

    private static readonly HashSet<string> AllowedRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "proxy",
        "runtime",
        "sr",
        "addon",
        "model",
        "shader",
        "effect",
        "texture",
        "preset",
        "installer",
        "config",
        "other",
    };

    private static readonly HashSet<string> AllowedGpuRoutes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Any",
        "Sm86",
        "Sm75",
        "Gtx16",
        "Pascal",
        "NotNeeded",
        "Unsupported",
        "Unknown",
    };

    private static readonly HashSet<string> AllowedIniModes = new(StringComparer.OrdinalIgnoreCase)
    {
        "merge",
        "replace",
    };

    private static readonly HashSet<string> AllowedStepTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "copyFiles",
        "writeIni",
        "runInstaller",
        "removeStaleFiles",
    };

    private static readonly Regex IdPattern = new(
        "^[a-z0-9][a-z0-9._-]{1,63}$",
        RegexOptions.CultureInvariant);

    private static readonly Regex SemVerPattern = new(
        @"^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?(\+[0-9A-Za-z.-]+)?$",
        RegexOptions.CultureInvariant);

    private static readonly Regex Sha256Pattern = new(
        "^[0-9a-fA-F]{64}$",
        RegexOptions.CultureInvariant);

    public static PackValidationResult ValidateSchema(AssetPack? pack, string? currentAppVersion = null)
    {
        ArgumentNullException.ThrowIfNull(pack);

        var errors = new List<PackValidationError>();

        if (pack.SchemaVersion != SupportedSchemaVersion)
        {
            Add(errors, "PACK-001", "schemaVersion", $"Only schema version {SupportedSchemaVersion} is supported.");
        }

        if (string.IsNullOrWhiteSpace(pack.Id) || !IdPattern.IsMatch(pack.Id))
        {
            Add(errors, "PACK-002", "id", "Pack id must match ^[a-z0-9][a-z0-9._-]{1,63}$.");
        }

        if (string.IsNullOrWhiteSpace(pack.Version) || !SemVerPattern.IsMatch(pack.Version))
        {
            Add(errors, "PACK-003", "version", "Pack version must be a semantic version.");
        }

        if (string.IsNullOrWhiteSpace(pack.DisplayName) || pack.DisplayName.Length > 120)
        {
            Add(errors, "PACK-004", "displayName", "Display name is required and must not exceed 120 characters.");
        }

        ValidateVersionRequirement(pack.MinAppVersion, currentAppVersion, errors);
        ValidateGpuRoutes(pack.GpuRoutes, errors);

        var targets = ValidateFiles(pack.Files, errors);
        ValidateOwnedDirectories(pack.OwnedDirectories, targets, errors);
        ValidateIni(pack.Ini, targets, errors);
        ValidateSteps(pack.Steps, pack.Trust, errors);

        return new PackValidationResult(errors);
    }

    private static void ValidateOwnedDirectories(
        List<string>? directories,
        HashSet<string> fileTargets,
        List<PackValidationError> errors)
    {
        if (directories is null || directories.Count == 0)
        {
            return;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < directories.Count; index++)
        {
            var path = $"ownedDirectories[{index}]";
            var normalized = NormalizePath(directories[index], path, errors);
            if (normalized is null)
            {
                continue;
            }

            if (!seen.Add(normalized))
            {
                Add(errors, "PACK-026", path, $"Duplicate owned directory '{normalized}'.");
            }

            if (fileTargets.Contains(normalized))
            {
                Add(errors, "PACK-027", path, $"Owned directory '{normalized}' conflicts with a file target.");
            }
        }
    }

    private static void ValidateVersionRequirement(
        string? minAppVersion,
        string? currentAppVersion,
        List<PackValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(minAppVersion))
        {
            return;
        }

        if (!SemVerPattern.IsMatch(minAppVersion))
        {
            Add(errors, "PACK-005", "minAppVersion", "minAppVersion must be a semantic version.");
            return;
        }

        if (string.IsNullOrWhiteSpace(currentAppVersion) ||
            !TryParseSemVer(currentAppVersion, out var current) ||
            !TryParseSemVer(minAppVersion, out var minimum))
        {
            return;
        }

        if (minimum.CompareTo(current) > 0)
        {
            Add(
                errors,
                "PACK-020",
                "minAppVersion",
                $"Pack requires app version {minAppVersion} but the current version is {currentAppVersion}.");
        }
    }

    private static void ValidateGpuRoutes(List<string>? routes, List<PackValidationError> errors)
    {
        if (routes is null || routes.Count == 0)
        {
            Add(errors, "PACK-006", "gpuRoutes", "At least one GPU route is required.");
            return;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < routes.Count; index++)
        {
            var route = routes[index];
            var field = $"gpuRoutes[{index}]";

            if (string.IsNullOrWhiteSpace(route) || !AllowedGpuRoutes.Contains(route))
            {
                Add(errors, "PACK-006", field, $"Unknown GPU route '{route}'.");
                continue;
            }

            if (!seen.Add(route))
            {
                Add(errors, "PACK-023", field, $"Duplicate GPU route '{route}'.");
            }
        }
    }

    private static HashSet<string> ValidateFiles(
        List<AssetPackFile>? files,
        List<PackValidationError> errors)
    {
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (files is null || files.Count == 0)
        {
            Add(errors, "PACK-007", "files", "At least one file is required.");
            return targets;
        }

        for (var index = 0; index < files.Count; index++)
        {
            var file = files[index];
            var path = $"files[{index}]";

            if (file is null)
            {
                Add(errors, "PACK-022", path, "File entry must not be null.");
                continue;
            }

            var target = NormalizePath(file.Target, $"{path}.target", errors);
            NormalizePath(file.Source, $"{path}.source", errors);

            if (target is not null && !targets.Add(target))
            {
                Add(errors, "PACK-009", $"{path}.target", $"Duplicate target '{target}'.");
            }

            if (string.IsNullOrWhiteSpace(file.Role) || !AllowedRoles.Contains(file.Role))
            {
                Add(errors, "PACK-010", $"{path}.role", $"Unknown file role '{file.Role}'.");
            }

            if (file.ProxySlot && !string.Equals(file.Role, "proxy", StringComparison.OrdinalIgnoreCase))
            {
                Add(errors, "PACK-011", $"{path}.proxySlot", "proxySlot is only valid for role 'proxy'.");
            }

            if (string.IsNullOrWhiteSpace(file.Sha256) || !Sha256Pattern.IsMatch(file.Sha256))
            {
                Add(errors, "PACK-012", $"{path}.sha256", "SHA-256 must be 64 hexadecimal characters.");
            }

            if (file.Size is null || file.Size < 0)
            {
                Add(errors, "PACK-013", $"{path}.size", "File size must be a non-negative number.");
            }
        }

        return targets;
    }

    private static void ValidateIni(
        AssetPackIni? ini,
        HashSet<string> fileTargets,
        List<PackValidationError> errors)
    {
        if (ini is null)
        {
            return;
        }

        var target = NormalizePath(ini.Target, "ini.target", errors);
        NormalizePath(ini.Template, "ini.template", errors);

        if (target is not null && fileTargets.Contains(target))
        {
            Add(errors, "PACK-019", "ini.target", $"INI target '{target}' conflicts with a file target.");
        }

        if (string.IsNullOrWhiteSpace(ini.Mode) || !AllowedIniModes.Contains(ini.Mode))
        {
            Add(errors, "PACK-014", "ini.mode", $"Unknown INI mode '{ini.Mode}'.");
        }

        if (string.IsNullOrWhiteSpace(ini.TemplateSha256) || !Sha256Pattern.IsMatch(ini.TemplateSha256))
        {
            Add(errors, "PACK-024", "ini.templateSha256", "INI template SHA-256 must be 64 hexadecimal characters.");
        }

        if (ini.TemplateSize is null || ini.TemplateSize < 0)
        {
            Add(errors, "PACK-025", "ini.templateSize", "INI template size must be a non-negative number.");
        }

        if (ini.StockValues is null)
        {
            return;
        }

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < ini.StockValues.Count; index++)
        {
            var setting = ini.StockValues[index];
            var path = $"ini.stockValues[{index}]";

            if (setting is null ||
                setting.Section is null ||
                string.IsNullOrWhiteSpace(setting.Key) ||
                setting.Value is null)
            {
                Add(errors, "PACK-015", path, "Stock value requires a section, key and value.");
                continue;
            }

            var key = $"{setting.Section}\u001F{setting.Key}";
            if (!keys.Add(key))
            {
                Add(errors, "PACK-021", path, $"Duplicate stock value '{setting.Section}/{setting.Key}'.");
            }
        }
    }

    private static void ValidateSteps(
        List<AssetPackStep>? steps,
        AssetPackTrust trust,
        List<PackValidationError> errors)
    {
        if (steps is null || steps.Count == 0)
        {
            return;
        }

        for (var index = 0; index < steps.Count; index++)
        {
            var step = steps[index];
            var path = $"steps[{index}]";

            if (step is null)
            {
                Add(errors, "PACK-022", path, "Step entry must not be null.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(step.Type) || !AllowedStepTypes.Contains(step.Type))
            {
                Add(errors, "PACK-016", $"{path}.type", $"Unknown step type '{step.Type}'.");
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

            switch (step.Type.ToLowerInvariant())
            {
                case "copyfiles":
                case "writeini":
                    ValidateNoArguments(arguments, path, errors);
                    break;

                case "removestalefiles":
                    ValidateArguments(arguments, path, errors, ["target"]);
                    ValidateRequiredPath(arguments, "target", path, errors);
                    break;

                case "runinstaller":
                    if (trust != AssetPackTrust.Bundled)
                    {
                        Add(errors, "PACK-018", path, "runInstaller is only allowed for bundled packs.");
                    }

                    ValidateArguments(arguments, path, errors, ["source", "sha256", "arguments"]);
                    ValidateRequiredPath(arguments, "source", path, errors);

                    if (!arguments.TryGetValue("sha256", out var installerHash) ||
                        string.IsNullOrWhiteSpace(installerHash) ||
                        !Sha256Pattern.IsMatch(installerHash))
                    {
                        Add(errors, "PACK-017", $"{path}.sha256", "runInstaller requires a 64-character SHA-256.");
                    }

                    break;
            }
        }
    }

    private static void ValidateNoArguments(
        Dictionary<string, string> arguments,
        string path,
        List<PackValidationError> errors)
    {
        if (arguments.Count > 0)
        {
            Add(errors, "PACK-017", path, "This step does not accept arguments.");
        }
    }

    private static void ValidateArguments(
        Dictionary<string, string> arguments,
        string path,
        List<PackValidationError> errors,
        string[] allowed)
    {
        var allowedSet = new HashSet<string>(allowed, StringComparer.OrdinalIgnoreCase);
        foreach (var key in arguments.Keys)
        {
            if (!allowedSet.Contains(key))
            {
                Add(errors, "PACK-017", $"{path}.{key}", $"Unknown argument '{key}'.");
            }
        }
    }

    private static void ValidateRequiredPath(
        Dictionary<string, string> arguments,
        string key,
        string path,
        List<PackValidationError> errors)
    {
        if (!arguments.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            Add(errors, "PACK-017", $"{path}.{key}", $"Argument '{key}' is required.");
            return;
        }

        NormalizePath(value, $"{path}.{key}", errors);
    }

    private static string? NormalizePath(
        string? value,
        string field,
        List<PackValidationError> errors)
    {
        var result = SafePath.NormalizeRelativePath(value);
        if (result.IsFailure)
        {
            Add(errors, "PACK-008", field, result.Error?.Diagnostic ?? "Invalid path.");
            return null;
        }

        return result.Value;
    }

    private static bool TryParseSemVer(string value, out Version version)
    {
        var core = value.Split('-', 2)[0].Split('+', 2)[0];
        if (Version.TryParse(core, out var parsed))
        {
            version = parsed;
            return true;
        }

        version = new Version(0, 0, 0);
        return false;
    }

    private static void Add(
        List<PackValidationError> errors,
        string code,
        string path,
        string message)
    {
        errors.Add(new PackValidationError(code, path, message));
    }
}
