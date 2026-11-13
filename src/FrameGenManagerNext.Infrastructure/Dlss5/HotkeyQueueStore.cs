using System.Text.Json;
using FrameGenManagerNext.Infrastructure.Paths;

namespace FrameGenManagerNext.Infrastructure.Dlss5;

/// <summary>
/// Persists hotkey writes that could not be applied yet. ReShade can hold
/// ReShade.ini open while the game runs, and it may flush its own settings on
/// exit, so the write is retried after the game closes.
/// </summary>
public sealed class HotkeyQueueStore : IDisposable
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly AppPaths _paths;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public HotkeyQueueStore(AppPaths paths)
    {
        _paths = paths;
    }

    private string QueuePath => Path.Combine(_paths.StateDirectory, "hotkey-queue.json");

    internal async Task EnqueueAsync(
        string targetDirectory,
        string? exePath,
        int virtualKey,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var items = await ReadCoreAsync(cancellationToken);
            var normalized = Normalize(targetDirectory);
            items.RemoveAll(item =>
                string.Equals(Normalize(item.TargetDirectory), normalized, StringComparison.OrdinalIgnoreCase));
            items.Add(new PendingHotkey
            {
                TargetDirectory = targetDirectory,
                ExePath = string.IsNullOrWhiteSpace(exePath) ? null : exePath,
                VirtualKey = virtualKey,
                Attempts = 0,
                EnqueuedUtc = DateTimeOffset.UtcNow,
            });
            await WriteCoreAsync(items, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    internal async Task<IReadOnlyList<PendingHotkey>> ReadAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return (await ReadCoreAsync(cancellationToken)).ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    internal async Task RemoveAsync(string targetDirectory, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var items = await ReadCoreAsync(cancellationToken);
            var normalized = Normalize(targetDirectory);
            items.RemoveAll(item =>
                string.Equals(Normalize(item.TargetDirectory), normalized, StringComparison.OrdinalIgnoreCase));
            await WriteCoreAsync(items, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    internal async Task MarkFailureAsync(
        string targetDirectory,
        string error,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var items = await ReadCoreAsync(cancellationToken);
            var normalized = Normalize(targetDirectory);
            var item = items.FirstOrDefault(candidate =>
                string.Equals(Normalize(candidate.TargetDirectory), normalized, StringComparison.OrdinalIgnoreCase));
            if (item is not null)
            {
                item.Attempts++;
                item.LastError = error;
                item.LastAttemptUtc = DateTimeOffset.UtcNow;
                await WriteCoreAsync(items, cancellationToken);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<List<PendingHotkey>> ReadCoreAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(QueuePath))
        {
            return [];
        }

        try
        {
            await using var stream = File.OpenRead(QueuePath);
            return await JsonSerializer.DeserializeAsync<List<PendingHotkey>>(
                       stream,
                       Options,
                       cancellationToken) ??
                   [];
        }
        catch (JsonException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
    }

    private async Task WriteCoreAsync(List<PendingHotkey> items, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_paths.StateDirectory);
        var temp = QueuePath + ".tmp";
        await using (var stream = File.Create(temp))
        {
            await JsonSerializer.SerializeAsync(stream, items, Options, cancellationToken);
        }

        File.Move(temp, QueuePath, overwrite: true);
    }

    public void Dispose()
    {
        _gate.Dispose();
    }

    private static string Normalize(string path)
    {
        try
        {
            return Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path;
        }
    }
}

internal sealed class PendingHotkey
{
    public string TargetDirectory { get; set; } = string.Empty;
    public string? ExePath { get; set; }
    public int VirtualKey { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset EnqueuedUtc { get; set; }
    public DateTimeOffset? LastAttemptUtc { get; set; }
}
