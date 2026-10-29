using System.Security.Cryptography;
using System.Text;
using FrameGenManagerNext.Core.Features.Library;
using FrameGenManagerNext.Domain.Library;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.Infrastructure.Library;

public sealed class GameProbe : IGameProbe
{
    private const int MaxEntries = 30_000;
    private const int MaxDepth = 5;
    private const int MaxCandidates = 3_000;

    private static readonly string[] GraphicsApiSiblingFiles =
    [
        "d3d12.dll",
        "d3d11.dll",
        "d3d10.dll",
        "d3d10_1.dll",
        "d3d9.dll",
        "vulkan-1.dll",
        "opengl32.dll",
        "dxgi.dll",
    ];

    private static readonly string[] WrapperDllNames =
    [
        "dxgi.dll",
        "d3d12.dll",
        "d3d11.dll",
        "d3d9.dll",
    ];

    private static readonly HashSet<string> SkippedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "_CommonRedist",
        "_Redist",
        "DirectX",
        "vcredist",
        "Redist",
        "EasyAntiCheat",
        "BattlEye",
        "Engine",
        "Tools",
        "Support",
        "Docs",
        "Content",
        "Saved",
        "Config",
        "Logs",
        "CrashReport",
        "Installers",
    };

    private readonly ILogger<GameProbe> _logger;

    public GameProbe(ILogger<GameProbe> logger)
    {
        _logger = logger;
    }

    public Task<GameEntry> ProbeAsync(GameCandidate candidate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return Task.Run(() => ProbeCore(candidate), cancellationToken);
    }

    public Task<GameEntry?> ProbeManualAsync(string exePath, CancellationToken cancellationToken)
    {
        return Task.Run(() => ProbeManualCore(exePath), cancellationToken);
    }

    private GameEntry ProbeCore(GameCandidate candidate)
    {
        var installDirectory = Path.GetFullPath(candidate.InstallDirectory);
        var renderExe = FindRenderExe(installDirectory, candidate.Name);
        var graphicsApi = renderExe is null ? GraphicsApi.Unknown : DetectApi(renderExe);
        var targetDirectory = renderExe is null
            ? installDirectory
            : Path.GetDirectoryName(renderExe) ?? installDirectory;

        if (renderExe is not null)
        {
            _logger.LogDebug(
                "Detected render exe {Exe} for {Game} ({Api})",
                renderExe,
                candidate.Name,
                graphicsApi);
        }

        return new GameEntry(
            BuildId(candidate),
            candidate.Name,
            candidate.Launcher,
            installDirectory,
            renderExe,
            targetDirectory,
            graphicsApi,
            candidate.AppId,
            IsManual: false);
    }

    private static GameEntry? ProbeManualCore(string exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath) ||
            !File.Exists(exePath) ||
            !string.Equals(Path.GetExtension(exePath), ".exe", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(exePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        var name = Path.GetFileNameWithoutExtension(fullPath);
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return new GameEntry(
            "manual:" + ShortHash(fullPath),
            name,
            LauncherKind.Manual,
            directory,
            fullPath,
            directory,
            DetectApi(fullPath),
            AppId: null,
            IsManual: true);
    }

    private string? FindRenderExe(string installDirectory, string gameName)
    {
        var candidates = new List<GameExeCandidate>();
        var stack = new Stack<(string Directory, int Depth)>();
        stack.Push((installDirectory, 0));

        var scanned = 0;
        while (stack.Count > 0 && scanned < MaxEntries && candidates.Count < MaxCandidates)
        {
            var (directory, depth) = stack.Pop();
            IEnumerable<string> entries;

            try
            {
                entries = Directory.EnumerateFileSystemEntries(directory)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
            {
                _logger.LogDebug(exception, "Could not enumerate {Directory}", directory);
                continue;
            }

            foreach (var entry in entries)
            {
                scanned++;
                if (scanned >= MaxEntries)
                {
                    break;
                }

                if (Directory.Exists(entry))
                {
                    if (depth < MaxDepth &&
                        !SkippedDirectories.Contains(Path.GetFileName(entry)) &&
                        !IsReparsePoint(entry))
                    {
                        stack.Push((entry, depth + 1));
                    }

                    continue;
                }

                if (!string.Equals(Path.GetExtension(entry), ".exe", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    var info = new FileInfo(entry);
                    var relative = Path.GetRelativePath(installDirectory, directory);
                    candidates.Add(new GameExeCandidate(
                        info.FullName,
                        relative == "." ? string.Empty : relative,
                        info.Length));
                }
                catch (Exception exception) when (
                    exception is IOException or UnauthorizedAccessException or FileNotFoundException)
                {
                    _logger.LogDebug(exception, "Could not inspect {File}", entry);
                }
            }
        }

        var installDirectoryName = Path.GetFileName(installDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var best = RenderExeLocator.FindBest(candidates, installDirectoryName);
        if (best is null)
        {
            _logger.LogDebug(
                "No render exe found for {Game}; inspected {Count} candidates",
                gameName,
                candidates.Count);
        }

        return best?.FullPath;
    }

    private static bool IsReparsePoint(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or FileNotFoundException)
        {
            return true;
        }
    }

    public static GraphicsApi DetectApi(string exePath)
    {
        var imports = PeImportReader.GetImportedDlls(exePath);
        var directory = Path.GetDirectoryName(exePath) ?? string.Empty;
        var siblings = GraphicsApiSiblingFiles
            .Where(name => File.Exists(Path.Combine(directory, name)))
            .ToList();

        var wrapperIsVulkan = WrapperImportsVulkan(directory);
        var api = GraphicsApiClassifier.Classify(new GraphicsApiFacts(
            imports,
            siblings,
            Path.GetFileName(exePath),
            wrapperIsVulkan));

        if (api != GraphicsApi.Unknown)
        {
            return api;
        }

        foreach (var engineName in new[] { "unityplayer.dll", "engine2.dll", "engine.dll", "crysystem.dll" })
        {
            var enginePath = Path.Combine(directory, engineName);
            if (!File.Exists(enginePath))
            {
                continue;
            }

            var engineImports = PeImportReader.GetImportedDlls(enginePath);
            var engineApi = GraphicsApiClassifier.Classify(new GraphicsApiFacts(
                engineImports,
                siblings,
                engineName,
                wrapperIsVulkan));

            if (engineApi != GraphicsApi.Unknown)
            {
                return engineApi;
            }
        }

        return GraphicsApi.Unknown;
    }

    private static bool WrapperImportsVulkan(string directory)
    {
        foreach (var name in WrapperDllNames)
        {
            var path = Path.Combine(directory, name);
            if (!File.Exists(path))
            {
                continue;
            }

            var imports = PeImportReader.GetImportedDlls(path);
            if (imports.Any(dll => string.Equals(dll, "vulkan-1.dll", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static string BuildId(GameCandidate candidate)
    {
        var prefix = candidate.Launcher.ToString().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(candidate.AppId))
        {
            return $"{prefix}:{candidate.AppId.Trim().ToLowerInvariant()}";
        }

        return $"{prefix}:{ShortHash(Path.GetFullPath(candidate.InstallDirectory))}";
    }

    private static string ShortHash(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value.ToLowerInvariant());
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }
}
