using System.Globalization;
using FrameGenManagerNext.Core.Features.SmoothMotion;
using FrameGenManagerNext.Infrastructure.NvApi;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.Infrastructure.SmoothMotion;

public sealed class NvApiSmoothMotion : INvApiSmoothMotion
{
    private readonly SmProfileStore _store;
    private readonly ILogger<NvApiSmoothMotion> _logger;

    public NvApiSmoothMotion(SmProfileStore store, ILogger<NvApiSmoothMotion> logger)
    {
        _store = store;
        _logger = logger;
    }

    public Task<SmoothMotionSettingState> GetStateAsync(
        string exePath,
        CancellationToken cancellationToken)
    {
        return Task.Run(() => GetStateCore(exePath), cancellationToken);
    }

    public Task<IReadOnlyList<SmoothMotionSettingState>> GetStatesAsync(
        IReadOnlyList<string> exePaths,
        CancellationToken cancellationToken)
    {
        return Task.Run<IReadOnlyList<SmoothMotionSettingState>>(
            () => GetStatesCore(exePaths),
            cancellationToken);
    }

    private static List<SmoothMotionSettingState> GetStatesCore(
        IReadOnlyList<string> exePaths)
    {
        var opened = NvApiSession.Open();
        if (opened.IsFailure)
        {
            return exePaths
                .Select(_ => new SmoothMotionSettingState(null, string.Empty, false, opened.Error?.Diagnostic))
                .ToList();
        }

        using var session = opened.Value;
        var states = new List<SmoothMotionSettingState>(exePaths.Count);
        foreach (var exePath in exePaths)
        {
            states.Add(ReadState(session, exePath));
        }

        return states;
    }

    public Task<SmoothMotionChangeResult> SetAsync(
        string game,
        string targetDirectory,
        string exePath,
        bool enabled,
        CancellationToken cancellationToken)
    {
        return Task.Run(() => SetCore(game, targetDirectory, exePath, enabled), cancellationToken);
    }

    public Task<SmoothMotionChangeResult> RestoreAsync(
        string game,
        string targetDirectory,
        string exePath,
        CancellationToken cancellationToken)
    {
        return Task.Run(() => RestoreCore(game, targetDirectory, exePath), cancellationToken);
    }

    private static SmoothMotionSettingState GetStateCore(string exePath)
    {
        var opened = NvApiSession.Open();
        if (opened.IsFailure)
        {
            return new SmoothMotionSettingState(null, string.Empty, false, opened.Error?.Diagnostic);
        }

        using var session = opened.Value;
        return ReadState(session, exePath);
    }

    private static SmoothMotionSettingState ReadState(NvApiSession session, string exePath)
    {
        try
        {
            var profile = session.FindProfileForExe(exePath);
            if (profile is null || session.IsBaseProfile(profile.Value))
            {
                return new SmoothMotionSettingState(null, string.Empty, false, "No driver profile is associated with this executable.");
            }

            var name = session.GetProfileName(profile.Value);
            if (string.IsNullOrWhiteSpace(name))
            {
                return new SmoothMotionSettingState(null, string.Empty, false, "The driver profile name could not be read.");
            }

            var value = session.ReadSetting(profile.Value, NvApiNative.SmoothMotionSettingId);
            var enabled = value is null ? (bool?)null : value.Value != 0;
            var ours = name.StartsWith(NvApiNative.ProfilePrefix, StringComparison.Ordinal);
            return new SmoothMotionSettingState(enabled, name, ours, null);
        }
        catch (Exception exception) when (exception is NvApiException or InvalidOperationException)
        {
            return new SmoothMotionSettingState(null, string.Empty, false, exception.Message);
        }
    }

