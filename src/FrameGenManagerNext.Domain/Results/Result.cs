using FrameGenManagerNext.Domain.Errors;

namespace FrameGenManagerNext.Domain.Results;

/// <summary>
/// Result of an operation that does not return a value.
/// </summary>
public sealed class Result
{
    private static readonly Result Success = new(isSuccess: true, error: null);

    private Result(bool isSuccess, AppError? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public AppError? Error { get; }

    public static Result Ok()
    {
        return Success;
    }

    public static Result Fail(AppError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result(isSuccess: false, error);
    }
}
