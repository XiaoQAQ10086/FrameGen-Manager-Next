use std::ffi::c_void;
use std::path::{Path, PathBuf};
use std::ptr;

use fgm_domain::error::{AppError, ErrorCode};

use super::nvapi_native::{
    APPLICATION_BUFFER_SIZE, APPLICATION_SLOTS, DestroySession, ID_CREATE_APPLICATION,
    ID_CREATE_PROFILE, ID_CREATE_SESSION, ID_DELETE_SETTING, ID_DESTROY_SESSION,
    ID_ENUM_APPLICATIONS, ID_ENUM_PROFILES, ID_FIND_APPLICATION, ID_FIND_PROFILE,
    ID_GET_BASE_PROFILE, ID_GET_SETTING, ID_INITIALIZE, ID_LOAD_SETTINGS, ID_PROFILE_INFO,
    ID_SAVE_SETTINGS, ID_SET_SETTING, ID_UNLOAD, NvApiFunctions, SMOOTH_MOTION_SETTING_ID,
    application_buffer, free_library, get_setting_value, initialize_application_slots, load_nvapi,
    query_fn, read_wide, set_setting_value, setting_buffer, wide,
};
use super::nvapi_profile::NvApiProfile;

/// Open NVIDIA driver session used by Smooth Motion.
///
/// Closing destroys the DRS session and unloads NVAPI; `NvAPI_Uninitialize` is
/// never called because it is not exported / not safe to use here.
pub struct NvApiSession {
    library: *mut c_void,
    pub(super) session: *mut c_void,
    pub(super) functions: NvApiFunctions,
}

impl NvApiSession {
    pub fn open() -> Result<Self, AppError> {
        unsafe { Self::open_inner() }
    }

    unsafe fn open_inner() -> Result<Self, AppError> {
        let Some((library, query)) = (unsafe { load_nvapi() }) else {
            return Err(unavailable("nvapi64.dll could not be loaded"));
        };

        let Some(initialize) =
            (unsafe { query_fn::<unsafe extern "C" fn() -> i32>(query, ID_INITIALIZE) })
        else {
            unsafe { free_library(library) };
            return Err(unavailable("NvAPI_Initialize export is missing"));
        };
        let Some(unload) = (unsafe { query_fn::<unsafe extern "C" fn() -> i32>(query, ID_UNLOAD) })
        else {
            unsafe { free_library(library) };
            return Err(unavailable("NvAPI_Unload export is missing"));
        };
        let Some(create_session) = (unsafe {
            query_fn::<unsafe extern "C" fn(*mut *mut c_void) -> i32>(query, ID_CREATE_SESSION)
        }) else {
            unsafe { free_library(library) };
            return Err(unavailable("DRS_CreateSession export is missing"));
        };
        let Some(load_settings) = (unsafe {
            query_fn::<unsafe extern "C" fn(*mut c_void) -> i32>(query, ID_LOAD_SETTINGS)
        }) else {
            unsafe { free_library(library) };
            return Err(unavailable("DRS_LoadSettings export is missing"));
        };
        let Some(get_base_profile) = (unsafe { query_fn(query, ID_GET_BASE_PROFILE) }) else {
            unsafe { free_library(library) };
            return Err(unavailable("DRS_GetBaseProfile export is missing"));
        };
        let Some(find_profile) = (unsafe { query_fn(query, ID_FIND_PROFILE) }) else {
            unsafe { free_library(library) };
            return Err(unavailable("DRS_FindProfileByName export is missing"));
        };
        let Some(find_application) = (unsafe { query_fn(query, ID_FIND_APPLICATION) }) else {
            unsafe { free_library(library) };
            return Err(unavailable("DRS_FindApplicationByName export is missing"));
        };
        let Some(create_profile) = (unsafe { query_fn(query, ID_CREATE_PROFILE) }) else {
            unsafe { free_library(library) };
            return Err(unavailable("DRS_CreateProfile export is missing"));
        };
        let Some(create_application) = (unsafe { query_fn(query, ID_CREATE_APPLICATION) }) else {
            unsafe { free_library(library) };
            return Err(unavailable("DRS_CreateApplication export is missing"));
        };
        let Some(profile_info) = (unsafe { query_fn(query, ID_PROFILE_INFO) }) else {
            unsafe { free_library(library) };
            return Err(unavailable("DRS_GetProfileInfo export is missing"));
        };
        let Some(delete_setting) = (unsafe { query_fn(query, ID_DELETE_SETTING) }) else {
            unsafe { free_library(library) };
            return Err(unavailable("DRS_DeleteProfileSetting export is missing"));
        };
        let Some(get_setting) = (unsafe { query_fn(query, ID_GET_SETTING) }) else {
            unsafe { free_library(library) };
            return Err(unavailable("DRS_GetSetting export is missing"));
        };
        let Some(set_setting) = (unsafe { query_fn(query, ID_SET_SETTING) }) else {
            unsafe { free_library(library) };
            return Err(unavailable("DRS_SetSetting export is missing"));
        };
        let Some(save_settings) = (unsafe { query_fn(query, ID_SAVE_SETTINGS) }) else {
            unsafe { free_library(library) };
            return Err(unavailable("DRS_SaveSettings export is missing"));
        };
        let Some(enum_profiles) = (unsafe { query_fn(query, ID_ENUM_PROFILES) }) else {
            unsafe { free_library(library) };
            return Err(unavailable("DRS_EnumProfiles export is missing"));
        };
        let Some(enum_applications) = (unsafe { query_fn(query, ID_ENUM_APPLICATIONS) }) else {
            unsafe { free_library(library) };
            return Err(unavailable("DRS_EnumApplications export is missing"));
        };
        let destroy_session: Option<DestroySession> =
            unsafe { query_fn(query, ID_DESTROY_SESSION) };

        let initialize_result = unsafe { initialize() };
        if initialize_result != 0 {
            unsafe { free_library(library) };
            return Err(call_failed("NvAPI_Initialize", initialize_result));
        }

        let mut session = ptr::null_mut();
        let create_result = unsafe { create_session(&mut session) };
        if create_result != 0 || session.is_null() {
            unsafe {
                let _ = unload();
                free_library(library);
            }
            return Err(call_failed("DRS_CreateSession", create_result));
        }

        let load_result = unsafe { load_settings(session) };
        if load_result != 0 {
            unsafe {
                if let Some(destroy) = destroy_session {
                    let _ = destroy(session);
                }
                let _ = unload();
                free_library(library);
            }
            return Err(call_failed("DRS_LoadSettings", load_result));
        }

        Ok(Self {
            library,
            session,
            functions: NvApiFunctions {
                unload,
                get_base_profile,
                find_profile,
                create_profile,
                find_application,
                create_application,
                profile_info,
                get_setting,
                set_setting,
                delete_setting,
                save_settings,
                destroy_session,
                enum_profiles,
                enum_applications,
            },
        })
    }

