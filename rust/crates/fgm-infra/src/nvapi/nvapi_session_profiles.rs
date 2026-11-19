use std::path::Path;
use std::ptr;

use fgm_domain::error::{AppError, ErrorCode};

use super::nvapi_native::{
    PROFILE_NAME_OFFSET, application_buffer, profile_buffer, profile_info_buffer, read_wide,
};
use super::nvapi_profile::NvApiProfile;
use super::nvapi_session::{NvApiSession, absolute};

impl NvApiSession {
    pub fn profile_name(&self, profile: NvApiProfile) -> Option<String> {
        let mut buffer = profile_info_buffer();
        let result = unsafe {
            (self.functions.profile_info)(self.session, profile.as_ptr(), buffer.as_mut_ptr())
        };
        if result != 0 {
            return None;
        }
        let name = read_wide(&buffer, PROFILE_NAME_OFFSET, 2_048);
        (!name.is_empty()).then_some(name)
    }

    pub fn create_profile(&self, name: &str) -> Result<NvApiProfile, AppError> {
        let mut buffer = profile_buffer(name);
        let mut profile = ptr::null_mut();
        let result = unsafe {
            (self.functions.create_profile)(self.session, buffer.as_mut_ptr(), &mut profile)
        };
        if result != 0 || profile.is_null() {
            return Err(call_failed("DRS_CreateProfile", result));
        }
        Ok(NvApiProfile::from_raw(profile))
    }

    pub fn create_application(
        &self,
        profile: NvApiProfile,
        executable: &Path,
    ) -> Result<(), AppError> {
        let executable = absolute(executable)?;
        let mut buffer = application_buffer(&executable);
        let result = unsafe {
            (self.functions.create_application)(self.session, profile.as_ptr(), buffer.as_mut_ptr())
        };
        if result != 0 {
            return Err(call_failed("DRS_CreateApplication", result));
        }
        Ok(())
    }
}

fn call_failed(operation: &str, status: i32) -> AppError {
    AppError::new(ErrorCode::NvApiCallFailed)
        .with("operation", operation)
        .with("status", status.to_string())
}

impl NvApiSession {
    pub fn delete_setting(&self, profile: NvApiProfile, setting_id: u32) -> Result<(), AppError> {
        let result =
            unsafe { (self.functions.delete_setting)(self.session, profile.as_ptr(), setting_id) };
        if result != 0 {
            return Err(call_failed("DRS_DeleteProfileSetting", result));
        }
        Ok(())
    }
}
