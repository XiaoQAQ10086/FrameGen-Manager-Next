using System.IO.Compression;
using FrameGenManagerNext.Core.Features.Anr;
using FrameGenManagerNext.Infrastructure.Anr;
using FrameGenManagerNext.Infrastructure.Paths;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace FrameGenManagerNext.Infrastructure.Tests;

public sealed class AnrPackageServiceTests : IDisposable
{
    private readonly TestDirectory _directory = new("fgm-anr-package-tests");

    [Fact]
    public async Task ImportAcceptsFullAndAddonLayouts()
    {
        var paths = new AppPaths(_directory.GetPath("data"));
        paths.EnsureCreated();
        using var service = new AnrPackageService(
            paths,
            NullLogger<AnrPackageService>.Instance);

        var fullSource = _directory.CreateDirectory("full/game");
        _directory.WriteFile("full/game/dxgi.dll", "proxy");
        _directory.WriteFile("full/game/OptiScaler.ini", "[OptiScaler]");
        var fullZip = _directory.GetPath("anr-fg-V.B15-full.zip");
        ZipFile.CreateFromDirectory(_directory.GetPath("full"), fullZip);

        var full = await service.ImportAsync(fullZip, TestContext.Current.CancellationToken);
        full.IsSuccess.ShouldBeTrue(full.Error?.ToString());
        full.Value.Kind.ShouldBe(AnrPackageKind.Full);

        var addonSource = _directory.CreateDirectory("addon");
        _directory.WriteFile(
            "addon/Apex Neural Rendering FG/dlssg/nvngx_dlssg.dll",
            "addon");
        _directory.WriteFile(
            "addon/nr_fg_package.json",
            "{\"version\":\"V.B16\",\"kind\":\"addon\"}");
        var addonZip = _directory.GetPath("anr-fg-V.B16-addon.zip");
        ZipFile.CreateFromDirectory(_directory.GetPath("addon"), addonZip);

        var addon = await service.ImportAsync(addonZip, TestContext.Current.CancellationToken);
        addon.IsSuccess.ShouldBeTrue(addon.Error?.ToString());
        addon.Value.Kind.ShouldBe(AnrPackageKind.Addon);
        addon.Value.Version.ShouldBe("V.B16");

        var cached = await service.GetCachedAsync(TestContext.Current.CancellationToken);
        cached.ShouldNotBeNull();
        cached.Kind.ShouldBe(AnrPackageKind.Addon);
    }

    [Fact]
    public async Task ImportRejectsZipWithoutAnrLayout()
    {
        var paths = new AppPaths(_directory.GetPath("data"));
        paths.EnsureCreated();
        using var service = new AnrPackageService(
            paths,
            NullLogger<AnrPackageService>.Instance);
        _directory.WriteFile("unknown/README.txt", "not an ANR package");
        var zip = _directory.GetPath("unknown.zip");
        ZipFile.CreateFromDirectory(_directory.GetPath("unknown"), zip);

        var result = await service.ImportAsync(zip, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe(Domain.Errors.ErrorCode.PackInvalid);
    }

    public void Dispose()
    {
        _directory.Dispose();
    }
}
