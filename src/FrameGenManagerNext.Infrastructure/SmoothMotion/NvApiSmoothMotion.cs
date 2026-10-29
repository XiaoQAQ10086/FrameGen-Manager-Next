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
        try
        {
            var profile = session.FindProfileForExe(exePath);
            if (profile is null)
            {
                return new SmoothMotionSettingState(null, string.Empty, false, "No driver profile is associated with this executable.");
            }

            if (session.IsBaseProfile(profile.Value))
            {
                return new SmoothMotionSettingState(null, string.Empty, false, "Refusing to use the base profile.");
            }

            var name = session.GetProfileName(profile.Value);
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
            var profile = session.FindProfileForExe(exePath);
            var createdProfile = false;

            if (profile is null)
            {
                var profileName = NvApiNative.ProfilePrefix + game;
                profile = session.FindProfile(profileName) ?? session.CreateProfile(profileName);
                createdProfile = true;

                var createResult = session.CreateApplication(profile.Value, exePath);
                if (createResult != 0)
                {
                    return Failure(
                        $"The executable is not associated with a driver profile and could not be attached to a new one (status {createResult}).");
                }
            }

            if (session.IsBaseProfile(profile.Value))
            {
                return Failure("Refusing to write to the base profile.");
            }

            var targetProfileName = session.GetProfileName(profile.Value);
            if (string.IsNullOrWhiteSpace(targetProfileName))
            {
                return Failure("The driver profile name could not be read; aborting.");
            }

            var existing = _store.Load(targetDirectory);
            if (existing is null || !string.Equals(existing.Profile, targetProfileName, StringComparison.Ordinal))
            {
                var oldValue = session.ReadSetting(profile.Value, NvApiNative.SmoothMotionSettingId);
                _store.Save(new SmProfileRecord(
                    game,
                    exePath,
                    targetProfileName,
                    targetProfileName.StartsWith(NvApiNative.ProfilePrefix, StringComparison.Ordinal),
                    oldValue is not null,
                    oldValue,
                    targetDirectory));
            }

            var value = enabled ? 1u : 0u;
            var setResult = session.SetDword(
                profile.Value,
                NvApiNative.SmoothMotionSettingId,
                value);
            session.Save();

            var readBack = ReadBack(targetProfileName, exePath);
            var ok = readBack == value;
            var message = ok
                ? $"Smooth Motion {(enabled ? "enabled" : "disabled")} in profile '{targetProfileName}'."
                : $"Read-back mismatch: expected {value}, got {readBack?.ToString(CultureInfo.InvariantCulture) ?? "none"} (profile '{targetProfileName}').";

            _logger.LogInformation(
                "Smooth Motion set game={Game} enabled={Enabled} profile={Profile} status={Status} readBack={ReadBack} createdProfile={Created}",
                game,
                enabled,
                targetProfileName,
                setResult,
                readBack,
                createdProfile);

            return new SmoothMotionChangeResult(ok, readBack, targetProfileName, message);
        }
        catch (Exception exception) when (exception is NvApiException or InvalidOperationException or IOException)
        {
            _logger.LogError(exception, "Could not set Smooth Motion for {Game}", game);
            return Failure(exception.Message);
        }
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
            var profile = session.FindProfile(record.Profile);
            profile ??= session.FindProfileForExe(exePath);
            if (profile is null)
            {
                _store.Delete(targetDirectory);
                return new SmoothMotionChangeResult(true, null, record.Profile, "The driver profile no longer exists.");
            }

            if (session.IsBaseProfile(profile.Value))
            {
                return Failure("Refusing to write to the base profile.");
            }

            var want = record.Had ? record.HadValue : null;
            if (want is not null)
            {
                session.SetDword(profile.Value, NvApiNative.SmoothMotionSettingId, want.Value);
            }
            else
            {
                session.DeleteSetting(profile.Value, NvApiNative.SmoothMotionSettingId);
            }

            session.Save();
            var readBack = ReadBack(record.Profile, exePath);
            var ok = want is null ? readBack is null : readBack == want;
            if (ok)
            {
                _store.Delete(targetDirectory);
            }

            var message = ok
                ? want is null
                    ? "Smooth Motion override removed; the profile follows the global setting."
                    : $"Smooth Motion restored to the original value {want}."
                : $"Restore read-back mismatch: expected {want?.ToString(CultureInfo.InvariantCulture) ?? "none"}, got {readBack?.ToString(CultureInfo.InvariantCulture) ?? "none"}.";

            _logger.LogInformation(
                "Smooth Motion restore game={Game} profile={Profile} ok={Ok} expected={Expected} readBack={ReadBack}",
                game,
                record.Profile,
                ok,
                want,
                readBack);

            return new SmoothMotionChangeResult(ok, readBack, record.Profile, message);
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
