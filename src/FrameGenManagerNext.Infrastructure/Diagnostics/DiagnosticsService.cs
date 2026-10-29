using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using FrameGenManagerNext.Core.Features.Diagnostics;
using FrameGenManagerNext.Core.Features.Gpu;
using FrameGenManagerNext.Core.Features.Library;
using FrameGenManagerNext.Domain;
using FrameGenManagerNext.Infrastructure.Paths;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.Infrastructure.Diagnostics;

public sealed class DiagnosticsService : IDiagnosticsService
{
    private readonly AppPaths _paths;
    private readonly IGpuService _gpuService;
    private readonly IGameLibraryService _gameLibraryService;
    private readonly ILogger<DiagnosticsService> _logger;

    public DiagnosticsService(
        AppPaths paths,
        IGpuService gpuService,
        IGameLibraryService gameLibraryService,
        ILogger<DiagnosticsService> logger)
    {
        _paths = paths;
        _gpuService = gpuService;
        _gameLibraryService = gameLibraryService;
        _logger = logger;
    }

    public string LogsDirectory => _paths.LogsDirectory;

    public async Task<string> GetLogTailAsync(int maxLines, CancellationToken cancellationToken)
    {
        var latest = GetLatestLogFile();
        if (latest is null)
        {
            return string.Empty;
        }

        await using var stream = new FileStream(
            latest,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true);

        var text = await reader.ReadToEndAsync(cancellationToken);
        var lines = text.Split(
            ["\r\n", "\n", "\r"],
            StringSplitOptions.None);
        var take = Math.Clamp(maxLines, 1, 5_000);
        return string.Join(Environment.NewLine, lines.TakeLast(take));
    }

    public async Task<string> ExportAsync(CancellationToken cancellationToken)
    {
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            "fgm-diagnostics-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(temporaryDirectory);
        var logsTarget = Path.Combine(temporaryDirectory, "logs");
        Directory.CreateDirectory(logsTarget);

        try
        {
            CopyLogs(logsTarget);
            var report = await BuildReportAsync(cancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(temporaryDirectory, "diagnostics.txt"),
                report,
                cancellationToken);

            var zipPath = Path.Combine(
                Path.GetTempPath(),
                $"fgm-diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.zip");

            if (File.Exists(zipPath))
            {
                zipPath = Path.Combine(
                    Path.GetTempPath(),
                    $"fgm-diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.zip");
            }

            ZipFile.CreateFromDirectory(
                temporaryDirectory,
                zipPath,
                CompressionLevel.Optimal,
                includeBaseDirectory: false);

            return zipPath;
        }
        finally
        {
            try
            {
                Directory.Delete(temporaryDirectory, recursive: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logger.LogDebug(exception, "Could not remove diagnostics temp directory");
            }
        }
    }

    private string? GetLatestLogFile()
    {
        if (!Directory.Exists(LogsDirectory))
        {
            return null;
        }

        return Directory
            .EnumerateFiles(LogsDirectory, "app-*.log")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    private void CopyLogs(string destination)
    {
        if (!Directory.Exists(LogsDirectory))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(LogsDirectory))
        {
            var name = Path.GetFileName(file);
            if (!name.EndsWith(".log", StringComparison.OrdinalIgnoreCase) &&
                !name.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                File.Copy(file, Path.Combine(destination, name), overwrite: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logger.LogDebug(exception, "Could not copy log {Log}", file);
            }
        }
    }

    private async Task<string> BuildReportAsync(CancellationToken cancellationToken)
    {
        string gpuLine;
        try
        {
            var gpu = await _gpuService.GetAsync(cancellationToken);
            gpuLine =
                $"GPU: {gpu.Name}{Environment.NewLine}" +
                $"  route={gpu.Route}, driver={gpu.DriverVersion ?? "unknown"}, " +
                $"frameGen={gpu.FrameGeneration}, smoothMotion={gpu.SmoothMotion}, hags={gpu.HardwareAcceleratedGpuScheduling}";
        }
        catch (Exception exception)
        {
            gpuLine = $"GPU: unavailable ({exception.Message})";
        }

        string libraryLine;
        try
        {
            var library = await _gameLibraryService.LoadAsync(cancellationToken);
            var visible = library.EnumerateVisibleGames().Count();
            libraryLine =
                $"Library: scanned={library.Scanned.Count}, manual={library.Manual.Count}, " +
                $"ignored={library.IgnoredIds.Count}, visible={visible}";
        }
        catch (Exception exception)
        {
            libraryLine = $"Library: unavailable ({exception.Message})";
        }

        var report =
            $"{AppInfo.ProductName} {AppInfo.Version}{Environment.NewLine}" +
            $"OS: {RuntimeInformation.OSDescription}{Environment.NewLine}" +
            $".NET: {Environment.Version}{Environment.NewLine}" +
            $"Process: {(Environment.Is64BitProcess ? "64-bit" : "32-bit")}{Environment.NewLine}" +
            $"Data root: {Redact(_paths.RootDirectory)}{Environment.NewLine}" +
            $"Portable: {_paths.IsPortable}{Environment.NewLine}" +
            gpuLine + Environment.NewLine +
            libraryLine + Environment.NewLine;

        return Redact(report);
    }

    private static string Redact(string text)
    {
        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)] = "%USERPROFILE%",
            [Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)] = "%LOCALAPPDATA%",
            [Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)] = "%APPDATA%",
        };

        foreach (var (value, replacement) in replacements)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                text = text.Replace(value, replacement, StringComparison.OrdinalIgnoreCase);
            }
        }

        return text;
    }
}
