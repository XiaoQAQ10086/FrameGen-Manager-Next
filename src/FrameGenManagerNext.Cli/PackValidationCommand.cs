using FrameGenManagerNext.Domain;
using FrameGenManagerNext.Domain.Errors;
using FrameGenManagerNext.Domain.Packs;
using FrameGenManagerNext.Infrastructure.Packs;

namespace FrameGenManagerNext.Cli;

internal static class PackValidationCommand
{
    public static async Task<int> RunAsync(
        string packDirectory,
        bool verifyHashes,
        bool schemaOnly,
        CancellationToken cancellationToken)
    {
        string fullDirectory;
        try
        {
            fullDirectory = Path.GetFullPath(packDirectory);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            Console.Error.WriteLine($"Invalid path: {exception.Message}");
            return 2;
        }

        if (!Directory.Exists(fullDirectory))
        {
            Console.Error.WriteLine($"Pack directory not found: {fullDirectory}");
            return 2;
        }

        var manifestPath = Path.Combine(fullDirectory, "pack.json");
        var load = PackJsonSerializer.LoadFromFile(manifestPath);
        if (load.IsFailure)
        {
            PrintError(load.Error!);
            return 1;
        }

        var schema = AssetPackValidator.ValidateSchema(load.Value, AppInfo.Version);
        if (!schema.IsValid)
        {
            PrintErrors(schema.Errors);
            return 1;
        }

        if (schemaOnly)
        {
            Console.WriteLine($"OK {load.Value.Id} {load.Value.Version} (schema only)");
            return 0;
        }

        var payload = await PackPayloadValidator.ValidateAsync(
            load.Value,
            fullDirectory,
            verifyHashes,
            cancellationToken);

        if (!payload.IsValid)
        {
            PrintErrors(payload.Errors);
            return 1;
        }

        Console.WriteLine(
            $"OK {load.Value.Id} {load.Value.Version} " +
            $"({load.Value.Files.Count} files, trust={load.Value.Trust})");
        return 0;
    }

    private static void PrintError(AppError error)
    {
        Console.Error.WriteLine($"{error.Code}: {error.Diagnostic ?? "pack could not be loaded"}");
    }

    private static void PrintErrors(IEnumerable<PackValidationError> errors)
    {
        foreach (var error in errors)
        {
            Console.Error.WriteLine($"{error.Code} {error.Path}: {error.Message}");
        }
    }
}
