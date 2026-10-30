using FrameGenManagerNext.Core.Features.Deployment;
using FrameGenManagerNext.Core.Features.Dlss5;
using FrameGenManagerNext.Core.Features.Framegen;
using FrameGenManagerNext.Core.Features.Library;
using FrameGenManagerNext.Core.Features.Maintenance;
using FrameGenManagerNext.Core.Features.Packs;
using FrameGenManagerNext.Core.Features.SmoothMotion;
using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Errors;
using FrameGenManagerNext.Domain.Library;
using FrameGenManagerNext.Domain.Packs;
using FrameGenManagerNext.Domain.Results;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace FrameGenManagerNext.Core.Tests;

public sealed class AssetMaintenanceServiceTests
{
    [Fact]
    public async Task DetectReportsOnlyChangedFamiliesAndDeployedGames()
    {
        var gameA = Game("a", "Game A", @"C:\Games\A");
        var gameB = Game("b", "Game B", @"C:\Games\B");
        var engine = new FakeDeploymentEngine();
        engine.Manifests[("framegen", gameA.TargetDirectory)] = Manifest(
            "framegen",
            "framegen.sm86",
            "0.4.0",
            gameA.TargetDirectory);
        engine.Manifests[("smooth-motion", gameA.TargetDirectory)] = Manifest(
            "smooth-motion",
            "smooth-motion.rtx30",
            "2.8.0",
            gameA.TargetDirectory);
        engine.Manifests[("framegen", gameB.TargetDirectory)] = Manifest(
            "framegen",
            "framegen.sm86",
            "0.4.1",
            gameB.TargetDirectory);

        var service = CreateService(
            new FakeLibrary(gameA, gameB),
            engine,
            new FakePacks(
                ("framegen.sm86", "0.4.1"),
                ("smooth-motion.rtx30", "2.8.5")));

        var report = await service.DetectAsync(TestContext.Current.CancellationToken);

        report.Groups.Count.ShouldBe(2);
        var frameGen = report.Groups.Single(group => group.Family == AssetFamily.FrameGen);
        frameGen.Games.ShouldHaveSingleItem().GameId.ShouldBe("a");

        var smoothMotion = report.Groups.Single(group => group.Family == AssetFamily.SmoothMotion);
        smoothMotion.Games.ShouldHaveSingleItem().GameId.ShouldBe("a");
        report.Groups.ShouldNotContain(group => group.Family == AssetFamily.Dlss5);
    }

    [Fact]
    public async Task SamePackVersionIsNotReported()
    {
        var game = Game("a", "Game A", @"C:\Games\A");
        var engine = new FakeDeploymentEngine();
        engine.Manifests[("framegen", game.TargetDirectory)] = Manifest(
            "framegen",
            "framegen.sm86",
            "0.4.1",
            game.TargetDirectory);

        var service = CreateService(
            new FakeLibrary(game),
            engine,
            new FakePacks(("framegen.sm86", "0.4.1")));

        var report = await service.DetectAsync(TestContext.Current.CancellationToken);

        report.HasUpdates.ShouldBeFalse();
    }

    [Fact]
    public async Task CleanRestoreClearsTheGameAndDiscardsItsBackup()
    {
        var game = Game("a", "Game A", @"C:\Games\A");
        var engine = new FakeDeploymentEngine();
        var frameGen = new FakeFrameGen
        {
            Restore = Result<RestoreReport>.Ok(new RestoreReport(1, 0, 0, [], [])),
        };
        var service = CreateService(new FakeLibrary(game), engine, new FakePacks(), frameGen);

        var report = Report(AssetFamily.FrameGen, game);
        var result = await service.ClearAsync(report, progress: null, TestContext.Current.CancellationToken);

        result.ClearedGames.ShouldBe(1);
        result.Failures.ShouldBeEmpty();
        engine.DiscardCalls.ShouldContain(("framegen", game.TargetDirectory));
    }