    pub fn base_profile(&self) -> Result<NvApiProfile, AppError> {
        let mut profile = ptr::null_mut();
        let result = unsafe { (self.functions.get_base_profile)(self.session, &mut profile) };
        if result != 0 || profile.is_null() {
            return Err(call_failed("DRS_GetBaseProfile", result));
        }
        Ok(NvApiProfile::from_raw(profile))
    }

    pub fn find_profile(&self, name: &str) -> Result<Option<NvApiProfile>, AppError> {
        let name = wide(name);
        let mut profile = ptr::null_mut();
        let result =
            unsafe { (self.functions.find_profile)(self.session, name.as_ptr(), &mut profile) };

        if result == 0 && !profile.is_null() {
            return Ok(Some(NvApiProfile::from_raw(profile)));
        }
        if result == -163 {
            return Ok(None);
        }
        Err(call_failed("DRS_FindProfileByName", result))
    }

    pub fn find_profile_for_exe(
        &self,
        executable: &Path,
    ) -> Result<Option<NvApiProfile>, AppError> {
        let executable = absolute(executable)?;
        let name = executable
            .file_name()
            .map_or_else(String::new, |value| value.to_string_lossy().into_owned());
        let mut buffer = application_buffer(&executable);

        if let Some(profile) = self.find_application(&name, &mut buffer) {
            return Ok(Some(profile));
        }

        let full_path = executable.to_string_lossy();
        Ok(self.find_application(&full_path, &mut buffer))
    }

    /// Every driver profile that binds this executable. NVIDIA can store more
    /// than one profile for the same EXE, so writes must target all of them.
    pub fn find_profiles_for_exe(&self, executable: &Path) -> Result<Vec<NvApiProfile>, AppError> {
        let executable = absolute(executable)?;
        let mut matches = Vec::new();
        let mut seen = std::collections::HashSet::new();

        if let Some(profile) = self.find_profile_for_exe(&executable)?
            && !self.is_base_profile(profile)
            && seen.insert(profile)
        {
            matches.push(profile);
        }

        let mut buffer = vec![0_u8; APPLICATION_SLOTS * APPLICATION_BUFFER_SIZE];
        let mut index = 0_u32;
        loop {
            let mut profile = ptr::null_mut();
            let result =
                unsafe { (self.functions.enum_profiles)(self.session, index, &mut profile) };
            if result != 0 || profile.is_null() {
                break;
            }
            let profile = NvApiProfile::from_raw(profile);
            if !self.is_base_profile(profile)
                && seen.insert(profile)
                && self.profile_binds_executable(profile, &executable, &mut buffer)
            {
                matches.push(profile);
            }
            index += 1;
        }

        Ok(matches)
    }

