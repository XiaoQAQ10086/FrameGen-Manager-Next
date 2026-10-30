using System.Security.Cryptography;
using System.Text;
using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Infrastructure.Deployment;
using FrameGenManagerNext.Infrastructure.Paths;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace FrameGenManagerNext.Infrastructure.Tests;

public sealed class DeploymentEngineTests : IDisposable
{
    private const string Family = "test-family";
    private readonly TestDirectory _directory = new("fgm-deploy-tests");
    private readonly AppPaths _paths;
    private readonly BackupStore _backups;
    private readonly DeploymentEngine _engine;
    private readonly string _target;

    public DeploymentEngineTests()
    {
        _paths = new AppPaths(_directory.GetPath("data"));
        _paths.EnsureCreated();
        _target = _directory.CreateDirectory("game");
        _backups = new BackupStore(_paths, NullLogger<BackupStore>.Instance);
        _engine = new DeploymentEngine(_paths, _backups, NullLogger<DeploymentEngine>.Instance);
    }

    [Fact]
    public async Task DeployInspectAndRestoreRoundTrip()
    {
        _directory.WriteFile("game/runtime.dll", "old runtime");
        _directory.WriteFile("game/config.ini", "old=1");
        var plan = BuildPlan();

        var deployed = await _engine.DeployAsync(
            plan,
            progress: null,
            TestContext.Current.CancellationToken);

        deployed.IsSuccess.ShouldBeTrue(deployed.Error?.ToString());
        deployed.Value.State.ShouldBe(DeploymentState.Installed);
        deployed.Value.FilesWritten.ShouldBe(2);
        deployed.Value.FilesBackedUp.ShouldBe(2);
        File.ReadAllText(Path.Combine(_target, "runtime.dll")).ShouldBe("new runtime");
        File.ReadAllText(Path.Combine(_target, "config.ini")).ShouldBe("new=1");

        var inspected = await _engine.InspectAsync(plan, TestContext.Current.CancellationToken);
        inspected.IsSuccess.ShouldBeTrue(inspected.Error?.ToString());
        inspected.Value.State.ShouldBe(DeploymentState.Installed);

        var restored = await _engine.RestoreAsync(
            Family,
            _target,
            progress: null,
            TestContext.Current.CancellationToken);

        restored.IsSuccess.ShouldBeTrue(restored.Error?.ToString());
        restored.Value.Conflicts.ShouldBe(0);
        restored.Value.RestoredFiles.ShouldBe(2);
        File.ReadAllText(Path.Combine(_target, "runtime.dll")).ShouldBe("old runtime");
        File.ReadAllText(Path.Combine(_target, "config.ini")).ShouldBe("old=1");

        var afterRestore = await _engine.InspectAsync(plan, TestContext.Current.CancellationToken);
        afterRestore.Value.State.ShouldBe(DeploymentState.NotDeployed);
    }

    [Fact]
    public async Task ModifiedFileIsReportedAsConflict()
    {
        var plan = BuildPlan();
        var deployed = await _engine.DeployAsync(
            plan,
            progress: null,
            TestContext.Current.CancellationToken);
        deployed.IsSuccess.ShouldBeTrue(deployed.Error?.ToString());

        _directory.WriteFile("game/runtime.dll", "user changed this");

        var inspected = await _engine.InspectAsync(plan, TestContext.Current.CancellationToken);
        inspected.Value.State.ShouldBe(DeploymentState.Conflict);

        var restored = await _engine.RestoreAsync(
            Family,
            _target,
            progress: null,
            TestContext.Current.CancellationToken);
        restored.IsSuccess.ShouldBeTrue(restored.Error?.ToString());
        restored.Value.Conflicts.ShouldBe(1);
        File.ReadAllText(Path.Combine(_target, "runtime.dll")).ShouldBe("user changed this");
    }

