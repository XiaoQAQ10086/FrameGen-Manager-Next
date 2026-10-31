using System.Runtime.InteropServices;
using FrameGenManagerNext.Domain.Errors;
using FrameGenManagerNext.Domain.Results;

namespace FrameGenManagerNext.Infrastructure.NvApi;

internal sealed partial class NvApiSession : IDisposable
{
    private readonly IntPtr _library;
    private readonly IntPtr _session;
    private readonly NvApiNative.GetBaseProfileDelegate _getBaseProfile;
    private readonly NvApiNative.FindProfileDelegate _findProfile;
    private readonly NvApiNative.CreateProfileDelegate _createProfile;
    private readonly NvApiNative.CreateApplicationDelegate _createApplication;
    private readonly NvApiNative.FindApplicationDelegate _findApplication;
    private readonly NvApiNative.GetSettingDelegate _getSetting;
    private readonly NvApiNative.SetSettingDelegate _setSetting;
    private readonly NvApiNative.DeleteSettingDelegate _deleteSetting;
    private readonly NvApiNative.SaveSettingsDelegate _saveSettings;
    private readonly NvApiNative.ProfileInfoDelegate _profileInfo;
    private readonly NvApiNative.DestroySessionDelegate? _destroySession;
    private readonly NvApiNative.UnloadDelegate _unload;
    private bool _disposed;

    private NvApiSession(
        IntPtr library,
        IntPtr session,
        NvApiNative.GetBaseProfileDelegate getBaseProfile,
        NvApiNative.FindProfileDelegate findProfile,
        NvApiNative.CreateProfileDelegate createProfile,
        NvApiNative.CreateApplicationDelegate createApplication,
        NvApiNative.FindApplicationDelegate findApplication,
        NvApiNative.GetSettingDelegate getSetting,
        NvApiNative.SetSettingDelegate setSetting,
        NvApiNative.DeleteSettingDelegate deleteSetting,
        NvApiNative.SaveSettingsDelegate saveSettings,
        NvApiNative.ProfileInfoDelegate profileInfo,
        NvApiNative.DestroySessionDelegate? destroySession,
        NvApiNative.UnloadDelegate unload)
    {
        _library = library;
        _session = session;
        _getBaseProfile = getBaseProfile;
        _findProfile = findProfile;
        _createProfile = createProfile;
        _createApplication = createApplication;
        _findApplication = findApplication;
        _getSetting = getSetting;
        _setSetting = setSetting;
        _deleteSetting = deleteSetting;
        _saveSettings = saveSettings;
        _profileInfo = profileInfo;
        _destroySession = destroySession;
        _unload = unload;
    }

    public static Result<NvApiSession> Open()
    {
        if (!NativeLibrary.TryLoad("nvapi64.dll", out var library))
        {
            return Result<NvApiSession>.Fail(new AppError(
                ErrorCode.NvApiUnavailable,
                "nvapi64.dll could not be loaded."));
        }

        IntPtr session = IntPtr.Zero;
        NvApiNative.UnloadDelegate? unload = null;
        var initialized = false;
        try
        {
            var queryPointer = NativeLibrary.GetExport(library, "nvapi_QueryInterface");
            var query = Marshal.GetDelegateForFunctionPointer<NvApiNative.QueryInterface>(queryPointer);

            var initialize = GetDelegate<NvApiNative.InitializeDelegate>(query, NvApiNative.IdInitialize);
            unload = GetDelegate<NvApiNative.UnloadDelegate>(query, NvApiNative.IdUnload);
            var initializeResult = initialize();
            if (initializeResult != 0)
            {
                return FailAndFree(library, ErrorCode.NvApiCallFailed, $"NvAPI_Initialize returned {initializeResult}.");
            }

            initialized = true;
            var createSession = GetDelegate<NvApiNative.CreateSessionDelegate>(query, NvApiNative.IdCreateSession);
            var createResult = createSession(out session);
            if (createResult != 0)
            {
                TryUnload(unload);
                return FailAndFree(library, ErrorCode.NvApiCallFailed, $"DRS_CreateSession returned {createResult}.");
            }

            var loadSettings = GetDelegate<NvApiNative.LoadSettingsDelegate>(query, NvApiNative.IdLoadSettings);
            var loadResult = loadSettings(session);
            if (loadResult != 0)
            {
                TryDestroySession(library, session);
                TryUnload(unload);
                return FailAndFree(library, ErrorCode.NvApiCallFailed, $"DRS_LoadSettings returned {loadResult}.");
            }

            var getBaseProfile = GetDelegate<NvApiNative.GetBaseProfileDelegate>(query, NvApiNative.IdGetBaseProfile);
            var findProfile = GetDelegate<NvApiNative.FindProfileDelegate>(query, NvApiNative.IdFindProfile);
            var createProfile = GetDelegate<NvApiNative.CreateProfileDelegate>(query, NvApiNative.IdCreateProfile);
            var createApplication = GetDelegate<NvApiNative.CreateApplicationDelegate>(query, NvApiNative.IdCreateApplication);
            var findApplication = GetDelegate<NvApiNative.FindApplicationDelegate>(query, NvApiNative.IdFindApplicationByName);
            var getSetting = GetDelegate<NvApiNative.GetSettingDelegate>(query, NvApiNative.IdGetSetting);
            var setSetting = GetDelegate<NvApiNative.SetSettingDelegate>(query, NvApiNative.IdSetSetting);
            var deleteSetting = GetDelegate<NvApiNative.DeleteSettingDelegate>(query, NvApiNative.IdDeleteProfileSetting);
            var saveSettings = GetDelegate<NvApiNative.SaveSettingsDelegate>(query, NvApiNative.IdSaveSettings);
            var profileInfo = GetDelegate<NvApiNative.ProfileInfoDelegate>(query, NvApiNative.IdProfileInfo);
            var destroySession = TryGetDelegate<NvApiNative.DestroySessionDelegate>(query, NvApiNative.IdDestroySession);

            return Result<NvApiSession>.Ok(new NvApiSession(
                library,
                session,
                getBaseProfile,
                findProfile,
                createProfile,
                createApplication,
                findApplication,
                getSetting,
                setSetting,
                deleteSetting,
                saveSettings,
                profileInfo,
                destroySession,
                unload!));
        }
        catch (Exception exception) when (
            exception is EntryPointNotFoundException or ArgumentException or InvalidOperationException)
        {
            if (session != IntPtr.Zero)
            {
                TryDestroySession(library, session);
            }

            if (initialized)
            {
                TryUnload(unload);
            }

            NativeLibrary.Free(library);
            return Result<NvApiSession>.Fail(new AppError(ErrorCode.NvApiUnavailable, exception.Message));
        }
    }

