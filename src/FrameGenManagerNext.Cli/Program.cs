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

if (args.Length >= 1 && args[0].Equals("gpu", StringComparison.OrdinalIgnoreCase))
{
    using var services = CliServices.Create();
    return await GpuCommand.RunAsync(services, CancellationToken.None);
}

if (args.Length >= 2 &&
    args[0].Equals("library", StringComparison.OrdinalIgnoreCase))
{
    var action = args[1].ToLowerInvariant();
    if (action is not ("scan" or "list"))
    {
        Console.Error.WriteLine("Usage: fgm-next library <scan|list> [--json]");
        return 2;
    }

    var json = false;
    foreach (var option in args.Skip(2))
    {
        if (option.Equals("--json", StringComparison.OrdinalIgnoreCase))
        {
            json = true;
            continue;
        }

        Console.Error.WriteLine($"Unknown option: {option}");
        return 2;
    }

    using var services = CliServices.Create();
    return await LibraryCommand.RunAsync(services, action, json, CancellationToken.None);
}

if (args.Length >= 2 &&
    args[0].Equals("logs", StringComparison.OrdinalIgnoreCase) &&
    args[1].Equals("tail", StringComparison.OrdinalIgnoreCase))
{
    var lineCount = 200;
    for (var index = 2; index < args.Length; index++)
    {
        if (args[index].Equals("--lines", StringComparison.OrdinalIgnoreCase) &&
            index + 1 < args.Length &&
            int.TryParse(args[index + 1], out var parsed) &&
            parsed > 0)
        {
            lineCount = parsed;
            index++;
            continue;
        }

        Console.Error.WriteLine($"Invalid logs option: {args[index]}");
        return 2;
    }

    using var services = CliServices.Create();
    return await LogsCommand.RunAsync(services, lineCount, CancellationToken.None);
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
Console.WriteLine("  fgm-next gpu");
Console.WriteLine("  fgm-next library list [--json]");
Console.WriteLine("  fgm-next library scan [--json]");
Console.WriteLine("  fgm-next logs tail [--lines N]");
Console.WriteLine("  fgm-next pack validate <pack-dir> [--no-hash] [--schema-only]");
return 0;
