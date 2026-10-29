using FrameGenManagerNext.Cli;
using FrameGenManagerNext.Domain;
using FrameGenManagerNext.Infrastructure.Paths;

var arguments = new HashSet<string>(args, StringComparer.OrdinalIgnoreCase);

if (arguments.Contains("--version") || arguments.Contains("-v"))
{
    Console.WriteLine($"{AppInfo.ProductName} {AppInfo.Version}");
    return 0;
}

if (arguments.Contains("--paths"))
{
    var paths = AppPaths.CreateDefault();
    Console.WriteLine($"root: {paths.RootDirectory}");
    Console.WriteLine($"logs: {paths.LogsDirectory}");
    Console.WriteLine($"packs: {paths.PacksDirectory}");
    Console.WriteLine($"portable: {paths.IsPortable}");
    return 0;
}

if (args.Length >= 3 &&
    args[0].Equals("pack", StringComparison.OrdinalIgnoreCase) &&
    args[1].Equals("validate", StringComparison.OrdinalIgnoreCase))
{
    var verifyHashes = true;
    var schemaOnly = false;
    foreach (var extra in args.Skip(3))
    {
        if (extra.Equals("--no-hash", StringComparison.OrdinalIgnoreCase))
        {
            verifyHashes = false;
            continue;
        }

        if (extra.Equals("--schema-only", StringComparison.OrdinalIgnoreCase))
        {
            schemaOnly = true;
            continue;
        }

        Console.Error.WriteLine($"Unknown option: {extra}");
        return 2;
    }

    return await PackValidationCommand.RunAsync(
        args[2],
        verifyHashes,
        schemaOnly,
        CancellationToken.None);
}

Console.WriteLine($"{AppInfo.ProductName} {AppInfo.Version}");
Console.WriteLine();
Console.WriteLine("Usage:");
Console.WriteLine("  fgm-next --version");
Console.WriteLine("  fgm-next --paths");
Console.WriteLine("  fgm-next pack validate <pack-dir> [--no-hash] [--schema-only]");
return 0;
