using System.Security.Cryptography;
using System.Text;
using FrameGenManagerNext.Domain.Packs;
using FrameGenManagerNext.Infrastructure.Packs;
using Shouldly;

namespace FrameGenManagerNext.Infrastructure.Tests;

public sealed class PackPayloadValidatorTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "FrameGenPackPayloadTests",
        Guid.NewGuid().ToString("N"));

    public PackPayloadValidatorTests()
    {
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public async Task ValidPayloadPasses()
    {
        var (hash, size) = WriteFile("version.dll", "abc");
        var pack = BuildPack("version.dll", hash, size);

        var result = await PackPayloadValidator.ValidateAsync(
            pack,
            _directory,
            cancellationToken: TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue(result.Errors.Count > 0 ? result.Errors[0].Message : null);
    }

    [Fact]
    public async Task WrongHashIsReported()
    {
        var (_, size) = WriteFile("version.dll", "abc");
        var pack = BuildPack("version.dll", new string('0', 64), size);

        var result = await PackPayloadValidator.ValidateAsync(
            pack,
            _directory,
            cancellationToken: TestContext.Current.CancellationToken);

        result.Errors.ShouldContain(error => error.Code == "PACK-103");
    }

    [Fact]
    public async Task WrongSizeIsReported()
    {
        var (hash, size) = WriteFile("version.dll", "abc");
        var pack = BuildPack("version.dll", hash, size + 1);

        var result = await PackPayloadValidator.ValidateAsync(
            pack,
            _directory,
            cancellationToken: TestContext.Current.CancellationToken);

        result.Errors.ShouldContain(error => error.Code == "PACK-102");
    }

    [Fact]
    public async Task MissingFileIsReported()
    {
        var pack = BuildPack("missing.dll", new string('a', 64), 1);

        var result = await PackPayloadValidator.ValidateAsync(
            pack,
            _directory,
            cancellationToken: TestContext.Current.CancellationToken);

        result.Errors.ShouldContain(error => error.Code == "PACK-101");
    }

    [Fact]
    public async Task TraversalSourceIsReported()
    {
        var pack = BuildPack("../outside.dll", new string('a', 64), 1);

        var result = await PackPayloadValidator.ValidateAsync(
            pack,
            _directory,
            cancellationToken: TestContext.Current.CancellationToken);

        result.Errors.ShouldContain(error => error.Code == "PACK-101");
    }

    [Fact]
    public async Task MissingIniTemplateIsReported()
    {
        var (hash, size) = WriteFile("version.dll", "abc");
        var pack = BuildPack("version.dll", hash, size, new AssetPackIni
        {
            Target = "dlssg_sm86.ini",
            Template = "missing-template.ini",
            Mode = "merge",
            TemplateSha256 = new string('a', 64),
            TemplateSize = 1,
        });

        var result = await PackPayloadValidator.ValidateAsync(
            pack,
            _directory,
            cancellationToken: TestContext.Current.CancellationToken);

        result.Errors.ShouldContain(error => error.Code == "PACK-104");
    }

    [Fact]
    public async Task ValidIniTemplatePasses()
    {
        var (fileHash, fileSize) = WriteFile("version.dll", "abc");
        var (templateHash, templateSize) = WriteFile("template.ini", "[A]\n");
        var pack = BuildPack("version.dll", fileHash, fileSize, new AssetPackIni
        {
            Target = "dlssg_sm86.ini",
            Template = "template.ini",
            Mode = "merge",
            TemplateSha256 = templateHash,
            TemplateSize = templateSize,
        });

        var result = await PackPayloadValidator.ValidateAsync(
            pack,
            _directory,
            cancellationToken: TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue(result.Errors.Count > 0 ? result.Errors[0].Message : null);
    }

    [Fact]
    public async Task ValidInstallerPasses()
    {
        var (fileHash, fileSize) = WriteFile("version.dll", "abc");
        var (installerHash, _) = WriteFile("setup.exe", "installer");
        var pack = BuildPack(
            "version.dll",
            fileHash,
            fileSize,
            steps:
            [
                new AssetPackStep
                {
                    Type = "runInstaller",
                    Arguments = new Dictionary<string, string>
                    {
                        ["source"] = "setup.exe",
                        ["sha256"] = installerHash,
                        ["arguments"] = "--headless --api {api}",
                    },
                },
            ]);

        var result = await PackPayloadValidator.ValidateAsync(
            pack,
            _directory,
            cancellationToken: TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue(result.Errors.Count > 0 ? result.Errors[0].Message : null);
    }

    [Fact]
    public async Task InstallerHashMismatchIsReported()
    {
        var (fileHash, fileSize) = WriteFile("version.dll", "abc");
        WriteFile("setup.exe", "installer");
        var pack = BuildPack(
            "version.dll",
            fileHash,
            fileSize,
            steps:
            [
                new AssetPackStep
                {
                    Type = "runInstaller",
                    Arguments = new Dictionary<string, string>
                    {
                        ["source"] = "setup.exe",
                        ["sha256"] = new string('0', 64),
                    },
                },
            ]);

        var result = await PackPayloadValidator.ValidateAsync(
            pack,
            _directory,
            cancellationToken: TestContext.Current.CancellationToken);

        result.Errors.ShouldContain(error => error.Code == "PACK-107");
    }

    [Fact]
    public async Task HashVerificationCanBeSkipped()
    {
        var (_, size) = WriteFile("version.dll", "abc");
        var pack = BuildPack("version.dll", new string('0', 64), size);

        var result = await PackPayloadValidator.ValidateAsync(
            pack,
            _directory,
            verifyHashes: false,
            cancellationToken: TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue(result.Errors.Count > 0 ? result.Errors[0].Message : null);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static AssetPack BuildPack(
        string source,
        string hash,
        long size,
        AssetPackIni? ini = null,
        List<AssetPackStep>? steps = null)
    {
        return new AssetPack
        {
            SchemaVersion = 1,
            Id = "framegen.sm86",
            Version = "0.4.2",
            DisplayName = "FrameGen SM86",
            GpuRoutes = ["Sm86"],
            Files =
            [
                new AssetPackFile
                {
                    Source = source,
                    Target = "version.dll",
                    Role = "proxy",
                    ProxySlot = true,
                    Sha256 = hash,
                    Size = size,
                },
            ],
            Ini = ini,
            Steps = steps ?? [],
        };
    }

    private (string Hash, long Size) WriteFile(string name, string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        File.WriteAllBytes(Path.Combine(_directory, name), bytes);
        return (Convert.ToHexString(SHA256.HashData(bytes)), bytes.LongLength);
    }
}
