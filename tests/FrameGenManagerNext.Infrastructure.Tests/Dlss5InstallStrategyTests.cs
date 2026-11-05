using System.Security.Cryptography;
using System.Text;
using FrameGenManagerNext.Core.Features.Deployment;
using FrameGenManagerNext.Core.Features.Dlss5;
using FrameGenManagerNext.Core.Features.Packs;
using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Errors;
using FrameGenManagerNext.Domain.Library;
using FrameGenManagerNext.Domain.Packs;
using FrameGenManagerNext.Domain.Results;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace FrameGenManagerNext.Infrastructure.Tests;

public sealed class Dlss5InstallStrategyTests : IDisposable
{
    private readonly TestDirectory _directory = new("fgm-dlss5-strategy-tests");

    [Theory]
    [InlineData(GraphicsApi.Direct3D11)]
    [InlineData(GraphicsApi.OpenGl)]
    [InlineData(GraphicsApi.Direct3D12)]
    public async Task NonVulkanUsesEmbeddedExtraction(GraphicsApi api)
    {
        var (exe, service, offline, process) = CreateInstallContext(api);

        var result = await service.InstallAsync(
            Path.GetDirectoryName(exe)!,
            exe,
            progress: null,
            TestContext.Current.CancellationToken,
            api);

        result.IsSuccess.ShouldBeTrue(result.Error?.ToString());
        offline.Calls.ShouldBe(1);
        process.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task NonVulkanFallsBackToOfficialSetupWhenExtractionFails()
    {
        var (exe, service, offline, process) = CreateInstallContext(GraphicsApi.Direct3D11);
        offline.Outcome = Result<string>.Fail(
            new AppError(ErrorCode.InstallerFailed, "extraction failed"));

        var result = await service.InstallAsync(
            Path.GetDirectoryName(exe)!,
            exe,
            progress: null,
            TestContext.Current.CancellationToken,
            GraphicsApi.Direct3D11);

        result.IsSuccess.ShouldBeTrue(result.Error?.ToString());
        offline.Calls.ShouldBe(1);
        process.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task AutoModeUsesDxgiForDirect3D12()
    {
        var (exe, service, offline, process) = CreateInstallContext(GraphicsApi.Direct3D12);

        var result = await service.InstallAsync(
            Path.GetDirectoryName(exe)!,
            exe,
            progress: null,
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.Error?.ToString());
        offline.Calls.ShouldBe(1);
        offline.LastModule.ShouldBe("dxgi.dll");
        process.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task ExplicitDirect3D12UsesItsOwnProxy()
    {
        var (exe, service, offline, _) = CreateInstallContext(GraphicsApi.Direct3D12);

        var result = await service.InstallAsync(
            Path.GetDirectoryName(exe)!,
            exe,
            progress: null,
            TestContext.Current.CancellationToken,
            GraphicsApi.Direct3D12);

        result.IsSuccess.ShouldBeTrue(result.Error?.ToString());
        offline.LastModule.ShouldBe("d3d12.dll");
    }

    [Fact]
    public async Task VulkanUsesTheOfficialSetup()
    {
        var (exe, service, offline, process) = CreateInstallContext(GraphicsApi.Vulkan);

        var result = await service.InstallAsync(
            Path.GetDirectoryName(exe)!,
            exe,
            progress: null,
            TestContext.Current.CancellationToken,
            GraphicsApi.Vulkan);

        result.IsSuccess.ShouldBeTrue(result.Error?.ToString());
        offline.Calls.ShouldBe(0);
        process.Calls.ShouldBe(1);
    }

    private (string Exe, Dlss5DeploymentService Service, FakeOfflineInstaller Offline, FakeProcessRunner Process)
        CreateInstallContext(GraphicsApi api)
    {
        var exe = _directory.WriteFile("game/game.exe", "game");
        var payload = _directory.CreateDirectory("pack");
        var runtime = _directory.WriteFile("pack/payload.bin", "runtime");
        var setup = _directory.WriteFile("pack/setup.exe", "setup");
        var pack = new AssetPack
        {
            SchemaVersion = 1,
            Id = Dlss5DeploymentService.PackId,
            Version = "6.8.0",
            DisplayName = "DLSS5 strategy test",
            MinAppVersion = "2.0.0",
            GpuRoutes = ["Sm86"],
            Files =
            [
                new AssetPackFile
                {
                    Source = "payload.bin",
                    Target = "payload.bin",
                    Role = "addon",
                    Sha256 = Sha256("runtime"),
                    Size = Encoding.UTF8.GetByteCount("runtime"),
                },
            ],
            Steps =
            [
                new AssetPackStep
                {
                    Type = "runInstaller",
                    Arguments = new Dictionary<string, string>
                    {
                        ["source"] = Path.GetFileName(setup),
                        ["sha256"] = Sha256("setup"),
                        ["arguments"] = "--headless --api {api}",
                    },
                },
            ],
        };

        var offline = new FakeOfflineInstaller();
        var process = new FakeProcessRunner();
        var service = new Dlss5DeploymentService(
            new FakePacks(new PackLocation(pack, Path.Combine(payload, "pack.json"), payload)),
            new FakeEngine(),
            new FakeApiProbe(api),
            offline,
            process,
            new FakeReShadeConfig(),
            NullLogger<Dlss5DeploymentService>.Instance);

        return (exe, service, offline, process);
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

    private sealed class FakePacks(PackLocation location) : IPackRepository
    {
        public Task<PackLocation?> FindAsync(string packId, CancellationToken cancellationToken)
        {
            return Task.FromResult<PackLocation?>(location);
        }
    }

    private sealed class FakeEngine : IDeploymentEngine
    {
        public Task<Result<DeploymentReport>> DeployAsync(
            DeploymentPlan plan,
            IProgress<DeploymentProgress>? progress,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Result<DeploymentReport>.Ok(
                new DeploymentReport(DeploymentState.Installed, 1, 0, [])));
        }

        public Task<Result<DeploymentInspection>> InspectAsync(
            DeploymentPlan plan,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyDictionary<string, string>?> LoadOptionsAsync(
            string familyId,
            string targetDirectory,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<DeploymentManifest?> LoadManifestAsync(
            string familyId,
            string targetDirectory,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<DeploymentManifest?>(null);
        }

        public Task<Result<RestoreReport>> RestoreAsync(
            string familyId,
            string targetDirectory,
            IProgress<DeploymentProgress>? progress,
            CancellationToken cancellationToken,
            IReadOnlyList<string>? ownedDirectories = null,
            bool force = false)
        {
            throw new NotSupportedException();
        }

        public Task<Result<RestoreReport>> CleanAsync(
            string familyId,
            string targetDirectory,
            IReadOnlyList<CleanupCandidate> candidates,
            IReadOnlyList<string> ownedDirectories,
            string? iniRelativePath,
            IProgress<DeploymentProgress>? progress,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<Result> DiscardBackupAsync(
            string familyId,
            string targetDirectory,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeApiProbe(GraphicsApi api) : IGraphicsApiProbe
    {
        public Task<GraphicsApi> DetectAsync(string exePath, CancellationToken cancellationToken)
        {
            return Task.FromResult(api);
        }
    }

    private sealed class FakeOfflineInstaller : IReShadeOfflineInstaller
    {
        public int Calls { get; private set; }

        public string? LastModule { get; private set; }

        public Result<string> Outcome { get; set; } = Result<string>.Ok("d3d11.dll");

        public Task<Result<string>> InstallAsync(
            string setupPath,
            string targetDirectory,
            string exePath,
            string proxyModule,
            IProgress<DeploymentProgress>? progress,
            CancellationToken cancellationToken)
        {
            Calls++;
            LastModule = proxyModule;
            return Task.FromResult(Outcome);
        }
    }

    private sealed class FakeProcessRunner : IProcessRunner
    {
        public int Calls { get; private set; }

        public ProcessResult Result { get; set; } =
            new(0, string.Empty, string.Empty, TimedOut: false);

        public Task<ProcessResult> RunAsync(
            string executablePath,
            string arguments,
            string workingDirectory,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeReShadeConfig : IReShadeConfigService
    {
        public Task MergeSearchPathsAsync(string targetDirectory, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
