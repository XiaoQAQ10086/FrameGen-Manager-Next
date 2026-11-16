using FrameGenManagerNext.Core.Features.Dlss5;
using Shouldly;

namespace FrameGenManagerNext.Core.Tests;

public sealed class Dlss5ModuleCleanupPolicyTests
{
    [Theory]
    [InlineData(false, false, "abc", "abc", true)]
    [InlineData(false, false, "abc", "xyz", false)]
    [InlineData(false, false, null, "abc", false)]
    [InlineData(false, false, "abc", null, false)]
    [InlineData(true, false, "abc", "abc", false)]
    [InlineData(false, true, "abc", "abc", false)]
    public void OnlyDeletesVerifiedUnrecordedModules(
        bool existedBefore,
        bool recordedInManifest,
        string? currentHash,
        string? expectedHash,
        bool expected)
    {
        var canDelete = Dlss5ModuleCleanupPolicy.CanDelete(
            existedBefore,
            recordedInManifest,
            currentHash,
            expectedHash);

        canDelete.ShouldBe(expected);
    }
}
