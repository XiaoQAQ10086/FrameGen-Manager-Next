using System.Text.Json;
using System.Text.Json.Serialization;
using FrameGenManagerNext.Core.Features.Library;
using FrameGenManagerNext.Domain.Library;
using FrameGenManagerNext.Infrastructure.Paths;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.Infrastructure.Library;

public sealed class GameLibraryStore : IGameLibraryStore
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    private readonly AppPaths _paths;
    private readonly ILogger<GameLibraryStore> _logger;

    public GameLibraryStore(AppPaths paths, ILogger<GameLibraryStore> logger)
    {
        _paths = paths;
        _logger = logger;
    }

    private string LibraryPath => Path.Combine(_paths.LibraryDirectory, "library.json");

    public async Task<GameLibrary> LoadAsync(CancellationToken cancellationToken)
    {
        _paths.EnsureCreated();
        if (!File.Exists(LibraryPath))
        {
            return new GameLibrary();
        }

        try
        {
            await using var stream = File.OpenRead(LibraryPath);
            var library = await JsonSerializer.DeserializeAsync<GameLibrary>(
                stream,
                Options,
                cancellationToken);

            return Normalize(library);
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Game library cache is corrupt; quarantining it");
            Quarantine();
            return new GameLibrary();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "Could not read game library cache");
            return new GameLibrary();
        }
    }

    public async Task SaveAsync(GameLibrary library, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(library);
        _paths.EnsureCreated();

        var tempPath = LibraryPath + ".tmp";
        try
        {
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, library, Options, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(tempPath, LibraryPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch (IOException)
                {
                    // A stale temp file is harmless.
                }
            }
        }
    }

    private static GameLibrary Normalize(GameLibrary? library)
    {
        if (library is null)
        {
            return new GameLibrary();
        }

        return new GameLibrary
        {
            SchemaVersion = library.SchemaVersion <= 0 ? 1 : library.SchemaVersion,
            LastScanUtc = library.LastScanUtc,
            Scanned = library.Scanned ?? [],
            Manual = library.Manual ?? [],
            IgnoredIds = library.IgnoredIds ?? [],
        };
    }

    private void Quarantine()
    {
        try
        {
            File.Move(LibraryPath, LibraryPath + ".bad", overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "Could not quarantine corrupt game library cache");
        }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            WriteIndented = true,
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
