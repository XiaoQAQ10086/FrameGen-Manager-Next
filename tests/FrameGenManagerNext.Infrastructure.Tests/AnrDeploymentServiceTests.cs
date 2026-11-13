using FrameGenManagerNext.Core.Features.Anr;
using FrameGenManagerNext.Domain.Results;
using FrameGenManagerNext.Infrastructure.Anr;
using FrameGenManagerNext.Infrastructure.Deployment;
using FrameGenManagerNext.Infrastructure.Paths;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace FrameGenManagerNext.Infrastructure.Tests;

public sealed class AnrDeploymentServiceTests : IDisposable
{
    private readonly TestDirectory _directory = new("fgm-anr-tests");

    [Fact]
    public async Task DeployUsesSelectedProxyAndRestoreReturnsOriginalFiles()
    {
        var paths = new AppPaths(_directory.GetPath("data"));
        paths.EnsureCreated();
        var backups = new BackupStore(paths, NullLogger<BackupStore>.Instance);
        var engine = new DeploymentEngine(paths, backups, NullLogger<DeploymentEngine>.Instance);
        var target = _directory.CreateDirectory("game");
        var exe = _directory.WriteFile("game/game.exe", "exe");
        _directory.WriteFile("game/winmm.dll", "original winmm");
        _directory.WriteFile("game/OptiScaler.ini", "original ini");

        var payload = _directory.CreateDirectory("anr/payload");
        _directory.WriteFile("anr/payload/game/dxgi.dll", "anr proxy");
        _directory.WriteFile("anr/payload/game/OptiScaler.ini", "[OptiScaler]");
        _directory.WriteFile(
            "anr/payload/game/OptiScaler/custom/Apex Neural Rendering FG/dlssg/nvngx_dlssg.dll",
            "anr dlssg");

        var package = new AnrPackageInfo("V.B15", payload, "anr.zip");
        var service = new AnrDeploymentService(
            new FakePackages(package),
            engine,
            NullLogger<AnrDeploymentService>.Instance);

        var deployed = await service.DeployAsync(
            target,
            exe,
            "winmm.dll",
            progress: null,
            TestContext.Current.CancellationToken);

        deployed.IsSuccess.ShouldBeTrue(deployed.Error?.ToString());
        File.ReadAllText(Path.Combine(target, "winmm.dll")).ShouldBe("anr proxy");
        File.ReadAllText(Path.Combine(target, "OptiScaler.ini")).ShouldBe("[OptiScaler]");
        File.Exists(Path.Combine(
            target,
            "OptiScaler/custom/Apex Neural Rendering FG/dlssg/nvngx_dlssg.dll")).ShouldBeTrue();

        var inspection = await service.InspectAsync(target, TestContext.Current.CancellationToken);
        inspection.State.ShouldBe(Domain.Deployment.DeploymentState.Installed);
        inspection.Version.ShouldBe("V.B15");
        inspection.Proxy.ShouldBe("winmm.dll");

        // ANR/OptiScaler write logs at runtime; restore must still end as clean.
        _directory.WriteFile(
            "game/OptiScaler/custom/Apex Neural Rendering FG/dlssg/nr_fg-test.log",
            "runtime log");

        var restored = await service.RestoreAsync(
            target,
            progress: null,
            TestContext.Current.CancellationToken);

        restored.IsSuccess.ShouldBeTrue(restored.Error?.ToString());
        File.ReadAllText(Path.Combine(target, "winmm.dll")).ShouldBe("original winmm");
        File.ReadAllText(Path.Combine(target, "OptiScaler.ini")).ShouldBe("original ini");
        File.Exists(Path.Combine(target, "OptiScaler")).ShouldBeFalse();
        var afterRestore = await service.InspectAsync(target, TestContext.Current.CancellationToken);
        afterRestore.State.ShouldBe(Domain.Deployment.DeploymentState.NotDeployed);
    }

