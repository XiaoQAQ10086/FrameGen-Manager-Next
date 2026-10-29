using FrameGenManagerNext.Domain.Errors;
using FrameGenManagerNext.Domain.Safety;
using Shouldly;

namespace FrameGenManagerNext.Domain.Tests;

public sealed class SafePathTests
{
    [Theory]
    [InlineData("version.dll")]
    [InlineData("dlssg_sm86/runtime/nvngx_dlssg.dll")]
    [InlineData("dlssg_sm86\\runtime\\nvngx_dlssg.dll")]
    [InlineData("reshade-shaders/Shaders/Effect.fx")]
    [InlineData("游戏/配置.ini")]
    [InlineData("COM10.txt")]
    public void NormalizeAcceptsSafeRelativePaths(string path)
    {
        var result = SafePath.NormalizeRelativePath(path);

        result.IsSuccess.ShouldBeTrue(result.Error?.ToString());
        result.Value.ShouldNotContain("\\");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("../evil.dll")]
    [InlineData("a/../../b")]
    [InlineData("a/..")]
    [InlineData("a/./b")]
    [InlineData("C:\\Windows\\x.dll")]
    [InlineData("C:/Windows/x.dll")]
    [InlineData("\\\\server\\share\\x.dll")]
    [InlineData("/etc/passwd")]
    [InlineData("\\Windows\\x.dll")]
    [InlineData("C:foo")]
    [InlineData("file.txt:stream")]
    [InlineData("CON")]
    [InlineData("con.txt")]
    [InlineData("aux.dll")]
    [InlineData("COM1.log")]
    [InlineData("LPT9")]
    [InlineData("conin$.txt")]
    [InlineData("foo.")]
    [InlineData("foo ")]
    [InlineData("foo /bar")]
    [InlineData("a//b")]
    [InlineData("a<b")]
    [InlineData("a|b")]
    [InlineData("a*b")]
    [InlineData("a?b")]
    [InlineData("a\"b")]
    [InlineData("line\nbreak")]
    [InlineData("a\tb")]
    [InlineData("...")]
    public void NormalizeRejectsUnsafePaths(string? path)
    {
        var result = SafePath.NormalizeRelativePath(path);

        result.IsFailure.ShouldBeTrue(path ?? "<null>");
    }

    [Fact]
    public void CombineUnderRootBuildsPathBelowRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "FrameGenSafePath");

        var result = SafePath.CombineUnderRoot(root, "bin/win64/x.dll");

        result.IsSuccess.ShouldBeTrue(result.Error?.ToString());
        result.Value.ShouldBe(Path.Combine(Path.GetFullPath(root), "bin", "win64", "x.dll"));
    }

    [Fact]
    public void CombineUnderRootRejectsTraversal()
    {
        var root = Path.Combine(Path.GetTempPath(), "FrameGenSafePath");

        var result = SafePath.CombineUnderRoot(root, "../x.dll");

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe(ErrorCode.UnsafePath);
    }

    [Fact]
    public void CombineUnderRootRejectsRelativeRoot()
    {
        var result = SafePath.CombineUnderRoot("relative-root", "x.dll");

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe(ErrorCode.InvalidPath);
    }

    [Fact]
    public void IsUnderRootUsesSegmentBoundary()
    {
        SafePath.IsUnderRoot("C:\\Games", "C:\\Games\\a.dll").ShouldBeTrue();
        SafePath.IsUnderRoot("C:\\Games", "C:\\Games").ShouldBeTrue();
        SafePath.IsUnderRoot("C:\\Games", "C:\\Games2\\a.dll").ShouldBeFalse();
        SafePath.IsUnderRoot("C:\\", "C:\\Windows\\a.dll").ShouldBeTrue();
    }

    [Fact]
    public void IsProtectedSystemDirectoryRejectsWindowsAndDriveRoots()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        if (!string.IsNullOrWhiteSpace(windows))
        {
            SafePath.IsProtectedSystemDirectory(windows).ShouldBeTrue();
        }

        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            SafePath.IsProtectedSystemDirectory(programFiles).ShouldBeTrue();
        }

        SafePath.IsProtectedSystemDirectory(Path.GetPathRoot(Path.GetTempPath())!).ShouldBeTrue();
        SafePath.IsProtectedSystemDirectory(Path.GetTempPath()).ShouldBeFalse();
    }
}
