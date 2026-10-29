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

    public void Dispose()
    {
        _directory.Dispose();
    }

    private DeploymentPlan BuildPlan()
    {
        var source = _directory.WriteFile("source/runtime.dll", "new runtime");
        var content = Encoding.UTF8.GetBytes("new=1");

        return new DeploymentPlan(
            Family,
            "test.pack",
            "1.0.0",
            _target,
            [
                SourcePlan("runtime.dll", source, "runtime"),
                new DeploymentFilePlan(
                    "config.ini",
                    SourcePath: null,
                    content,
                    Sha256(content),
                    content.LongLength,
                    "ini"),
            ],
            new Dictionary<string, string>());
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
