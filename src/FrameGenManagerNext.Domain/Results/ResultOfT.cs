using FrameGenManagerNext.Domain.Errors;

namespace FrameGenManagerNext.Domain.Results;

/// <summary>
/// Result of an operation that returns a value on success.
/// </summary>
public sealed class Result<T>
{
    private readonly T? _value;

    private Result(bool isSuccess, T? value, AppError? error)
    {
        IsSuccess = isSuccess;
        _value = value;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public AppError? Error { get; }

    /// <summary>
    /// Gets the value. Throws when the result is a failure; use <see cref="TryGetValue"/> when the outcome is unknown.
    /// </summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot read Value from a failed result ({Error?.Code}).");

    public static Result<T> Ok(T value)
    {
        return new Result<T>(isSuccess: true, value, error: null);
    }

    public static Result<T> Fail(AppError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(isSuccess: false, value: default, error);
    }

    public bool TryGetValue(out T value)
    {
        if (IsSuccess)
        {
            value = _value!;
            return true;
        }

        value = default!;
        return false;
    }

    public T GetValueOrDefault(T fallback)
    {
        return IsSuccess ? _value! : fallback;
    }
}
