use std::ffi::c_char;

use fgm_domain::error::AppError;
use fgm_infra::paths::AppPaths;
use serde_json::{Value, json};

/// Removes the current log files.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_logs_clear_json() -> *mut c_char {
    super::execute(|| match logs_clear() {
        Ok(value) => value,
        Err(error) => super::error_json(&error),
    })
}

pub(crate) fn logs_clear() -> Result<Value, AppError> {
    let paths = AppPaths::create_default(None)?;
    let directory = paths.logs();
    let mut removed = 0_usize;
    if let Ok(entries) = std::fs::read_dir(&directory) {
        for entry in entries.flatten() {
            let path = entry.path();
            if path.is_file() && std::fs::remove_file(&path).is_ok() {
                removed += 1;
            }
        }
    }
    Ok(json!({ "ok": true, "removed": removed }))
}