    [Fact]
    public async Task MissingFileIsReportedAsPartial()
    {
        var plan = BuildPlan();
        await _engine.DeployAsync(plan, progress: null, TestContext.Current.CancellationToken);

        File.Delete(Path.Combine(_target, "runtime.dll"));

        var inspected = await _engine.InspectAsync(plan, TestContext.Current.CancellationToken);
        inspected.Value.State.ShouldBe(DeploymentState.Partial);
    }

    [Fact]
    public async Task LockedTargetIsRejected()
    {
        _directory.WriteFile("game/runtime.dll", "old runtime");
        var plan = BuildPlan();

        await using var lockStream = new FileStream(
            Path.Combine(_target, "runtime.dll"),
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None);

        var deployed = await _engine.DeployAsync(
            plan,
            progress: null,
            TestContext.Current.CancellationToken);

        deployed.IsFailure.ShouldBeTrue();
        deployed.Error!.Code.ShouldBe(Domain.Errors.ErrorCode.FileLocked);
    }

    [Fact]
    public async Task FilesWithoutManifestAreRecognized()
    {
        var plan = BuildPlan();
        _directory.WriteFile("game/runtime.dll", "new runtime");

        var inspected = await _engine.InspectAsync(plan, TestContext.Current.CancellationToken);

        inspected.Value.State.ShouldBe(DeploymentState.InstalledWithoutManifest);
    }

    [Fact]
    public async Task RestoreWithoutManifestFails()
    {
        var restored = await _engine.RestoreAsync(
            Family,
            _target,
            progress: null,
            TestContext.Current.CancellationToken);

        restored.IsFailure.ShouldBeTrue();
        restored.Error!.Code.ShouldBe(Domain.Errors.ErrorCode.ManifestMissing);
    }

    [Fact]
    public async Task DiscardBackupRemovesManifestAndBackupFiles()
    {
        var plan = BuildPlan();
        var deployed = await _engine.DeployAsync(
            plan,
            progress: null,
            TestContext.Current.CancellationToken);
        deployed.IsSuccess.ShouldBeTrue(deployed.Error?.ToString());

        var backupDirectory = _backups.GetBackupDirectory(Family, _target);
        Directory.Exists(backupDirectory).ShouldBeTrue();

        var discarded = await _engine.DiscardBackupAsync(
            Family,
            _target,
            TestContext.Current.CancellationToken);

        discarded.IsSuccess.ShouldBeTrue(discarded.Error?.ToString());
        Directory.Exists(backupDirectory).ShouldBeFalse();

        var inspected = await _engine.InspectAsync(plan, TestContext.Current.CancellationToken);
        inspected.Value.HasManifest.ShouldBeFalse();
    }