    public IntPtr? FindProfileForExe(string executablePath)
    {
        var buffer = NvApiNative.CreateApplicationBuffer(executablePath);
        try
        {
            var result = _findApplication(_session, executablePath, out var profile, buffer);
            return result == 0 && profile != IntPtr.Zero ? profile : null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public string GetProfileName(IntPtr profile)
    {
        var buffer = NvApiNative.CreateProfileInfoBuffer();
        try
        {
            var result = _profileInfo(_session, profile, buffer);
            return result == 0 ? NvApiNative.ReadString(buffer, 4, 2048) : string.Empty;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public IntPtr? FindProfile(string name)
    {
        var result = _findProfile(_session, name, out var profile);
        if (result == 0)
        {
            return profile;
        }

        if (result == -163)
        {
            return null;
        }

        throw new NvApiException(result, "DRS_FindProfileByName");
    }

    public IntPtr CreateProfile(string name)
    {
        var buffer = NvApiNative.CreateProfileBuffer(name);
        try
        {
            var result = _createProfile(_session, buffer, out var profile);
            if (result != 0)
            {
                throw new NvApiException(result, "DRS_CreateProfile");
            }

            return profile;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public int CreateApplication(IntPtr profile, string executablePath)
    {
        var buffer = NvApiNative.CreateApplicationBuffer(executablePath);
        try
        {
            return _createApplication(_session, profile, buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public bool IsBaseProfile(IntPtr profile)
    {
        var result = _getBaseProfile(_session, out var baseProfile);
        return result == 0 && baseProfile == profile;
    }

    public uint? ReadSetting(IntPtr profile, uint settingId)
    {
        var buffer = NvApiNative.CreateSettingBuffer(settingId);
        try
        {
            var result = _getSetting(_session, profile, settingId, buffer, out _);
            return result == 0 ? NvApiNative.GetSettingValue(buffer) : null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public int SetDword(IntPtr profile, uint settingId, uint value)
    {
        var buffer = NvApiNative.CreateSettingBuffer(settingId);
        try
        {
            NvApiNative.SetSettingValue(buffer, value);
            return _setSetting(_session, profile, buffer, 0, 0);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public int DeleteSetting(IntPtr profile, uint settingId)
    {
        return _deleteSetting(_session, profile, settingId);
    }

    public void Save()
    {
        var result = _saveSettings(_session);
        if (result != 0)
        {
            throw new NvApiException(result, "DRS_SaveSettings");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_destroySession is not null)
        {
            try
            {
                _destroySession(_session);
            }
            catch (Exception)
            {
                // Best-effort cleanup.
            }
        }

        TryUnload(_unload);
        NativeLibrary.Free(_library);
    }

    private static T GetDelegate<T>(NvApiNative.QueryInterface query, uint id)
        where T : Delegate
    {
        var pointer = query(id);
        if (pointer == IntPtr.Zero)
        {
            throw new EntryPointNotFoundException($"NVAPI export 0x{id:X8} was not found.");
        }

        return Marshal.GetDelegateForFunctionPointer<T>(pointer);
    }

    private static T? TryGetDelegate<T>(NvApiNative.QueryInterface query, uint id)
        where T : Delegate
    {
        var pointer = query(id);
        return pointer == IntPtr.Zero ? null : Marshal.GetDelegateForFunctionPointer<T>(pointer);
    }

    private static Result<NvApiSession> FailAndFree(
        IntPtr library,
        ErrorCode code,
        string message)
    {
        NativeLibrary.Free(library);
        return Result<NvApiSession>.Fail(new AppError(code, message));
    }

    private static void TryUnload(NvApiNative.UnloadDelegate? unload)
    {
        try
        {
            unload?.Invoke();
        }
        catch (Exception)
        {
            // Best-effort cleanup.
        }
    }

    private static void TryDestroySession(IntPtr library, IntPtr session)
    {
        try
        {
            var queryPointer = NativeLibrary.GetExport(library, "nvapi_QueryInterface");
            var query = Marshal.GetDelegateForFunctionPointer<NvApiNative.QueryInterface>(queryPointer);
            var destroy = TryGetDelegate<NvApiNative.DestroySessionDelegate>(query, NvApiNative.IdDestroySession);
            destroy?.Invoke(session);
        }
        catch (Exception)
        {
            // Best-effort cleanup.
        }
    }
}

internal sealed class NvApiException : Exception
{
    public NvApiException(int resultCode, string operation)
        : base($"{operation} returned NVAPI status {resultCode}.")
    {
        ResultCode = resultCode;
    }

    public int ResultCode { get; }
}
