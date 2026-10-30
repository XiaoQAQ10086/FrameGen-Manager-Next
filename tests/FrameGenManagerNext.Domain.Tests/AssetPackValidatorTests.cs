using FrameGenManagerNext.Domain.Ini;
using FrameGenManagerNext.Domain.Packs;
using Shouldly;

namespace FrameGenManagerNext.Domain.Tests;

public sealed class AssetPackValidatorTests
{
    [Fact]
    public void ValidPackPasses()
    {
        var result = AssetPackValidator.ValidateSchema(ValidPack());

        result.IsValid.ShouldBeTrue(result.Errors.Count > 0 ? result.Errors[0].Message : null);
    }

    [Fact]
    public void InvalidSchemaVersionIsReported()
    {
        var result = AssetPackValidator.ValidateSchema(ValidPack(schemaVersion: 2));

        HasCode(result, "PACK-001");
    }

    [Fact]
    public void InvalidIdIsReported()
    {
        var result = AssetPackValidator.ValidateSchema(ValidPack(id: "Bad Pack"));

        HasCode(result, "PACK-002");
    }

    [Fact]
    public void InvalidVersionIsReported()
    {
        var result = AssetPackValidator.ValidateSchema(ValidPack(version: "latest"));

        HasCode(result, "PACK-003");
    }

    [Fact]
    public void MissingDisplayNameIsReported()
    {
        var result = AssetPackValidator.ValidateSchema(ValidPack(displayName: " "));

        HasCode(result, "PACK-004");
    }

    [Fact]
    public void NewerMinimumAppVersionIsReported()
    {
        var result = AssetPackValidator.ValidateSchema(ValidPack(minAppVersion: "2.1.0"), "2.0.0");

        HasCode(result, "PACK-020");
    }

    [Fact]
    public void UnknownGpuRouteIsReported()
    {
        var result = AssetPackValidator.ValidateSchema(ValidPack(gpuRoutes: ["RTX5090"]));

        HasCode(result, "PACK-006");
    }

    [Fact]
    public void DuplicateGpuRouteIsReported()
    {
        var result = AssetPackValidator.ValidateSchema(ValidPack(gpuRoutes: ["Sm86", "sm86"]));

        HasCode(result, "PACK-023");
    }

    [Fact]
    public void EmptyFilesAreReported()
    {
        var result = AssetPackValidator.ValidateSchema(ValidPack(files: []));

        HasCode(result, "PACK-007");
    }

    [Fact]
    public void OwnedDirectoryWithTraversalIsReported()
    {
        var result = AssetPackValidator.ValidateSchema(ValidPack(ownedDirectories: ["../outside"]));

        HasCode(result, "PACK-008");
    }

    [Fact]
    public void OwnedDirectoryThatIsAlsoAFileTargetIsReported()
    {
        var result = AssetPackValidator.ValidateSchema(ValidPack(ownedDirectories: ["version.dll"]));

        HasCode(result, "PACK-027");
    }

    [Fact]
    public void UnsafeTargetIsReported()
    {
        var result = AssetPackValidator.ValidateSchema(
            ValidPack(files: [ValidFile(target: "../version.dll")]));

        HasCode(result, "PACK-008");
    }

    [Fact]
    public void DuplicateTargetsAreReported()
    {
        var result = AssetPackValidator.ValidateSchema(
            ValidPack(files: [ValidFile(), ValidFile(source: "other.dll")]));

        HasCode(result, "PACK-009");
    }

    [Fact]
    public void UnknownRoleIsReported()
    {
        var result = AssetPackValidator.ValidateSchema(
            ValidPack(files: [ValidFile(role: "magic")]));

        HasCode(result, "PACK-010");
    }

    [Fact]
    public void ProxySlotOnNonProxyIsReported()
    {
        var result = AssetPackValidator.ValidateSchema(
            ValidPack(files: [ValidFile(role: "runtime", proxySlot: true)]));

        HasCode(result, "PACK-011");
    }

    [Fact]
    public void InvalidHashIsReported()
    {
        var result = AssetPackValidator.ValidateSchema(
            ValidPack(files: [ValidFile(sha256: "abc")]));

        HasCode(result, "PACK-012");
    }

    [Fact]
    public void MissingSizeIsReported()
    {
        var result = AssetPackValidator.ValidateSchema(
            ValidPack(files: [ValidFile(size: null)]));

        HasCode(result, "PACK-013");
    }

    [Fact]
    public void IniTargetCannotConflictWithFileTarget()
    {
        var ini = new AssetPackIni
        {
            Target = "version.dll",
            Template = "template.ini",
            Mode = "merge",
        };

        var result = AssetPackValidator.ValidateSchema(ValidPack(ini: ini));

        HasCode(result, "PACK-019");
    }

    [Fact]
    public void ValidIniPackPasses()
    {
        var ini = new AssetPackIni
        {
            Target = "dlssg_sm86.ini",
            Template = "template.ini",
            Mode = "merge",
            TemplateSha256 = new string('c', 64),
            TemplateSize = 10,
        };

        var result = AssetPackValidator.ValidateSchema(ValidPack(ini: ini));

        result.IsValid.ShouldBeTrue(result.Errors.Count > 0 ? result.Errors[0].Message : null);
    }

