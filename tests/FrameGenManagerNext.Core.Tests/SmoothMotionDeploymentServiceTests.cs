using System.Security.Cryptography;
using FrameGenManagerNext.Core.Features.Deployment;
using FrameGenManagerNext.Core.Features.Gpu;
using FrameGenManagerNext.Core.Features.Packs;
using FrameGenManagerNext.Core.Features.SmoothMotion;
using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Gpu;
using FrameGenManagerNext.Domain.Packs;
using FrameGenManagerNext.Domain.Results;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace FrameGenManagerNext.Core.Tests;

public sealed class SmoothMotionDeploymentServiceTests
{
    [Fact]
    public async Task EnableProceedsWhenAntiCheatIsPresent()
    {
        var root = Path.Combine(Path.GetTempPath(), $"fgm-sm-{Guid.NewGuid():N}");
        var gameDirectory = Path.Combine(root, "game");
        var payloadDirectory = Path.Combine(root, "payload");
        Directory.CreateDirectory(Path.Combine(gameDirectory, "BattlEye"));
        Directory.CreateDirectory(Path.Combine(payloadDirectory, "rtx30"));
        var payload = Path.Combine(payloadDirectory, "rtx30", "version.dll");
        await File.WriteAllTextAsync(payload, "payload", TestContext.Current.CancellationToken);

        try
        {
            var size = new FileInfo(payload).Length;
            var hash = Convert.ToHexString(
                SHA256.HashData(await File.ReadAllBytesAsync(payload, TestContext.Current.CancellationToken)))
                .ToLowerInvariant();
            var pack = new AssetPack
            {
                SchemaVersion = 1,
                Id = "smooth-motion.rtx30",
                Version = "1.0.0",
                DisplayName = "Test pack",
                MinAppVersion = "2.0.0",
                GpuRoutes = ["Sm86"],
                Files =
                [
                    new AssetPackFile
                    {
                        Source = "rtx30/version.dll",
                        Target = "version.dll",
                        Role = "runtime",
                        Sha256 = hash,
                        Size = size,
                    },
                ],
                Steps = [new AssetPackStep { Type = "copyFiles" }],
            };
            var engine = new FakeEngine();
            var nvapi = new FakeNvApi();

            var service = new SmoothMotionDeploymentService(
                new SupportedGpu(),
                new FakePacks(new PackLocation(pack, Path.Combine(root, "pack.json"), payloadDirectory)),
                engine,
                nvapi,
                NullLogger<SmoothMotionDeploymentService>.Instance);

            var result = await service.EnableAsync(
                "Test Game",
                gameDirectory,
                Path.Combine(gameDirectory, "Game.exe"),
                progress: null,
                TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeTrue();
            engine.Deployed.ShouldBeTrue();
            nvapi.SetCalls.ShouldBe(1);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class SupportedGpu : IGpuService
    {
        public Task<GpuInfo> GetAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new GpuInfo(
                "NVIDIA GeForce RTX 3070",
                "621.01",
                GpuRoute.Sm86,
                HardwareAcceleratedGpuScheduling: true,
                GpuFeatureGate.Supported,
                GpuFeatureGate.Supported));
        }
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
        public bool Deployed { get; private set; }

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

        public Task<Result<DeploymentReport>> DeployAsync(
            DeploymentPlan plan,
            IProgress<DeploymentProgress>? progress,
            CancellationToken cancellationToken)
        {
            Deployed = true;
            return Task.FromResult(Result<DeploymentReport>.Ok(
                new DeploymentReport(DeploymentState.Installed, 1, 0, [])));
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

    private sealed class FakeNvApi : INvApiSmoothMotion
    {
        public int SetCalls { get; private set; }

        public Task<SmoothMotionSettingState> GetStateAsync(
            string exePath,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<SmoothMotionSettingState>> GetStatesAsync(
            IReadOnlyList<string> exePaths,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<SmoothMotionChangeResult> SetAsync(
            string game,
            string targetDirectory,
            string exePath,
            bool enabled,
            CancellationToken cancellationToken)
        {
            SetCalls++;
            return Task.FromResult(new SmoothMotionChangeResult(true, 1, "Test", "enabled"));
        }

        public Task<SmoothMotionChangeResult> RestoreAsync(
            string game,
            string targetDirectory,
            string exePath,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
