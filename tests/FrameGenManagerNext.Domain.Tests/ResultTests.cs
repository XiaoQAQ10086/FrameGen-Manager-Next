using FrameGenManagerNext.Domain.Errors;
using FrameGenManagerNext.Domain.Results;
using Shouldly;

namespace FrameGenManagerNext.Domain.Tests;

public sealed class ResultTests
{
    [Fact]
    public void OkResultHasNoError()
    {
        var result = Result.Ok();

        result.IsSuccess.ShouldBeTrue();
        result.IsFailure.ShouldBeFalse();
        result.Error.ShouldBeNull();
    }

    [Fact]
    public void FailedResultCarriesError()
    {
        var error = new AppError(ErrorCode.InvalidPath, "path must stay under root");

        var result = Result.Fail(error);

        result.IsSuccess.ShouldBeFalse();
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeSameAs(error);
    }

    [Fact]
    public void GenericOkReturnsValue()
    {
        var result = Result<int>.Ok(42);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
        result.TryGetValue(out var value).ShouldBeTrue();
        value.ShouldBe(42);
    }

    [Fact]
    public void GenericFailureThrowsWhenReadingValue()
    {
        var result = Result<int>.Fail(new AppError(ErrorCode.PackInvalid, "missing id"));

        result.IsFailure.ShouldBeTrue();
        result.TryGetValue(out _).ShouldBeFalse();
        result.GetValueOrDefault(7).ShouldBe(7);
        Should.Throw<InvalidOperationException>(() => _ = result.Value);
    }

    [Fact]
    public void AppErrorCopiesContextAndSupportsWithContext()
    {
        var original = new AppError(
            ErrorCode.PackInvalid,
            new Dictionary<string, string> { ["pack"] = "framegen.sm86" },
            "schema version is not supported");

        original.Context["pack"].ShouldBe("framegen.sm86");
        original.ToString().ShouldContain("PackInvalid");

        var extended = original.WithContext("field", "schemaVersion");

        extended.Context["pack"].ShouldBe("framegen.sm86");
        extended.Context["field"].ShouldBe("schemaVersion");
        original.Context.ContainsKey("field").ShouldBeFalse();
    }

    [Fact]
    public void AppErrorCreateBuildsContext()
    {
        var error = AppError.Create(ErrorCode.DriverTooOld, ("required", "580"), ("actual", "560"));

        error.Context["required"].ShouldBe("580");
        error.Context["actual"].ShouldBe("560");
    }
}