    [Fact]
    public void MissingIniTemplateMetadataIsReported()
    {
        var ini = new AssetPackIni
        {
            Target = "dlssg_sm86.ini",
            Template = "template.ini",
            Mode = "merge",
        };

        var result = AssetPackValidator.ValidateSchema(ValidPack(ini: ini));

        HasCode(result, "PACK-024");
        HasCode(result, "PACK-025");
    }

    [Fact]
    public void UnknownIniModeIsReported()
    {
        var ini = new AssetPackIni
        {
            Target = "dlssg_sm86.ini",
            Template = "template.ini",
            Mode = "append",
        };

        var result = AssetPackValidator.ValidateSchema(ValidPack(ini: ini));

        HasCode(result, "PACK-014");
    }

    [Fact]
    public void DuplicateStockValueIsReported()
    {
        var ini = new AssetPackIni
        {
            Target = "dlssg_sm86.ini",
            Template = "template.ini",
            Mode = "merge",
            StockValues =
            [
                new IniSetting("DLSSG", "Preset", "A"),
                new IniSetting("dlssg", "preset", "B"),
            ],
        };

        var result = AssetPackValidator.ValidateSchema(ValidPack(ini: ini));

        HasCode(result, "PACK-021");
    }

    [Fact]
    public void UnknownStepTypeIsReported()
    {
        var result = AssetPackValidator.ValidateSchema(
            ValidPack(steps: [new AssetPackStep { Type = "deleteEverything" }]));

        HasCode(result, "PACK-016");
    }

    [Fact]
    public void RunInstallerIsRejectedForVerifiedPacks()
    {
        var step = new AssetPackStep
        {
            Type = "runInstaller",
            Arguments = new Dictionary<string, string>
            {
                ["source"] = "ReShade_Setup.exe",
                ["sha256"] = new string('b', 64),
            },
        };

        var result = AssetPackValidator.ValidateSchema(
            ValidPack(steps: [step], trust: AssetPackTrust.Verified));

        HasCode(result, "PACK-018");
    }

    [Fact]
    public void RunInstallerRequiresHash()
    {
        var step = new AssetPackStep
        {
            Type = "runInstaller",
            Arguments = new Dictionary<string, string> { ["source"] = "ReShade_Setup.exe" },
        };

        var result = AssetPackValidator.ValidateSchema(ValidPack(steps: [step]));

        HasCode(result, "PACK-017");
    }

    [Fact]
    public void RunInstallerRejectsUnknownArgument()
    {
        var step = new AssetPackStep
        {
            Type = "runInstaller",
            Arguments = new Dictionary<string, string>
            {
                ["source"] = "ReShade_Setup.exe",
                ["sha256"] = new string('b', 64),
                ["shell"] = "true",
            },
        };

        var result = AssetPackValidator.ValidateSchema(ValidPack(steps: [step]));

        HasCode(result, "PACK-017");
    }

    [Fact]
    public void RemoveStaleFilesRequiresTarget()
    {
        var result = AssetPackValidator.ValidateSchema(
            ValidPack(steps: [new AssetPackStep { Type = "removeStaleFiles" }]));

        HasCode(result, "PACK-017");
    }

    [Fact]
    public void CopyFilesRejectsArguments()
    {
        var step = new AssetPackStep
        {
            Type = "copyFiles",
            Arguments = new Dictionary<string, string> { ["source"] = "x.dll" },
        };

        var result = AssetPackValidator.ValidateSchema(ValidPack(steps: [step]));

        HasCode(result, "PACK-017");
    }

    private static void HasCode(PackValidationResult result, string code)
    {
        result.Errors.ShouldContain(error => error.Code == code);
    }

    private static AssetPack ValidPack(
        int schemaVersion = 1,
        string id = "framegen.sm86",
        string version = "0.4.2",
        string displayName = "FrameGen SM86",
        string minAppVersion = "2.0.0",
        List<string>? gpuRoutes = null,
        List<AssetPackFile>? files = null,
        AssetPackIni? ini = null,
        List<AssetPackStep>? steps = null,
        AssetPackTrust trust = AssetPackTrust.Bundled,
        List<string>? ownedDirectories = null)
    {
        return new AssetPack
        {
            SchemaVersion = schemaVersion,
            Id = id,
            Version = version,
            DisplayName = displayName,
            MinAppVersion = minAppVersion,
            GpuRoutes = gpuRoutes ?? ["Sm86"],
            Files = files ?? [ValidFile()],
            OwnedDirectories = ownedDirectories ?? [],
            Ini = ini,
            Steps = steps ?? [],
            Trust = trust,
        };
    }

    private static AssetPackFile ValidFile(
        string source = "version.dll",
        string target = "version.dll",
        string role = "proxy",
        bool proxySlot = true,
        string? sha256 = null,
        long? size = 123)
    {
        return new AssetPackFile
        {
            Source = source,
            Target = target,
            Role = role,
            ProxySlot = proxySlot,
            Sha256 = sha256 ?? new string('a', 64),
            Size = size,
        };
    }
}