    private SmoothMotionChangeResult SetCore(
        string game,
        string targetDirectory,
        string exePath,
        bool enabled)
    {
        var opened = NvApiSession.Open();
        if (opened.IsFailure)
        {
            return Failure(opened.Error?.Diagnostic ?? "NVAPI is unavailable.");
        }

        using var session = opened.Value;
        try
        {
            var targets = ResolveTargets(session, game, exePath);
            if (targets.Count == 0)
            {
                return Failure("No usable driver profile could be found or created for this executable.");
            }

            var existing = _store.Load(targetDirectory);
            var recordedNames = existing?.AllTargets()
                .Select(target => target.Profile)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList() ?? [];
            var currentNames = targets
                .Select(target => target.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();

            if (existing is null || !recordedNames.SequenceEqual(currentNames, StringComparer.Ordinal))
            {
                var recorded = targets.Select(target =>
                {
                    var oldValue = session.ReadSetting(target.Profile, NvApiNative.SmoothMotionSettingId);
                    return new SmProfileTarget(
                        target.Name,
                        target.Name.StartsWith(NvApiNative.ProfilePrefix, StringComparison.Ordinal),
                        oldValue is not null,
                        oldValue);
                }).ToList();

                _store.Save(new SmProfileRecord(
                    game,
                    exePath,
                    targets[0].Name,
                    recorded[0].Ours,
                    recorded[0].Had,
                    recorded[0].HadValue,
                    targetDirectory)
                {
                    Targets = recorded,
                });
            }

            var value = enabled ? 1u : 0u;
            foreach (var target in targets)
            {
                var setResult = session.SetDword(
                    target.Profile,
                    NvApiNative.SmoothMotionSettingId,
                    value);
                _logger.LogDebug(
                    "Smooth Motion write profile={Profile} status={Status}",
                    target.Name,
                    setResult);
            }

            session.Save();

            var readBacks = targets
                .Select(target => ReadBack(target.Name, exePath))
                .ToList();
            var ok = readBacks.All(readBack => readBack == value);
            var profileList = string.Join(", ", targets.Select(target => $"'{target.Name}'"));
            var message = ok
                ? $"Smooth Motion {(enabled ? "enabled" : "disabled")} in {profileList}."
                : $"Read-back mismatch: expected {value}, got {string.Join(", ", readBacks.Select(readBack => readBack?.ToString(CultureInfo.InvariantCulture) ?? "none"))} ({profileList}).";

            _logger.LogInformation(
                "Smooth Motion set game={Game} enabled={Enabled} profiles={Profiles} readBack={ReadBack}",
                game,
                enabled,
                profileList,
                string.Join(",", readBacks.Select(readBack => readBack?.ToString(CultureInfo.InvariantCulture) ?? "none")));

            return new SmoothMotionChangeResult(ok, readBacks.FirstOrDefault(), targets[0].Name, message);
        }
        catch (Exception exception) when (exception is NvApiException or InvalidOperationException or IOException)
        {
            _logger.LogError(exception, "Could not set Smooth Motion for {Game}", game);
            return Failure(exception.Message);
        }
    }

    private static List<(IntPtr Profile, string Name)> ResolveTargets(
        NvApiSession session,
        string game,
        string exePath)
    {
        var targets = new List<(IntPtr Profile, string Name)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var profile in session.FindProfilesForExe(exePath))
        {
            if (session.IsBaseProfile(profile))
            {
                continue;
            }

            var name = session.GetProfileName(profile);
            if (string.IsNullOrWhiteSpace(name) || !seen.Add(name))
            {
                continue;
            }

            targets.Add((profile, name));
        }

        if (targets.Count > 0)
        {
            return targets;
        }

        var profileName = NvApiNative.ProfilePrefix + game;
        var handle = session.FindProfile(profileName);
        if (handle is null || session.IsBaseProfile(handle.Value))
        {
            handle = session.CreateProfile(profileName);
        }

        if (session.IsBaseProfile(handle.Value))
        {
            return targets;
        }

        var created = session.GetProfileName(handle.Value);
        if (string.IsNullOrWhiteSpace(created) || session.CreateApplication(handle.Value, exePath) != 0)
        {
            return targets;
        }

        targets.Add((handle.Value, created));
        return targets;
    }