    [Fact]
    public async Task UncleanRestoreBecomesAFailureAndKeepsTheBackup()
    {
        var game = Game("a", "Game A", @"C:\Games\A");
        var engine = new FakeDeploymentEngine();
        var frameGen = new FakeFrameGen
        {
            Restore = Result<RestoreReport>.Ok(new RestoreReport(0, 0, 1, ["leftover.dll"], [])),
        };
        var service = CreateService(new FakeLibrary(game), engine, new FakePacks(), frameGen);

        var result = await service.ClearAsync(
            Report(AssetFamily.FrameGen, game),
            progress: null,
            TestContext.Current.CancellationToken);

        result.ClearedGames.ShouldBe(0);
        var failure = result.Failures.ShouldHaveSingleItem();
        failure.Code.ShouldBe(ErrorCode.Conflict);
        failure.Remaining.ShouldContain("leftover.dll");
        engine.DiscardCalls.ShouldBeEmpty();
    }

    [Fact]
    public async Task Dlss5UninstallLeftoversAreReportedForManualCleanup()
    {
        var game = Game("a", "Game A", @"C:\Games\A");
        var dlss5 = new FakeDlss5
        {
            Uninstall = Result<Dlss5UninstallReport>.Ok(new Dlss5UninstallReport(
                ["removed.dll"],
                ["ReShade.ini"],
                "Some files could not be removed.")),
        };
        var service = CreateService(
            new FakeLibrary(game),
            new FakeDeploymentEngine(),
            new FakePacks(),
            dlss5: dlss5);

        var result = await service.ClearAsync(
            Report(AssetFamily.Dlss5, game),
            progress: null,
            TestContext.Current.CancellationToken);

        result.ClearedGames.ShouldBe(0);
        var failure = result.Failures.ShouldHaveSingleItem();
        failure.Family.ShouldBe(AssetFamily.Dlss5);
        failure.Code.ShouldBe(ErrorCode.Conflict);
        failure.Remaining.ShouldContain("ReShade.ini");
    }

    private static AssetMaintenanceService CreateService(
        IGameLibraryService library,
        IDeploymentEngine engine,
        IPackRepository packs,
        FakeFrameGen? frameGen = null,
        FakeDlss5? dlss5 = null)
    {
        return new AssetMaintenanceService(
            library,
            engine,
            packs,
            frameGen ?? new FakeFrameGen(),
            new FakeSmoothMotion(),
            dlss5 ?? new FakeDlss5(),
            NullLogger<AssetMaintenanceService>.Instance);
    }

    private static AssetUpdateReport Report(AssetFamily family, GameEntry game)
    {
        return new AssetUpdateReport(
        [
            new AssetUpdateGroup(
                family,
                "0.4.1",
                [
                    new AssetUpdateGame(
                        game.Id,
                        game.Name,
                        game.TargetDirectory,
                        game.RenderExePath,
                        "0.4.0",
                        "0.4.1"),
                ]),
        ]);
    }

    private static DeploymentManifest Manifest(
        string family,
        string packId,
        string version,
        string target)
    {
        return new DeploymentManifest
        {
            FamilyId = family,
            PackId = packId,
            PackVersion = version,
            TargetDirectory = target,
            State = DeploymentManifestState.Installed,
        };
    }

    private static GameEntry Game(string id, string name, string directory)
    {
        return new GameEntry(
            id,
            name,
            LauncherKind.Manual,
            directory,
            Path.Combine(directory, "game.exe"),
            directory,
            GraphicsApi.Direct3D12,
            AppId: null,
            IsManual: true);
    }

