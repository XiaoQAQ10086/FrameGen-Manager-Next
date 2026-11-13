using System.Security.Cryptography;
using System.Text;
using FrameGenManagerNext.Core.Features.Dlss5;
using FrameGenManagerNext.Core.Features.Packs;
using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Library;
using FrameGenManagerNext.Domain.Packs;
using FrameGenManagerNext.Domain.Results;
using FrameGenManagerNext.Infrastructure.Deployment;
using FrameGenManagerNext.Infrastructure.Paths;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace FrameGenManagerNext.Infrastructure.Tests;

public sealed class Dlss5UninstallCleanupTests : IDisposable
{
    private readonly TestDirectory _directory = new("fgm-dlss5-uninstall-tests");
    private readonly AppPaths _paths;
    private readonly DeploymentEngine _engine;
    private readonly string _target;

    public Dlss5UninstallCleanupTests()
    {
        _paths = new AppPaths(_directory.GetPath("data"));
        _paths.EnsureCreated();
        _target = _directory.CreateDirectory("game");
        var backups = new BackupStore(_paths, NullLogger<BackupStore>.Instance);
        _engine = new DeploymentEngine(_paths, backups, NullLogger<DeploymentEngine>.Instance);
    }

    [Fact]
    public async Task UninstallRemovesPackFilesLeftWithoutManifest()
    {
        const string content = "texture-bytes";
        _directory.WriteFile("game/reshade-shaders/Textures/lut.png", content);
        var pack = PackWith("reshade-shaders/Textures/lut.png", content);

        var result = await CreateService(pack).UninstallAsync(
            _target,
            exePath: null,
            progress: null,
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.Error?.ToString());
        result.Value.IsClean.ShouldBeTrue();
        result.Value.Removed.ShouldContain("lut.png");
        File.Exists(Path.Combine(_target, "reshade-shaders/Textures/lut.png")).ShouldBeFalse();
        Directory.Exists(Path.Combine(_target, "reshade-shaders")).ShouldBeFalse();
    }

    [Fact]
    public async Task UninstallKeepsModifiedPackFileAndReportsIt()
    {
        _directory.WriteFile("game/reshade-shaders/Textures/lut.png", "user changed this");
        var pack = PackWith("reshade-shaders/Textures/lut.png", "texture-bytes");

        var result = await CreateService(pack).UninstallAsync(
            _target,
            exePath: null,
            progress: null,
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.Error?.ToString());
        result.Value.IsClean.ShouldBeFalse();
        result.Value.Remaining.ShouldContain("reshade-shaders/Textures/lut.png");
        File.ReadAllText(Path.Combine(_target, "reshade-shaders/Textures/lut.png"))
            .ShouldBe("user changed this");
    }

    [Fact]
    public async Task UninstallRestoresOriginalFileInsteadOfDeletingIt()
    {
        const string relative = "reshade-shaders/Textures/lut.png";
        _directory.WriteFile("game/reshade-shaders/Textures/lut.png", "game original");
        var source = _directory.WriteFile("source/lut.png", "deployed texture");
        var pack = PackWith(relative, "deployed texture");
        var plan = new DeploymentPlan(
            Dlss5DeploymentService.FamilyId,
            Dlss5DeploymentService.PackId,
            "6.8.0",
            _target,
            new[]
            {
                new DeploymentFilePlan(
                    relative,
                    source,
                    Content: null,
                    Sha256(File.ReadAllText(source)),
                    new FileInfo(source).Length,
                    "shader"),
            },
            new Dictionary<string, string>());

        var deployed = await _engine.DeployAsync(
            plan,
            progress: null,
            TestContext.Current.CancellationToken);
        deployed.IsSuccess.ShouldBeTrue(deployed.Error?.ToString());

        var result = await CreateService(pack).UninstallAsync(
            _target,
            exePath: null,
            progress: null,
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.Error?.ToString());
        result.Value.IsClean.ShouldBeTrue();
        result.Value.Remaining.ShouldBeEmpty();
        File.ReadAllText(Path.Combine(_target, relative)).ShouldBe("game original");
    }

    [Fact]
    public async Task UninstallRemovesThirdPartyDlss5ModFiles()
    {
        _directory.WriteFile("game/nvngx_dlssnr.dll", "mod model");
        _directory.WriteFile("game/renodx-dlss-SF-26100421.addon64", "mod addon");
        var pack = PackWith("unused.bin", "unused");

        var result = await CreateService(pack).UninstallAsync(
            _target,
            exePath: null,
            progress: null,
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.Error?.ToString());
        result.Value.IsClean.ShouldBeTrue();
        result.Value.Removed.ShouldContain("nvngx_dlssnr.dll");
        result.Value.Removed.ShouldContain("renodx-dlss-SF-26100421.addon64");
        File.Exists(Path.Combine(_target, "nvngx_dlssnr.dll")).ShouldBeFalse();
        File.Exists(Path.Combine(_target, "renodx-dlss-SF-26100421.addon64")).ShouldBeFalse();
    }

    private Dlss5DeploymentService CreateService(AssetPack pack)
    {
        return new Dlss5DeploymentService(
            new FakePacks(pack, _directory.GetPath("pack")),
            _engine,
            new FakeApiProbe(),
            new FakeReShadeInstaller(),
            new FakeProcessRunner(),
            new FakeReShadeConfig(),
            NullLogger<Dlss5DeploymentService>.Instance);
    }

    private static AssetPack PackWith(string relativePath, string content)
    {
        return new AssetPack
        {
            Id = Dlss5DeploymentService.PackId,
            Version = "6.8.0",
            DisplayName = "DLSS5 test pack",
            Files =
            [
                new AssetPackFile
                {
                    Source = "source.bin",
                    Target = relativePath,
                    Role = "shader",
                    Sha256 = Sha256(content),
                    Size = Encoding.UTF8.GetByteCount(content),
                },
            ],
        };
    }

    private static string Sha256(string content)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)))
            .ToLowerInvariant();
    }

    public void Dispose()
    {
        _directory.Dispose();
    }

    private sealed class FakePacks(AssetPack pack, string payloadDirectory) : IPackRepository
    {
        public Task<PackLocation?> FindAsync(string packId, CancellationToken cancellationToken)
        {
            return Task.FromResult<PackLocation?>(
                new PackLocation(pack, Path.Combine(payloadDirectory, "pack.json"), payloadDirectory));
        }
    }

    private sealed class FakeApiProbe : IGraphicsApiProbe
    {
        public Task<GraphicsApi> DetectAsync(string exePath, CancellationToken cancellationToken)
        {
            return Task.FromResult(GraphicsApi.Direct3D11);
        }
    }

    private sealed class FakeReShadeInstaller : IReShadeOfflineInstaller
    {
        public Task<Result<string>> InstallAsync(
            string setupPath,
            string targetDirectory,
            string exePath,
            string proxyModule,
            IProgress<DeploymentProgress>? progress,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeProcessRunner : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(
            string executablePath,
            string arguments,
            string workingDirectory,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeReShadeConfig : IReShadeConfigService
    {
        public Task MergeSearchPathsAsync(string targetDirectory, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<HotkeyWriteResult> EnsureOverlayKeyAsync(
            string targetDirectory,
            string? exePath,
            int virtualKey,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<HotkeyFlushReport> FlushPendingAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