    pub fn is_base_profile(&self, profile: NvApiProfile) -> bool {
        self.base_profile().is_ok_and(|base| base == profile)
    }

    pub fn read_setting(
        &self,
        profile: NvApiProfile,
        setting_id: u32,
    ) -> Result<Option<u32>, AppError> {
        let mut buffer = setting_buffer(setting_id);
        let mut extra = 0_u32;
        let result = unsafe {
            (self.functions.get_setting)(
                self.session,
                profile.as_ptr(),
                setting_id,
                buffer.as_mut_ptr(),
                &mut extra,
            )
        };

        Ok((result == 0).then(|| get_setting_value(&buffer)))
    }

    pub fn read_smooth_motion(&self, profile: NvApiProfile) -> Result<Option<u32>, AppError> {
        self.read_setting(profile, SMOOTH_MOTION_SETTING_ID)
    }

    pub fn set_dword(
        &self,
        profile: NvApiProfile,
        setting_id: u32,
        value: u32,
    ) -> Result<(), AppError> {
        let mut buffer = setting_buffer(setting_id);
        set_setting_value(&mut buffer, value);
        let result = unsafe {
            (self.functions.set_setting)(self.session, profile.as_ptr(), buffer.as_mut_ptr(), 0, 0)
        };
        if result != 0 {
            return Err(call_failed("DRS_SetSetting", result));
        }
        Ok(())
    }

    pub fn save(&self) -> Result<(), AppError> {
        let result = unsafe { (self.functions.save_settings)(self.session) };
        if result != 0 {
            return Err(call_failed("DRS_SaveSettings", result));
        }
        Ok(())
    }

    fn find_application(&self, name: &str, buffer: &mut [u8]) -> Option<NvApiProfile> {
        let name = wide(name);
        let mut profile = ptr::null_mut();
        let result = unsafe {
            (self.functions.find_application)(
                self.session,
                name.as_ptr(),
                &mut profile,
                buffer.as_mut_ptr(),
            )
        };
        (result == 0 && !profile.is_null()).then(|| NvApiProfile::from_raw(profile))
    }

    fn profile_binds_executable(
        &self,
        profile: NvApiProfile,
        executable: &Path,
        buffer: &mut [u8],
    ) -> bool {
        initialize_application_slots(buffer);
        let mut count = APPLICATION_SLOTS as u32;
        let result = unsafe {
            (self.functions.enum_applications)(
                self.session,
                profile.as_ptr(),
                0,
                &mut count,
                buffer.as_mut_ptr(),
            )
        };
        if result != 0 {
            return false;
        }

        let limit = (count as usize).min(APPLICATION_SLOTS);
        for index in 0..limit {
            let slot = index * APPLICATION_BUFFER_SIZE;
            for offset in [8, 4_104, 8_200, 12_296] {
                let stored = read_wide(buffer, slot + offset, 2_048);
                if matches_executable(&stored, executable) {
                    return true;
                }
            }
        }
        false
    }
}

impl Drop for NvApiSession {
    fn drop(&mut self) {
        unsafe {
            if let Some(destroy) = self.functions.destroy_session {
                let _ = destroy(self.session);
            }
            let _ = (self.functions.unload)();
            free_library(self.library);
        }
    }
}

pub(super) fn absolute(path: &Path) -> Result<PathBuf, AppError> {
    std::path::absolute(path).map_err(|error| {
        AppError::new(ErrorCode::InvalidPath)
            .with("path", path.display().to_string())
            .with("reason", error.to_string())
    })
}

fn matches_executable(stored: &str, executable: &Path) -> bool {
    let stored = stored.replace('/', "\\").trim().to_string();
    if stored.is_empty() {
        return false;
    }

    let target = executable.to_string_lossy().replace('/', "\\");
    if stored.eq_ignore_ascii_case(&target) {
        return true;
    }

    let Some(name) = target.rsplit('\\').next() else {
        return false;
    };
    if !stored.contains('\\') {
        return stored.eq_ignore_ascii_case(name);
    }

    !Path::new(&stored).is_absolute()
        && target
            .to_lowercase()
            .ends_with(&format!("\\{}", stored.to_lowercase()))
}

fn unavailable(reason: &str) -> AppError {
    AppError::new(ErrorCode::NvApiUnavailable).with("reason", reason)
}

fn call_failed(operation: &str, status: i32) -> AppError {
    AppError::new(ErrorCode::NvApiCallFailed)
        .with("operation", operation)
        .with("status", status.to_string())
}
