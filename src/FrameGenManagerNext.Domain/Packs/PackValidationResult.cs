namespace FrameGenManagerNext.Domain.Packs;

public sealed class PackValidationResult
{
    public PackValidationResult(IEnumerable<PackValidationError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        Errors = [.. errors];
    }

    public IReadOnlyList<PackValidationError> Errors { get; }

    public bool IsValid => Errors.Count == 0;

    public static PackValidationResult Ok()
    {
        return new PackValidationResult([]);
    }
}
