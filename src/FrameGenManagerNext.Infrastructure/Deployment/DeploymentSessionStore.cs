using System.Text.Json;
using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Infrastructure.Paths;

namespace FrameGenManagerNext.Infrastructure.Deployment;

/// <summary>
/// Writes a small heartbeat file per deployment. The manifest already records the
/// persistent state; this session file explains which step was running when a
/// previous process died and lets the next operation detect an abandoned run.
/// </summary>
internal sealed class DeploymentSessionStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly AppPaths _paths;

    public DeploymentSessionStore(AppPaths paths)
    {
        _paths = paths;
    }

    private string SessionsDirectory => Path.Combine(_paths.StateDirectory, "sessions");

    public DeploymentSession Begin(string familyId, string targetDirectory)
    {
        var directory = SessionsDirectory;
        Directory.CreateDirectory(directory);
        var path = Path.Combine(
            directory,
            $"{Sanitize(familyId)}_{DeploymentKey.ForPath(targetDirectory)}.json");
        var session = new DeploymentSession
        {
            Path = path,
            Store = this,
            FamilyId = familyId,
            TargetDirectory = Path.GetFullPath(targetDirectory),
            Phase = "planning",
            State = "running",
            StartedUtc = DateTimeOffset.UtcNow,
            HeartbeatUtc = DateTimeOffset.UtcNow,
        };
        Write(path, session);
        return session;
    }

    public static void Update(DeploymentSession session, string phase, int current, int total)
    {
        session.Phase = phase;
        session.Current = current;
        session.Total = total;
        session.HeartbeatUtc = DateTimeOffset.UtcNow;
        Write(session.Path, session);
    }

    public static void Complete(DeploymentSession session)
    {
        session.Phase = "done";
        session.State = "completed";
        session.HeartbeatUtc = DateTimeOffset.UtcNow;
        Write(session.Path, session);
    }

    public static void Fail(DeploymentSession session, string reason)
    {
        session.Phase = "failed";
        session.State = "failed";
        session.Error = reason;
        session.HeartbeatUtc = DateTimeOffset.UtcNow;
        Write(session.Path, session);
    }

    public int RecoverStale(TimeSpan maxAge)
    {
        if (!Directory.Exists(SessionsDirectory))
        {
            return 0;
        }

        var recovered = 0;
        foreach (var path in Directory.EnumerateFiles(SessionsDirectory, "*.json"))
        {
            try
            {
                var session = JsonSerializer.Deserialize<DeploymentSession>(
                    File.ReadAllText(path),
                    Options);
                if (session is null ||
                    !string.Equals(session.State, "running", StringComparison.OrdinalIgnoreCase) ||
                    DateTimeOffset.UtcNow - session.HeartbeatUtc < maxAge)
                {
                    continue;
                }

                session.Path = path;
                session.State = "abandoned";
                session.Phase = "abandoned";
                session.Error = "The previous deployment did not report progress.";
                Write(path, session);
                recovered++;
            }
            catch (Exception exception) when (exception is IOException or JsonException)
            {
                // A damaged session file must never block a new deployment.
            }
        }

        return recovered;
    }

    private static void Write(string path, DeploymentSession session)
    {
        try
        {
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(session, Options));
            File.Move(temp, path, overwrite: true);
        }
        catch (IOException)
        {
            // The session file is diagnostic only; it must never abort a deployment.
        }
        catch (UnauthorizedAccessException)
        {
            // Same as above: a read-only data directory must not block game writes.
        }
    }

    private static string Sanitize(string value)
    {
        return new string(value.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_').ToArray());
    }
}

internal sealed class DeploymentSession : IDisposable
{
    [System.Text.Json.Serialization.JsonIgnore]
    public DeploymentSessionStore? Store { get; set; }

    public string Path { get; set; } = string.Empty;
    public string FamilyId { get; set; } = string.Empty;
    public string TargetDirectory { get; set; } = string.Empty;
    public string Phase { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public DateTimeOffset StartedUtc { get; set; }
    public DateTimeOffset HeartbeatUtc { get; set; }
    public int Current { get; set; }
    public int Total { get; set; }
    public string? Error { get; set; }

    public void Dispose()
    {
        if (string.Equals(State, "running", StringComparison.OrdinalIgnoreCase))
        {
            DeploymentSessionStore.Fail(this, "The deployment was aborted before completion.");
        }
    }
}

internal sealed class DeploymentProgressRelay(
    DeploymentSession session,
    IProgress<DeploymentProgress>? inner) : IProgress<DeploymentProgress>
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(1);
    private DateTimeOffset _lastWriteUtc = DateTimeOffset.MinValue;
    private string _lastPhase = string.Empty;

    public void Report(DeploymentProgress value)
    {
        var now = DateTimeOffset.UtcNow;
        if (!string.Equals(value.Phase, _lastPhase, StringComparison.Ordinal) ||
            now - _lastWriteUtc >= HeartbeatInterval)
        {
            DeploymentSessionStore.Update(session, value.Phase, value.Current, value.Total);
            _lastPhase = value.Phase;
            _lastWriteUtc = now;
        }

        inner?.Report(value);
    }
}