    [Fact]
    public async Task AddonDeployWritesCustomProviderAndRestoreReturnsIni()
    {
        var paths = new AppPaths(_directory.GetPath("data"));
        paths.EnsureCreated();
        var backups = new BackupStore(paths, NullLogger<BackupStore>.Instance);
        var engine = new DeploymentEngine(paths, backups, NullLogger<DeploymentEngine>.Instance);
        var target = _directory.CreateDirectory("addon-game");
        var exe = _directory.WriteFile("addon-game/game.exe", "exe");
        _directory.CreateDirectory("addon-game/OptiScaler");
        const string original = "[FrameGen]\r\nFGInput=old\r\nFGNvngxReplacement=old-provider\r\n\r\n[Other]\r\nKeep=1\r\n";
        _directory.WriteFile("addon-game/OptiScaler.ini", original);

        var payload = _directory.CreateDirectory("anr-addon/payload");
        _directory.WriteFile(
            "anr-addon/payload/Apex Neural Rendering FG/dlssg/nvngx_dlssg.dll",
            "anr addon");
        _directory.WriteFile(
            "anr-addon/payload/nr_fg_package.json",
            "{\"version\":\"V.B15\",\"kind\":\"addon\"}");

        var package = new AnrPackageInfo(
            "V.B15",
            payload,
            "anr-addon.zip",
            AnrPackageKind.Addon);
        var service = new AnrDeploymentService(
            new FakePackages(package),
            engine,
            NullLogger<AnrDeploymentService>.Instance);

        var deployed = await service.DeployAsync(
            target,
            exe,
            proxyModule: null,
            progress: null,
            TestContext.Current.CancellationToken);

        deployed.IsSuccess.ShouldBeTrue(deployed.Error?.ToString());
        File.Exists(Path.Combine(
            target,
            "OptiScaler/custom/Apex Neural Rendering FG/dlssg/nvngx_dlssg.dll")).ShouldBeTrue();
        var ini = File.ReadAllText(Path.Combine(target, "OptiScaler.ini"));
        ini.ShouldContain("FGInput=nvngxfg");
        ini.ShouldContain("FGNvngxReplacement=Apex Neural Rendering FG");
        ini.ShouldContain("Keep=1");

        var inspection = await service.InspectAsync(target, TestContext.Current.CancellationToken);
        inspection.State.ShouldBe(Domain.Deployment.DeploymentState.Installed);

        var restored = await service.RestoreAsync(
            target,
            progress: null,
            TestContext.Current.CancellationToken);

        restored.IsSuccess.ShouldBeTrue(restored.Error?.ToString());
        File.ReadAllText(Path.Combine(target, "OptiScaler.ini")).ShouldBe(original);
        Directory.Exists(Path.Combine(
            target,
            "OptiScaler/custom/Apex Neural Rendering FG")).ShouldBeFalse();
    }

    [Fact]
    public async Task DeployingNewerPackageUpdatesInPlaceAndKeepsOriginalBackup()
    {
        var paths = new AppPaths(_directory.GetPath("data"));
        paths.EnsureCreated();
        var backups = new BackupStore(paths, NullLogger<BackupStore>.Instance);
        var engine = new DeploymentEngine(paths, backups, NullLogger<DeploymentEngine>.Instance);
        var target = _directory.CreateDirectory("update-game");
        var exe = _directory.WriteFile("update-game/game.exe", "exe");
        _directory.WriteFile("update-game/winmm.dll", "original winmm");
        _directory.WriteFile("update-game/OptiScaler.ini", "original ini");

        var firstPayload = _directory.CreateDirectory("anr-v1/payload");
        _directory.WriteFile("anr-v1/payload/game/dxgi.dll", "v1 proxy");
        _directory.WriteFile("anr-v1/payload/game/OptiScaler.ini", "v1 ini");
        var first = new AnrPackageInfo(
            "V.B15",
            firstPayload,
            "anr-v1.zip");
        var firstService = new AnrDeploymentService(
            new FakePackages(first),
            engine,
            NullLogger<AnrDeploymentService>.Instance);

        var deployed = await firstService.DeployAsync(
            target,
            exe,
            "winmm.dll",
            progress: null,
            TestContext.Current.CancellationToken);
        deployed.IsSuccess.ShouldBeTrue(deployed.Error?.ToString());

        var secondPayload = _directory.CreateDirectory("anr-v2/payload");
        _directory.WriteFile("anr-v2/payload/game/dxgi.dll", "v2 proxy");
        _directory.WriteFile("anr-v2/payload/game/OptiScaler.ini", "v2 ini");
        var second = new AnrPackageInfo("V.B16", secondPayload, "anr-v2.zip");
        var secondService = new AnrDeploymentService(
            new FakePackages(second),
            engine,
            NullLogger<AnrDeploymentService>.Instance);

        var updated = await secondService.DeployAsync(
            target,
            exe,
            "winmm.dll",
            progress: null,
            TestContext.Current.CancellationToken);

        updated.IsSuccess.ShouldBeTrue(updated.Error?.ToString());
        File.ReadAllText(Path.Combine(target, "winmm.dll")).ShouldBe("v2 proxy");
        File.ReadAllText(Path.Combine(target, "OptiScaler.ini")).ShouldBe("v2 ini");

        var inspection = await secondService.InspectAsync(target, TestContext.Current.CancellationToken);
        inspection.Version.ShouldBe("V.B16");

        var restored = await secondService.RestoreAsync(
            target,
            progress: null,
            TestContext.Current.CancellationToken);
        restored.IsSuccess.ShouldBeTrue(restored.Error?.ToString());
        File.ReadAllText(Path.Combine(target, "winmm.dll")).ShouldBe("original winmm");
        File.ReadAllText(Path.Combine(target, "OptiScaler.ini")).ShouldBe("original ini");
    }

    public void Dispose()
    {
        _directory.Dispose();
    }

    private sealed class FakePackages(AnrPackageInfo package) : IAnrPackageService
    {
        public Task<Result<AnrPackageInfo>> EnsureLatestAsync(
            IProgress<AnrDownloadProgress>? progress,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Result<AnrPackageInfo>.Ok(package));
        }

        public Task<Result<AnrPackageInfo>> ImportAsync(
            string zipPath,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Result<AnrPackageInfo>.Ok(package));
        }

        public Task<Result<AnrReleaseInfo>> CheckLatestAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(Result<AnrReleaseInfo>.Ok(new AnrReleaseInfo(package.Version)));
        }

        public Task<AnrPackageInfo?> GetCachedAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<AnrPackageInfo?>(package);
        }
    }
}
