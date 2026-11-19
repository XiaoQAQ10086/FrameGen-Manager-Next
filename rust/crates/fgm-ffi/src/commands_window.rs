use std::ffi::c_char;

use fgm_infra::paths::AppPaths;
use fgm_infra::platform::WindowStateService;
use serde_json::json;

use super::{error_json, execute};

/// Restores the saved main-window bounds.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_window_restore_json() -> *mut c_char {
    execute(|| {
        match AppPaths::create_default(None).and_then(|paths| WindowStateService::restore(&paths)) {
            Ok(applied) => json!({ "ok": true, "applied": applied }),
            Err(error) => error_json(&error),
        }
    })
}

/// Saves the current main-window bounds.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_window_save_json() -> *mut c_char {
    execute(|| {
        match AppPaths::create_default(None).and_then(|paths| WindowStateService::save(&paths)) {
            Ok(()) => json!({ "ok": true }),
            Err(error) => error_json(&error),
        }
    })
}