    private SmoothMotionChangeResult RestoreCore(
        string game,
        string targetDirectory,
        string exePath)
    {
        var record = _store.Load(targetDirectory);
        if (record is null)
        {
            return new SmoothMotionChangeResult(true, null, string.Empty, "No Smooth Motion record exists for this game.");
        }

        var opened = NvApiSession.Open();
        if (opened.IsFailure)
        {
            return Failure(opened.Error?.Diagnostic ?? "NVAPI is unavailable.");
        }

        using var session = opened.Value;
        try
        {
            var records = record.AllTargets();
            if (records.Count == 0)
            {
                _store.Delete(targetDirectory);
                return new SmoothMotionChangeResult(true, null, string.Empty, "No Smooth Motion record exists for this game.");
            }

            var resolved = new List<(IntPtr Profile, SmProfileTarget Record)>();
            foreach (var target in records)
            {
                var handle = session.FindProfile(target.Profile);
                if (handle is null || session.IsBaseProfile(handle.Value))
                {
                    var byExe = session.FindProfileForExe(exePath);
                    if (byExe is null || session.IsBaseProfile(byExe.Value))
                    {
                        continue;
                    }

                    handle = byExe;
                }

                resolved.Add((handle.Value, target));
            }

            if (resolved.Count == 0)
            {
                _store.Delete(targetDirectory);
                return new SmoothMotionChangeResult(true, null, record.Profile, "The driver profile no longer exists.");
            }

            foreach (var entry in resolved)
            {
                var want = entry.Record.Had ? entry.Record.HadValue : null;
                if (want is not null)
                {
                    session.SetDword(entry.Profile, NvApiNative.SmoothMotionSettingId, want.Value);
                }
                else
                {
                    session.DeleteSetting(entry.Profile, NvApiNative.SmoothMotionSettingId);
                }
            }

            session.Save();
            var readBacks = resolved
                .Select(entry => ReadBack(entry.Record.Profile, exePath))
                .ToList();
            var ok = resolved
                .Select((entry, index) => entry.Record.Had
                    ? readBacks[index] == entry.Record.HadValue
                    : readBacks[index] is null)
                .All(match => match);
            if (ok)
            {
                _store.Delete(targetDirectory);
            }

            var message = ok
                ? "Smooth Motion restored to the original value in all matching driver profiles."
                : "Restore read-back mismatch in at least one driver profile.";

            _logger.LogInformation(
                "Smooth Motion restore game={Game} profiles={Profiles} ok={Ok} readBack={ReadBack}",
                game,
                string.Join(",", resolved.Select(entry => entry.Record.Profile)),
                ok,
                string.Join(",", readBacks.Select(readBack => readBack?.ToString(CultureInfo.InvariantCulture) ?? "none")));

            return new SmoothMotionChangeResult(ok, readBacks.FirstOrDefault(), resolved[0].Record.Profile, message);
        }
        catch (Exception exception) when (exception is NvApiException or InvalidOperationException or IOException)
        {
            _logger.LogError(exception, "Could not restore Smooth Motion for {Game}", game);
            return Failure(exception.Message);
        }
    }

    private static uint? ReadBack(string profileName, string exePath)
    {
        var opened = NvApiSession.Open();
        if (opened.IsFailure)
        {
            return null;
        }

        using var session = opened.Value;
        var profile = session.FindProfile(profileName) ?? session.FindProfileForExe(exePath);
        return profile is null
            ? null
            : session.ReadSetting(profile.Value, NvApiNative.SmoothMotionSettingId);
    }

    private static SmoothMotionChangeResult Failure(string message)
    {
        return new SmoothMotionChangeResult(false, null, string.Empty, message);
    }
}
