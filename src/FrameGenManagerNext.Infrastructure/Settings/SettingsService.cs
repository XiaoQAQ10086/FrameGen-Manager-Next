using System.Text.Json;
using System.Text.Json.Serialization;
using FrameGenManagerNext.Core.Features.Settings;
using FrameGenManagerNext.Domain.Errors;
using FrameGenManagerNext.Domain.Results;
using FrameGenManagerNext.Domain.Settings;
using FrameGenManagerNext.Infrastructure.Paths;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.Infrastructure.Settings;

public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    private readonly AppPaths _paths;
    private readonly ILogger<SettingsService> _logger;

    public SettingsService(AppPaths paths, ILogger<SettingsService> logger)
    {
        _paths = paths;
        _logger = logger;
    }

    public AppSettings Load()
    {
        var path = SettingsPath();
        if (!File.Exists(path))
        {
            return AppSettings.Default;
        }

        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            var settings = JsonSerializer.Deserialize<AppSettings>(stream, Options);
            return settings is null ? AppSettings.Default : Sanitize(settings);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.LogWarning(exception, "Could not read settings: {Path}", path);
            TryQuarantine(path);
            return AppSettings.Default;
        }
    }

    public async Task<Result<AppSettings>> SaveAsync(
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var sanitized = Sanitize(settings);
        var path = SettingsPath();

        try
        {
            _paths.EnsureCreated();
            var temp = path + ".tmp";
            await using (var stream = File.Create(temp))
            {
                await JsonSerializer.SerializeAsync(stream, sanitized, Options, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temp, path, overwrite: true);
            return Result<AppSettings>.Ok(sanitized);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "Could not save settings: {Path}", path);
            return Result<AppSettings>.Fail(new AppError(
                exception is UnauthorizedAccessException ? ErrorCode.AccessDenied : ErrorCode.FileLocked,
                exception.Message));
        }
    }

    private static AppSettings Sanitize(AppSettings settings)
    {
        return settings with
        {
            SchemaVersion = 1,
            Language = Enum.IsDefined(settings.Language) ? settings.Language : AppLanguage.System,
            Theme = AppTheme.Light,
        };
    }

    private string SettingsPath()
    {
        return Path.Combine(_paths.ConfigDirectory, "app.json");
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    private static void TryQuarantine(string path)
    {
        try
        {
            File.Move(path, path + ".bad", overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Keep the original file if it cannot be moved.
        }
    }
}
