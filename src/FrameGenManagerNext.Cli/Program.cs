using FrameGenManagerNext.Cli;
using FrameGenManagerNext.Core.Features.Framegen;
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
    if (args.Length >= 2 && args[1].Equals("spoof", StringComparison.OrdinalIgnoreCase))
    {
        var action = args.Length > 2 ? args[2].ToLowerInvariant() : "inspect";
        var value = args.Length > 3 ? string.Join(' ', args.Skip(3)) : null;
        return await GpuCommand.RunSpoofAsync(services, action, value, CancellationToken.None);
    }

    if (args.Length != 1)
    {
        Console.Error.WriteLine("Usage: fgm-next gpu [spoof [inspect|apply|restore] [value]]");
        return 2;
    }

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

if (args.Length >= 4 &&
    args[0].Equals("sm", StringComparison.OrdinalIgnoreCase))
{
    var action = args[1].ToLowerInvariant();
    if (action is not ("inspect" or "enable" or "disable" or "restore"))
    {
        Console.Error.WriteLine("Usage: fgm-next sm <inspect|enable|disable|restore> <target-dir> <game-exe>");
        return 2;
    }

    using var services = CliServices.Create();
    return await SmoothMotionCommand.RunAsync(
        services,
        action,
        args[2],
        args[3],
        CancellationToken.None);
}

if (args.Length >= 3 &&
    args[0].Equals("dlss5", StringComparison.OrdinalIgnoreCase))
{
    var action = args[1].ToLowerInvariant();
    if (action is not ("inspect" or "install" or "restore" or "uninstall"))
    {
        Console.Error.WriteLine("Usage: fgm-next dlss5 <inspect|install|restore|uninstall> <target-dir> [game-exe]");
        return 2;
    }

    using var services = CliServices.Create();
    return await Dlss5Command.RunAsync(
        services,
        action,
        args[2],
        args.Length > 3 ? args[3] : null,
        CancellationToken.None);
}

if (args.Length >= 3 &&
    args[0].Equals("deploy", StringComparison.OrdinalIgnoreCase))
{
    var action = args[1].ToLowerInvariant();
    if (action is not ("inspect" or "framegen" or "restore"))
    {
        Console.Error.WriteLine("Usage: fgm-next deploy <inspect|framegen|restore> <target-dir> [--proxy version.dll]");
        return 2;
    }

    var options = FrameGenOptions.Default;
    for (var index = 3; index < args.Length; index++)
    {
        var option = args[index];
        if (index + 1 >= args.Length)
        {
            Console.Error.WriteLine($"Missing value for {option}");
            return 2;
        }

        var value = args[index + 1];
        index++;

        if (option.Equals("--proxy", StringComparison.OrdinalIgnoreCase))
        {
            options = options with { ProxySlot = value };
        }
        else if (option.Equals("--optimized", StringComparison.OrdinalIgnoreCase) &&
                 int.TryParse(value, out var optimized))
        {
            options = options with { Optimized = optimized };
        }
        else if (option.Equals("--frames", StringComparison.OrdinalIgnoreCase) &&
                 int.TryParse(value, out var frames))
        {
            options = options with { MaxGeneratedFrames = frames };
        }
        else if (option.Equals("--preset", StringComparison.OrdinalIgnoreCase))
        {
            options = options with { Preset = value };
        }
        else if (option.Equals("--engine", StringComparison.OrdinalIgnoreCase))
        {
            options = options with { Engine = value };
        }
        else if (option.Equals("--sr", StringComparison.OrdinalIgnoreCase))
        {
            options = options with { SuperResolutionMode = value };
        }
        else if (option.Equals("--flow", StringComparison.OrdinalIgnoreCase))
        {
            options = options with { XeSsFlowResolution = value };
        }
        else if (option.Equals("--hudless", StringComparison.OrdinalIgnoreCase))
        {
            options = options with { XeSsHudless = value };
        }
        else
        {
            Console.Error.WriteLine($"Unknown option: {option}");
            return 2;
        }
    }

    try
    {
        FrameGenIniBuilder.Validate(options);
    }
    catch (ArgumentException exception)
    {
        Console.Error.WriteLine(exception.Message);
        return 2;
    }

    using var services = CliServices.Create();
    return await DeployCommand.RunAsync(
        services,
        action,
        args[2],
        options,
        CancellationToken.None);
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
Console.WriteLine("  fgm-next sm <inspect|enable|disable|restore> <target-dir> <game-exe>");
Console.WriteLine("  fgm-next dlss5 <inspect|install|restore> <target-dir> <game-exe>");
Console.WriteLine("  fgm-next deploy <inspect|framegen|restore> <target-dir>");
Console.WriteLine("      [--proxy version.dll] [--optimized 0-3] [--frames 3|5] [--preset A|B|C]");
Console.WriteLine("      [--engine DLSS|XeSS] [--sr Off|M-INT8|L-INT8|M-FP16|L-FP16]");
Console.WriteLine("      [--flow Coarse|Middle|Fine] [--hudless Auto|1|0]");
Console.WriteLine("  fgm-next pack validate <pack-dir> [--no-hash] [--schema-only]");
return 0;
