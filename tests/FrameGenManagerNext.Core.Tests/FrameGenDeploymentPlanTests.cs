using System.Security.Cryptography;
using FrameGenManagerNext.Core.Features.Deployment;
using FrameGenManagerNext.Core.Features.Framegen;
using FrameGenManagerNext.Core.Features.Packs;
using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Ini;
using FrameGenManagerNext.Domain.Packs;
using FrameGenManagerNext.Domain.Results;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace FrameGenManagerNext.Core.Tests;

public sealed class FrameGenDeploymentPlanTests
{
    private const string Template = """
        [DLSSG]
        Optimized=1
        Preset=A

        [FrameGeneration]
        Engine=DLSS
        MaxGeneratedFrames=3

        [SuperResolution]
        Mode=Off

        [XeSSFG]
        FlowResolution=Middle
        UseHudless=Auto
        """;

    [Fact]
    public void AlternateTargetMovesRootProxyIntoAlternativesFolder()
    {
        FrameGenProxyLayout.AlternateTarget("winmm.dll").ShouldBe("alternatives/winmm.dll");
        FrameGenProxyLayout.AlternateTarget("d3d12.dll").ShouldBe("alternatives/d3d12.dll");
    }

    [Fact]
    public async Task PlanDeploysOnlyTheSelectedProxy()
    {
        var root = Path.Combine(Path.GetTempPath(), $"fgm-fg-plan-{Guid.NewGuid():N}");
        var gameDirectory = Path.Combine(root, "game");
        var payloadDirectory = Path.Combine(root, "payload");
        Directory.CreateDirectory(gameDirectory);
        Directory.CreateDirectory(Path.Combine(payloadDirectory, "alternatives"));

        try
        {
            var entries = new (string Source, string Target, string Role, bool ProxySlot)[]
            {
                ("version.dll", "version.dll", "proxy", true),
                ("alternatives/winmm.dll", "winmm.dll", "proxy", true),
                ("dlssg_sm86/backend/sm86_backend.dll", "dlssg_sm86/backend/sm86_backend.dll", "runtime", false),
            };

            var files = new List<AssetPackFile>();
            foreach (var entry in entries)
            {
                var path = Path.Combine(
                    payloadDirectory,
                    entry.Source.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllTextAsync(path, entry.Source, TestContext.Current.CancellationToken);
                files.Add(new AssetPackFile
                {
                    Source = entry.Source,
                    Target = entry.Target,
                    Role = entry.Role,
                    ProxySlot = entry.ProxySlot,
                    Sha256 = await HashAsync(path),
                    Size = new FileInfo(path).Length,
                });
            }

            var templatePath = Path.Combine(payloadDirectory, "dlssg_sm86.ini");
            await File.WriteAllTextAsync(templatePath, Template, TestContext.Current.CancellationToken);

            var pack = new AssetPack
            {
                SchemaVersion = 1,
                Id = FrameGenDeploymentService.PackId,
                Version = "0.4.2",
                DisplayName = "Test FrameGen pack",
                MinAppVersion = "2.0.0",
                GpuRoutes = ["Sm86"],
                Files = files,
                Ini = new AssetPackIni
                {
                    Target = "dlssg_sm86.ini",
                    Template = "dlssg_sm86.ini",
                    Mode = "merge",
                    TemplateSha256 = await HashAsync(templatePath),
                    TemplateSize = new FileInfo(templatePath).Length,
                    StockValues =
                    [
                        new IniSetting("DLSSG", "Optimized", "1"),
                        new IniSetting("DLSSG", "Preset", "A"),
                        new IniSetting("FrameGeneration", "Engine", "DLSS"),
                        new IniSetting("FrameGeneration", "MaxGeneratedFrames", "3"),
                        new IniSetting("SuperResolution", "Mode", "Off"),
                        new IniSetting("XeSSFG", "FlowResolution", "Middle"),
                        new IniSetting("XeSSFG", "UseHudless", "Auto"),
                    ],
                },
                Steps = [new AssetPackStep { Type = "copyFiles" }, new AssetPackStep { Type = "writeIni" }],
            };

            // One pack target must exist, otherwise inspect takes the "nothing deployed" fast path.
            await File.WriteAllTextAsync(
                Path.Combine(gameDirectory, "version.dll"),
                "deployed",
                TestContext.Current.CancellationToken);

            var engine = new CapturingEngine();
            var service = new FrameGenDeploymentService(
                new FakePacks(new PackLocation(pack, Path.Combine(root, "pack.json"), payloadDirectory)),
                engine,
                new FakeLegacy(),
                NullLogger<FrameGenDeploymentService>.Instance);

            var inspection = await service.InspectAsync(
                gameDirectory,
                FrameGenOptions.Default,
                TestContext.Current.CancellationToken);

            inspection.IsSuccess.ShouldBeTrue();

            var plan = engine.Plan.ShouldNotBeNull();
            var targets = plan.Files.Select(file => file.RelativePath).ToList();
            targets.ShouldContain("version.dll");
            targets.ShouldContain("dlssg_sm86/backend/sm86_backend.dll");
            // The spare proxy stays in the pack: upstream wants one proxy in the game folder.
            targets.ShouldNotContain("winmm.dll");
            targets.ShouldNotContain("alternatives/winmm.dll");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task<string> HashAsync(string path)
    {
        var bytes = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private sealed class FakePacks(PackLocation location) : IPackRepository
    {
        public Task<PackLocation?> FindAsync(string packId, CancellationToken cancellationToken)
        {
            return Task.FromResult<PackLocation?>(location);
        }
    }

    private sealed class FakeLegacy : ILegacyDeploymentReader
    {
        public Task<LegacyDeploymentInfo?> FindAsync(
            string targetDirectory,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<LegacyDeploymentInfo?>(null);
        }
    }

    private sealed class CapturingEngine : IDeploymentEngine
    {
        public DeploymentPlan? Plan { get; private set; }

        public Task<Result<DeploymentInspection>> InspectAsync(
            DeploymentPlan plan,
            CancellationToken cancellationToken)
        {
            Plan = plan;
            return Task.FromResult(Result<DeploymentInspection>.Ok(
                new DeploymentInspection(DeploymentState.NotDeployed, null, HasManifest: false, [])));
        }

        public Task<IReadOnlyDictionary<string, string>?> LoadOptionsAsync(
            string familyId,
            string targetDirectory,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyDictionary<string, string>?>(null);
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
            throw new NotSupportedException();
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
}
