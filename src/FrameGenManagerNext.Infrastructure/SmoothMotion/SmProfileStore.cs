using System.Text.Json;
using FrameGenManagerNext.Infrastructure.Deployment;
using FrameGenManagerNext.Infrastructure.Paths;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.Infrastructure.SmoothMotion;

public sealed record SmProfileRecord(
    string Game,
    string Exe,
    string Profile,
    bool Ours,
    bool Had,
    uint? HadValue,
    string Directory)
{
    /// <summary>
    /// Profiles written for this game. Older records only carry the single
    /// <see cref="Profile"/> field; restore falls back to it.
    /// </summary>
    public List<SmProfileTarget> Targets { get; init; } = [];

    public IReadOnlyList<SmProfileTarget> AllTargets()
    {
        if (Targets.Count > 0)
        {
            return Targets;
        }

        return string.IsNullOrWhiteSpace(Profile)
            ? []
            : [new SmProfileTarget(Profile, Ours, Had, HadValue)];
    }
}

public sealed class SmProfileStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly AppPaths _paths;
    private readonly ILogger<SmProfileStore> _logger;

    public SmProfileStore(AppPaths paths, ILogger<SmProfileStore> logger)
    {
        _paths = paths;
        _logger = logger;
    }

    public SmProfileRecord? Load(string targetDirectory)
    {
        var path = PathFor(targetDirectory);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<SmProfileRecord>(json, Options);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "Could not read Smooth Motion record {Path}", path);
            return null;
        }
    }

    public void Save(SmProfileRecord record)
    {
        var path = PathFor(record.Directory);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temp = path + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(record, Options));
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
            {
                try
                {
                    File.Delete(temp);
                }
                catch (IOException)
                {
                    // Best-effort cleanup.
                }
            }
        }
    }

    public void Delete(string targetDirectory)
    {
        var path = PathFor(targetDirectory);
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "Could not delete Smooth Motion record {Path}", path);
        }
    }

    private string PathFor(string targetDirectory)
    {
        return Path.Combine(
            _paths.BackupsDirectory,
            "sm-profiles",
            DeploymentKey.ForPath(targetDirectory) + ".json");
    }
}
