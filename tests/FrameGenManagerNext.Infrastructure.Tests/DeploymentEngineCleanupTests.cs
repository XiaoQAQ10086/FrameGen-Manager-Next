using System.Security.Cryptography;
using System.Text;
using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Infrastructure.Deployment;
using FrameGenManagerNext.Infrastructure.Paths;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace FrameGenManagerNext.Infrastructure.Tests;

public sealed class DeploymentEngineCleanupTests : IDisposable
{
    private const string Family = "test-family";
    private readonly TestDirectory _directory = new("fgm-deploy-cleanup-tests");
    private readonly AppPaths _paths;
    private readonly BackupStore _backups;
    private readonly DeploymentEngine _engine;
    private readonly string _target;

    public DeploymentEngineCleanupTests()
    {
        _paths = new AppPaths(_directory.GetPath("data"));
        _paths.EnsureCreated();
        _target = _directory.CreateDirectory("game");
        _backups = new BackupStore(_paths, NullLogger<BackupStore>.Instance);
        _engine = new DeploymentEngine(_paths, _backups, NullLogger<DeploymentEngine>.Instance);
    }

    [Fact]
    public async Task RestoreRemovesOwnedDirectoryEvenWhenPluginWroteLogs()
    {
        var plan = BuildPlan(
            "1.0.0",
            SourcePlan(
                "dlssg_sm86/runtime.dll",
                _directory.WriteFile("source/runtime.dll", "runtime"),
                "runtime"),
            ContentPlan("dlssg_sm86.ini", "enabled=1"));

        var deployed = await _engine.DeployAsync(
            plan,
            progress: null,
            TestContext.Current.CancellationToken);
        deployed.IsSuccess.ShouldBeTrue(deployed.Error?.ToString());

        // The plugin creates its log folder while the game runs; it is not recorded
        // in the manifest and used to turn every restore into "Partial".
        _directory.WriteFile("game/dlssg_sm86/logs/dlssg_sm86.log", "log line");

        var restored = await _engine.RestoreAsync(
            Family,
            _target,
            progress: null,
            TestContext.Current.CancellationToken,
            ownedDirectories: ["dlssg_sm86"]);

        restored.IsSuccess.ShouldBeTrue(restored.Error?.ToString());
        restored.Value.Conflicts.ShouldBe(0);
        restored.Value.Leftovers.ShouldBeEmpty();
        restored.Value.Warnings.ShouldBeEmpty();
        Directory.Exists(Path.Combine(_target, "dlssg_sm86")).ShouldBeFalse();
        File.Exists(Path.Combine(_target, "dlssg_sm86.ini")).ShouldBeFalse();

        var manifest = await _engine.LoadManifestAsync(
            Family,
            _target,
            TestContext.Current.CancellationToken);
        manifest!.State.ShouldBe(DeploymentManifestState.Restored);

        var afterRestore = await _engine.InspectAsync(plan, TestContext.Current.CancellationToken);
        afterRestore.Value.State.ShouldBe(DeploymentState.NotDeployed);
    }

    [Fact]
    public async Task RestoreKeepsOwnedDirectoryWhenItContainsModifiedFiles()
    {
        var plan = BuildPlan(
            "1.0.0",
            SourcePlan(
                "dlssg_sm86/runtime.dll",
                _directory.WriteFile("source/runtime.dll", "runtime"),
                "runtime"));

        var deployed = await _engine.DeployAsync(
            plan,
            progress: null,
            TestContext.Current.CancellationToken);
        deployed.IsSuccess.ShouldBeTrue(deployed.Error?.ToString());

        _directory.WriteFile("game/dlssg_sm86/runtime.dll", "user changed this");

        var restored = await _engine.RestoreAsync(
            Family,
            _target,
            progress: null,
            TestContext.Current.CancellationToken,
            ownedDirectories: ["dlssg_sm86"]);

        restored.IsSuccess.ShouldBeTrue(restored.Error?.ToString());
        restored.Value.Conflicts.ShouldBe(1);
        File.ReadAllText(Path.Combine(_target, "dlssg_sm86/runtime.dll"))
            .ShouldBe("user changed this");
    }

    [Fact]
    public async Task RestoreDoesNotDeleteOriginalFilesInsideOwnedDirectory()
    {
        _directory.WriteFile("game/dlssg_sm86/user-original.dll", "game file");
        var plan = BuildPlan(
            "1.0.0",
            SourcePlan(
                "dlssg_sm86/user-original.dll",
                _directory.WriteFile("source/original.dll", "our runtime"),
                "runtime"));

        var deployed = await _engine.DeployAsync(
            plan,
            progress: null,
            TestContext.Current.CancellationToken);
        deployed.IsSuccess.ShouldBeTrue(deployed.Error?.ToString());

        var restored = await _engine.RestoreAsync(
            Family,
            _target,
            progress: null,
            TestContext.Current.CancellationToken,
            ownedDirectories: ["dlssg_sm86"]);

        restored.IsSuccess.ShouldBeTrue(restored.Error?.ToString());
        File.ReadAllText(Path.Combine(_target, "dlssg_sm86/user-original.dll"))
            .ShouldBe("game file");
    }