    private sealed class FakeLibrary(params GameEntry[] games) : IGameLibraryService
    {
        public Task<GameLibrary> LoadAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new GameLibrary { Manual = [.. games] });
        }

        public Task<GameScanResult> ScanAsync(
            IProgress<GameScanProgress>? progress,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<GameLibrary> AddManualAsync(string exePath, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<GameLibrary> RemoveAsync(string gameId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<GameLibrary> SetIgnoredAsync(
            string gameId,
            bool ignored,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<GameLibrary> SetTargetAsync(
            string gameId,
            string exePath,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakePacks(params (string Id, string Version)[] packs) : IPackRepository
    {
        public Task<PackLocation?> FindAsync(string packId, CancellationToken cancellationToken)
        {
            var match = packs.FirstOrDefault(item =>
                string.Equals(item.Id, packId, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult<PackLocation?>(match.Id is null
                ? null
                : new PackLocation(
                    new AssetPack { Id = match.Id, Version = match.Version },
                    "pack.json",
                    "."));
        }
    }

    private sealed class FakeDeploymentEngine : IDeploymentEngine
    {
        public Dictionary<(string Family, string Target), DeploymentManifest> Manifests { get; } = [];
        public List<(string Family, string Target)> DiscardCalls { get; } = [];

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
            return Task.FromResult<IReadOnlyDictionary<string, string>?>(null);
        }

        public Task<DeploymentManifest?> LoadManifestAsync(
            string familyId,
            string targetDirectory,
            CancellationToken cancellationToken)
        {
            Manifests.TryGetValue((familyId, targetDirectory), out var manifest);
            return Task.FromResult(manifest);
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
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<Result> DiscardBackupAsync(
            string familyId,
            string targetDirectory,
            CancellationToken cancellationToken)
        {
            DiscardCalls.Add((familyId, targetDirectory));
            return Task.FromResult(Result.Ok());
        }
    }

    private sealed class FakeFrameGen : IFrameGenDeploymentService
    {
        public Result<RestoreReport> Restore { get; set; } =
            Result<RestoreReport>.Ok(new RestoreReport(0, 0, 0, [], []));

        public Task<Result<DeploymentInspection>> InspectAsync(
            string targetDirectory,
            FrameGenOptions options,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<Result<DeploymentReport>> DeployAsync(
            string targetDirectory,
            FrameGenOptions options,
            IProgress<DeploymentProgress>? progress,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<Result<RestoreReport>> RestoreAsync(
            string targetDirectory,
            IProgress<DeploymentProgress>? progress,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Restore);
        }
    }

    private sealed class FakeSmoothMotion : ISmoothMotionDeploymentService
    {
        public Task<Result<SmoothMotionInspection>> InspectAsync(
            string game,
            string targetDirectory,
            string exePath,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<Result<SmoothMotionReport>> EnableAsync(
            string game,
            string targetDirectory,
            string exePath,
            IProgress<DeploymentProgress>? progress,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<Result<SmoothMotionChangeResult>> DisableAsync(
            string game,
            string targetDirectory,
            string exePath,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<Result<SmoothMotionRestoreReport>> RestoreAsync(
            string game,
            string targetDirectory,
            string exePath,
            IProgress<DeploymentProgress>? progress,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeDlss5 : IDlss5DeploymentService
    {
        public Result<Dlss5UninstallReport> Uninstall { get; set; } =
            Result<Dlss5UninstallReport>.Ok(new Dlss5UninstallReport([], [], "clean"));

        public Task<Result<Dlss5Inspection>> InspectAsync(
            string targetDirectory,
            string exePath,
            CancellationToken cancellationToken,
            GraphicsApi? knownApi = null)
        {
            throw new NotSupportedException();
        }

        public Task<Result<Dlss5Report>> InstallAsync(
            string targetDirectory,
            string exePath,
            IProgress<DeploymentProgress>? progress,
            CancellationToken cancellationToken,
            GraphicsApi? knownApi = null)
        {
            throw new NotSupportedException();
        }

        public Task<Result<Dlss5RestoreReport>> RestoreAsync(
            string targetDirectory,
            IProgress<DeploymentProgress>? progress,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<Result<Dlss5UninstallReport>> UninstallAsync(
            string targetDirectory,
            string? exePath,
            IProgress<DeploymentProgress>? progress,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Uninstall);
        }
    }
}
