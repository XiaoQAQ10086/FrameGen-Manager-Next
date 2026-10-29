using FrameGenManagerNext.Domain.Gpu;
using FrameGenManagerNext.Infrastructure.Gpu;
using FrameGenManagerNext.Infrastructure.Paths;
using Shouldly;

namespace FrameGenManagerNext.Infrastructure.Tests;

public sealed class GpuSpoofTests : IDisposable
{
    private readonly TestDirectory _directory = new("fgm-gpu-spoof-tests");

    [Fact]
    public void DisplayNameUsesTextAfterLastSemicolon()
    {
        GpuSpoofOperations.DisplayName("@oem19.inf,%nvidia_dev.2484%;NVIDIA GeForce RTX 3070")
            .ShouldBe("NVIDIA GeForce RTX 3070");
        GpuSpoofOperations.DisplayName("NVIDIA GeForce RTX 3070")
            .ShouldBe("NVIDIA GeForce RTX 3070");
    }

    [Fact]
    public void PresetsExposeTwoUiChoicesButKeepFullWhitelist()
    {
        GpuSpoofPresets.UiChoices.Count.ShouldBe(2);
        GpuSpoofPresets.Allowed.Count.ShouldBe(6);
        GpuSpoofPresets.IsAllowed("NVIDIA GeForce RTX 5090").ShouldBeTrue();
        GpuSpoofPresets.IsAllowed("NVIDIA GeForce RTX 4090").ShouldBeFalse();
    }

    [Fact]
    public void BackupRoundTripsThroughJson()
    {
        var paths = new AppPaths(_directory.GetPath("data"));
        paths.EnsureCreated();
        var backup = GpuSpoofBackup.Create();
        backup.Entries.Add(new GpuSpoofBackupEntry(
            @"SYSTEM\CurrentControlSet\Enum\PCI\VEN_10DE&DEV_2484\test",
            existedBefore: true,
            original: "NVIDIA GeForce RTX 3070",
            applied: "NVIDIA GeForce RTX 5090",
            driverName: "NVIDIA GeForce RTX 3070",
            savedAt: "2026-10-29T00:00:00Z"));

        GpuSpoofOperations.SaveBackup(paths, backup);

        var loaded = GpuSpoofOperations.LoadBackup(paths);
        loaded.ShouldNotBeNull();
        var entry = loaded.Entries.ShouldHaveSingleItem();
        entry.Original.ShouldBe("NVIDIA GeForce RTX 3070");
        entry.Applied.ShouldBe("NVIDIA GeForce RTX 5090");
        entry.ExistedBefore.ShouldBeTrue();
    }

    public void Dispose()
    {
        _directory.Dispose();
    }
}
