using FrameGenNext.Domain;
using FrameGenNext.Infrastructure.Paths;

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
    return 0;
}

Console.WriteLine($"{AppInfo.ProductName} {AppInfo.Version}");
Console.WriteLine();
Console.WriteLine("Usage:");
Console.WriteLine("  framegenn --version");
Console.WriteLine("  framegenn --paths");
return 0;
