using System.Text.Json;
using System.Text.Json.Serialization;
using FrameGenManagerNext.Domain.Errors;
using FrameGenManagerNext.Domain.Packs;
using FrameGenManagerNext.Domain.Results;

namespace FrameGenManagerNext.Infrastructure.Packs;

/// <summary>
/// Reads and writes pack.json with strict unknown-member handling.
/// </summary>
public static class PackJsonSerializer
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static Result<AssetPack> LoadFromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            return Result<AssetPack>.Fail(
                AppError.Create(ErrorCode.PackNotFound, ("path", Path.GetFullPath(path))));
        }

        try
        {
            var json = File.ReadAllText(path);
            var pack = JsonSerializer.Deserialize<AssetPack>(json, Options);
            if (pack is null)
            {
                return Result<AssetPack>.Fail(
                    AppError.Create(ErrorCode.PackInvalid, ("path", Path.GetFullPath(path)), ("reason", "json is empty")));
            }

            return Result<AssetPack>.Ok(pack);
        }
        catch (JsonException exception)
        {
            return Result<AssetPack>.Fail(
                AppError.Create(
                    ErrorCode.PackInvalid,
                    ("path", Path.GetFullPath(path)),
                    ("reason", exception.Message)));
        }
        catch (UnauthorizedAccessException exception)
        {
            return Result<AssetPack>.Fail(
                AppError.Create(ErrorCode.AccessDenied, ("path", Path.GetFullPath(path)), ("reason", exception.Message)));
        }
        catch (IOException exception)
        {
            return Result<AssetPack>.Fail(
                AppError.Create(ErrorCode.FileNotFound, ("path", Path.GetFullPath(path)), ("reason", exception.Message)));
        }
    }

    public static string Serialize(AssetPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        return JsonSerializer.Serialize(pack, Options);
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            WriteIndented = true,
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
