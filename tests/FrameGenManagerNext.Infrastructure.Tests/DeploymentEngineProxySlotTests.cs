using System.Security.Cryptography;
using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Infrastructure.Deployment;
using FrameGenManagerNext.Infrastructure.Paths;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace FrameGenManagerNext.Infrastructure.Tests;

/// <summary>
/// Upstream keeps exactly one proxy next to the game EXE: to switch, the old proxy is deleted
/// before the new one is copied, because the proxy the process loads first decides which version
/// runs. These tests lock that order in.
/// </summary>
public sealed class DeploymentEngineProxySlotTests : IDisposable
{
    private const string Family = "test-family";
    private readonly TestDirectory _directory = new("fgm-proxy-tests");
    private readonly DeploymentEngine _engine;
    private readonly string _target;

    public DeploymentEngineProxySlotTests()
    {
        var paths = new AppPaths(_directory.GetPath("data"));
        paths.EnsureCreated();
        _target = _directory.CreateDirectory("game");
        var backups = new BackupStore(paths, NullLogger<BackupStore>.Instance);
        _engine = new DeploymentEngine(paths, backups, NullLogger<DeploymentEngine>.Instance);
    }

    [Fact]
    public async Task SwitchingProxyRemovesOldProxyBeforeWritingNewOne()
    {
        var first = Plan(
            "1.0.0",
            Source("winmm.dll", _directory.WriteFile("source/v1/winmm.dll", "v1 proxy"), "proxy"));

        var deployed = await _engine.DeployAsync(
            first,
            progress: null,
            TestContext.Current.CancellationToken);
        deployed.IsSuccess.ShouldBeTrue(deployed.Error?.ToString());
        File.Exists(Path.Combine(_target, "winmm.dll")).ShouldBeTrue();

        var oldProxyExistedWhileWritingNew = true;
        var progress = new InlineProgress(update =>
        {
            if (string.Equals(update.Phase, "writing", StringComparison.Ordinal) &&
                string.Equals(update.Subject, "version.dll", StringComparison.Ordinal))
            {
                oldProxyExistedWhileWritingNew = File.Exists(Path.Combine(_target, "winmm.dll"));
            }
        });

        var second = Plan(
            "2.0.0",
            Source("version.dll", _directory.WriteFile("source/v2/version.dll", "v2 proxy"), "proxy"));

        var switched = await _engine.DeployAsync(
            second,
            progress,
            TestContext.Current.CancellationToken);
        switched.IsSuccess.ShouldBeTrue(switched.Error?.ToString());

        oldProxyExistedWhileWritingNew.ShouldBeFalse();
        File.Exists(Path.Combine(_target, "winmm.dll")).ShouldBeFalse();
        File.ReadAllText(Path.Combine(_target, "version.dll")).ShouldBe("v2 proxy");
    }

    [Fact]
    public async Task SwitchingProxyPutsGameOwnProxyFileBack()
    {
        _directory.WriteFile("game/winmm.dll", "game own winmm");

        var first = Plan(
            "1.0.0",
            Source("winmm.dll", _directory.WriteFile("source/v1/winmm.dll", "v1 proxy"), "proxy"));

        var deployed = await _engine.DeployAsync(
            first,
            progress: null,
            TestContext.Current.CancellationToken);
        deployed.IsSuccess.ShouldBeTrue(deployed.Error?.ToString());
        File.ReadAllText(Path.Combine(_target, "winmm.dll")).ShouldBe("v1 proxy");

        var second = Plan(
            "2.0.0",
            Source("version.dll", _directory.WriteFile("source/v2/version.dll", "v2 proxy"), "proxy"));

        var switched = await _engine.DeployAsync(
            second,
            progress: null,
            TestContext.Current.CancellationToken);
        switched.IsSuccess.ShouldBeTrue(switched.Error?.ToString());

        File.ReadAllText(Path.Combine(_target, "winmm.dll")).ShouldBe("game own winmm");
        File.ReadAllText(Path.Combine(_target, "version.dll")).ShouldBe("v2 proxy");
    }

    public void Dispose()
    {
        _directory.Dispose();
    }

    private DeploymentPlan Plan(string version, params DeploymentFilePlan[] files)
    {
        return new DeploymentPlan(
            Family,
            "test.pack",
            version,
            _target,
            files,
            new Dictionary<string, string>());
    }

    private static DeploymentFilePlan Source(string relativePath, string sourcePath, string role)
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

    private sealed class InlineProgress(Action<DeploymentProgress> callback) : IProgress<DeploymentProgress>
    {
        public void Report(DeploymentProgress value)
        {
            callback(value);
        }
    }
}
