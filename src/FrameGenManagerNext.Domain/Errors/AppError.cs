namespace FrameGenManagerNext.Domain.Errors;

/// <summary>
/// Structured error returned by core services. It never contains user-facing localized text.
/// </summary>
public sealed class AppError
{
    private static readonly IReadOnlyDictionary<string, string> EmptyContext =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public AppError(ErrorCode code, string? diagnostic = null)
        : this(code, EmptyContext, diagnostic)
    {
    }

    public AppError(
        ErrorCode code,
        IReadOnlyDictionary<string, string> context,
        string? diagnostic = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        Code = code;
        Context = new Dictionary<string, string>(context, StringComparer.Ordinal);
        Diagnostic = diagnostic;
    }

    public ErrorCode Code { get; }

    public IReadOnlyDictionary<string, string> Context { get; }

    /// <summary>
    /// English technical detail for logs and diagnostics. Not shown to users directly.
    /// </summary>
    public string? Diagnostic { get; }

    public static AppError Create(ErrorCode code, params (string Key, string Value)[] context)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in context)
        {
            values[key] = value;
        }

        return new AppError(code, values);
    }

    public AppError WithContext(string key, string value)
    {
        var values = new Dictionary<string, string>(Context, StringComparer.Ordinal)
        {
            [key] = value,
        };

        return new AppError(Code, values, Diagnostic);
    }

    public override string ToString()
    {
        return Diagnostic is null ? Code.ToString() : $"{Code}: {Diagnostic}";
    }
}