    [Fact]
    public async Task UpgradingPackKeepsOriginalBackupsAndRemovesStaleFiles()
    {
        _directory.WriteFile("game/runtime.dll", "original runtime");
        _directory.WriteFile("game/config.ini", "original=1");

        var version1 = BuildPlan(
            "1.0.0",
            SourcePlan("runtime.dll", _directory.WriteFile("source/v1/runtime.dll", "v1 runtime"), "runtime"),
            ContentPlan("config.ini", "v1=1"),
            SourcePlan("stale.dll", _directory.WriteFile("source/v1/stale.dll", "v1 stale"), "runtime"));

        var deployed = await _engine.DeployAsync(
            version1,
            progress: null,
            TestContext.Current.CancellationToken);
        deployed.IsSuccess.ShouldBeTrue(deployed.Error?.ToString());

        var version2 = BuildPlan(
            "2.0.0",
            SourcePlan("runtime.dll", _directory.WriteFile("source/v2/runtime.dll", "v2 runtime"), "runtime"),
            ContentPlan("config.ini", "v2=1"),
            SourcePlan("added.dll", _directory.WriteFile("source/v2/added.dll", "v2 added"), "runtime"));

        var inspected = await _engine.InspectAsync(version2, TestContext.Current.CancellationToken);
        inspected.IsSuccess.ShouldBeTrue(inspected.Error?.ToString());
        inspected.Value.State.ShouldBe(DeploymentState.Outdated);
        inspected.Value.PackVersion.ShouldBe("1.0.0");

        var upgraded = await _engine.DeployAsync(
            version2,
            progress: null,
            TestContext.Current.CancellationToken);
        upgraded.IsSuccess.ShouldBeTrue(upgraded.Error?.ToString());
        upgraded.Value.State.ShouldBe(DeploymentState.Installed);
        File.ReadAllText(Path.Combine(_target, "runtime.dll")).ShouldBe("v2 runtime");
        File.ReadAllText(Path.Combine(_target, "config.ini")).ShouldBe("v2=1");
        File.ReadAllText(Path.Combine(_target, "added.dll")).ShouldBe("v2 added");
        File.Exists(Path.Combine(_target, "stale.dll")).ShouldBeFalse();

        var restored = await _engine.RestoreAsync(
            Family,
            _target,
            progress: null,
            TestContext.Current.CancellationToken);
        restored.IsSuccess.ShouldBeTrue(restored.Error?.ToString());
        restored.Value.Conflicts.ShouldBe(0);
        File.ReadAllText(Path.Combine(_target, "runtime.dll")).ShouldBe("original runtime");
        File.ReadAllText(Path.Combine(_target, "config.ini")).ShouldBe("original=1");
        File.Exists(Path.Combine(_target, "added.dll")).ShouldBeFalse();
    }

    [Fact]
    public async Task InspectionAndOptionLookupReturnDeployedOptions()
    {
        var plan = BuildPlan(
            "1.0.0",
            new Dictionary<string, string> { ["preset"] = "B", ["optimized"] = "2" },
            ContentPlan("config.ini", "preset=B"));

        var deployed = await _engine.DeployAsync(
            plan,
            progress: null,
            TestContext.Current.CancellationToken);
        deployed.IsSuccess.ShouldBeTrue(deployed.Error?.ToString());

        var inspected = await _engine.InspectAsync(plan, TestContext.Current.CancellationToken);
        inspected.IsSuccess.ShouldBeTrue(inspected.Error?.ToString());
        inspected.Value.Options.ShouldNotBeNull()["preset"].ShouldBe("B");

        var stored = await _engine.LoadOptionsAsync(
            Family,
            _target,
            TestContext.Current.CancellationToken);
        stored.ShouldNotBeNull()["optimized"].ShouldBe("2");
    }

    public void Dispose()
    {
        _directory.Dispose();
    }

    private DeploymentPlan BuildPlan()
    {
        var source = _directory.WriteFile("source/runtime.dll", "new runtime");
        return BuildPlan(
            "1.0.0",
            SourcePlan("runtime.dll", source, "runtime"),
            ContentPlan("config.ini", "new=1"));
    }

    private DeploymentPlan BuildPlan(string version, params DeploymentFilePlan[] files)
    {
        return BuildPlan(version, new Dictionary<string, string>(), files);
    }

    private DeploymentPlan BuildPlan(
        string version,
        IReadOnlyDictionary<string, string> options,
        params DeploymentFilePlan[] files)
    {
        return new DeploymentPlan(
            Family,
            "test.pack",
            version,
            _target,
            files,
            options);
    }

    private static DeploymentFilePlan ContentPlan(string relativePath, string text)
    {
        var content = Encoding.UTF8.GetBytes(text);
        return new DeploymentFilePlan(
            relativePath,
            SourcePath: null,
            content,
            Sha256(content),
            content.LongLength,
            "ini");
    }

    private static DeploymentFilePlan SourcePlan(string relativePath, string sourcePath, string role)
    {
        var bytes = File.ReadAllBytes(sourcePath);
        return new DeploymentFilePlan(
            relativePath,
            sourcePath,
            Content: null,
            Sha256(bytes),
            bytes.LongLength,
            role);
    }

    private static string Sha256(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