    [Fact]
    public async Task CleanDeletesMatchingFilesAndKeepsModifiedOnes()
    {
        var runtime = _directory.WriteFile("source/runtime.dll", "runtime");
        var plan = BuildPlan(
            "1.0.0",
            SourcePlan("dlssg_sm86/runtime.dll", runtime, "runtime"),
            ContentPlan("dlssg_sm86.ini", "enabled=1"),
            ContentPlan("version.dll", "proxy"));

        // Simulate an old-tool deployment: same files, no manifest.
        _directory.WriteFile("game/dlssg_sm86/runtime.dll", "runtime");
        _directory.WriteFile("game/dlssg_sm86/logs/dlssg_sm86.log", "log line");
        _directory.WriteFile("game/dlssg_sm86.ini", "enabled=0");
        _directory.WriteFile("game/version.dll", "user replaced proxy");

        var candidates = plan.Files
            .Where(file => file.RelativePath != "dlssg_sm86.ini")
            .Select(file => new CleanupCandidate(file.RelativePath, file.Sha256))
            .ToArray();

        var cleaned = await _engine.CleanAsync(
            Family,
            _target,
            candidates,
            ["dlssg_sm86"],
            "dlssg_sm86.ini",
            progress: null,
            TestContext.Current.CancellationToken);

        cleaned.IsSuccess.ShouldBeTrue(cleaned.Error?.ToString());
        cleaned.Value.RemovedFiles.ShouldBe(2); // runtime.dll + INI
        cleaned.Value.Conflicts.ShouldBe(1); // modified version.dll
        cleaned.Value.Leftovers.ShouldContain("version.dll");
        Directory.Exists(Path.Combine(_target, "dlssg_sm86")).ShouldBeFalse();
        File.Exists(Path.Combine(_target, "dlssg_sm86.ini")).ShouldBeFalse();
        File.ReadAllText(Path.Combine(_target, "version.dll")).ShouldBe("user replaced proxy");
    }

    [Fact]
    public async Task CleanAcceptsAnyRecordedHashForTheSamePath()
    {
        var oldContent = Encoding.UTF8.GetBytes("old proxy");
        var newContent = Encoding.UTF8.GetBytes("new proxy");
        _directory.WriteFile("game/version.dll", "old proxy");

        var cleaned = await _engine.CleanAsync(
            Family,
            _target,
            [
                new CleanupCandidate("version.dll", Sha256(newContent)),
                new CleanupCandidate("version.dll", Sha256(oldContent)),
            ],
            [],
            iniRelativePath: null,
            progress: null,
            TestContext.Current.CancellationToken);

        cleaned.IsSuccess.ShouldBeTrue(cleaned.Error?.ToString());
        cleaned.Value.RemovedFiles.ShouldBe(1);
        cleaned.Value.Conflicts.ShouldBe(0);
        File.Exists(Path.Combine(_target, "version.dll")).ShouldBeFalse();
    }

    [Fact]
    public async Task CleanWithoutAnyMatchingFileFails()
    {
        _directory.WriteFile("game/version.dll", "user replaced proxy");

        var cleaned = await _engine.CleanAsync(
            Family,
            _target,
            [new CleanupCandidate("version.dll", new string('a', 64))],
            [],
            iniRelativePath: null,
            progress: null,
            TestContext.Current.CancellationToken);

        cleaned.IsFailure.ShouldBeTrue();
        cleaned.Error!.Code.ShouldBe(Domain.Errors.ErrorCode.NothingToUninstall);
        File.Exists(Path.Combine(_target, "version.dll")).ShouldBeTrue();
    }

    [Fact]
    public async Task CleanRefusesWhenADeploymentRecordExists()
    {
        var plan = BuildPlan();
        var deployed = await _engine.DeployAsync(
            plan,
            progress: null,
            TestContext.Current.CancellationToken);
        deployed.IsSuccess.ShouldBeTrue(deployed.Error?.ToString());

        var cleaned = await _engine.CleanAsync(
            Family,
            _target,
            [new CleanupCandidate("runtime.dll", plan.Files[0].Sha256)],
            [],
            iniRelativePath: null,
            progress: null,
            TestContext.Current.CancellationToken);

        cleaned.IsFailure.ShouldBeTrue();
        cleaned.Error!.Code.ShouldBe(Domain.Errors.ErrorCode.Conflict);
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
        return new DeploymentPlan(
            Family,
            "test.pack",
            version,
            _target,
            files,
            new Dictionary<string, string>());
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
