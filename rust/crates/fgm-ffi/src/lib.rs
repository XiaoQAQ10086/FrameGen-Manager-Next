#![allow(unsafe_code)]

use std::ffi::{CString, c_char};
use std::panic::{AssertUnwindSafe, catch_unwind};
use std::sync::LazyLock;

use fgm_core::operation::OperationPhase;
use fgm_domain::error::{AppError, ErrorCode};
use fgm_infra::library::{LibraryCache, LibraryScanner, LibraryStore};
use fgm_infra::paths::AppPaths;
use fgm_infra::platform::DriveRoots;
use serde_json::{Value, json};

mod commands;
mod commands_anr;
mod commands_dlss5;
mod commands_sm;

static VERSION: LazyLock<Vec<u8>> = LazyLock::new(|| {
    let mut bytes = fgm_domain::version::VERSION.as_bytes().to_vec();
    bytes.push(0);
    bytes
});

/// NUL-terminated stable keys indexed by `OperationPhase::wire_id`.
const PHASE_NAMES: [&[u8]; 7] = [
    b"planned\0",
    b"backingUp\0",
    b"writing\0",
    b"verifying\0",
    b"installed\0",
    b"rolledBack\0",
    b"partial\0",
];

/// Returns a process-lifetime, NUL-terminated UTF-8 version string.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_version() -> *const c_char {
    VERSION.as_ptr().cast()
}

/// Maps the numeric deployment phase used by the C ABI to its stable key.
///
/// Returns a null pointer for an unknown phase so the caller can surface
/// `ErrorCode::InvalidArgument` without allocating.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_operation_phase_name(phase: u32) -> *const c_char {
    match OperationPhase::from_wire(phase) {
        Ok(phase) => PHASE_NAMES[phase.wire_id() as usize].as_ptr().cast(),
        Err(_) => std::ptr::null(),
    }
}

/// Scans Steam, Epic and loose folders, updates `library.json`, and returns:
/// `{"ok":true,"games":[...]}` or `{"ok":false,"code":"...","context":[...]}`.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_library_scan_json() -> *mut c_char {
    execute(|| match scan_library() {
        Ok(games) => json!({ "ok": true, "games": games }),
        Err(error) => error_json(&error),
    })
}

/// Returns the cached library without scanning.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_library_list_json() -> *mut c_char {
    execute(|| match list_library() {
        Ok(games) => json!({ "ok": true, "games": games }),
        Err(error) => error_json(&error),
    })
}

/// Returns GPU name, driver version and route.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_gpu_json() -> *mut c_char {
    execute(|| match commands::gpu() {
        Ok(value) => value,
        Err(error) => error_json(&error),
    })
}

/// Returns the current app settings.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_settings_get_json() -> *mut c_char {
    execute(|| match commands::settings_get() {
        Ok(value) => value,
        Err(error) => error_json(&error),
    })
}

/// Replaces the app settings with a UTF-8 JSON object.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_settings_set_json(request: *const u8, length: usize) -> *mut c_char {
    execute_request(request, length, commands::settings_set)
}

/// Deploys a pack. `request` is a UTF-8 JSON object; returns a JSON envelope.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_deploy_json(request: *const u8, length: usize) -> *mut c_char {
    execute_request(request, length, commands::deploy)
}

/// Restores a deployment. `request` is a UTF-8 JSON object; returns a JSON envelope.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_restore_json(request: *const u8, length: usize) -> *mut c_char {
    execute_request(request, length, commands::restore)
}

/// Inspects a deployment. `request` is a UTF-8 JSON object; returns a JSON envelope.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_inspect_json(request: *const u8, length: usize) -> *mut c_char {
    execute_request(request, length, commands::inspect)
}

/// Inspects Smooth Motion for an executable.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_sm_inspect_json(request: *const u8, length: usize) -> *mut c_char {
    execute_request(request, length, commands_sm::sm_inspect)
}

/// Inspects Smooth Motion for a batch of executables.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_sm_inspect_many_json(request: *const u8, length: usize) -> *mut c_char {
    execute_request(request, length, commands_sm::sm_inspect_many)
}

/// Enables Smooth Motion (components + driver setting).
#[unsafe(no_mangle)]
pub extern "C" fn fgm_sm_enable_json(request: *const u8, length: usize) -> *mut c_char {
    execute_request(request, length, commands_sm::sm_enable)
}

/// Disables Smooth Motion (driver setting + component restore).
#[unsafe(no_mangle)]
pub extern "C" fn fgm_sm_disable_json(request: *const u8, length: usize) -> *mut c_char {
    execute_request(request, length, commands_sm::sm_disable)
}

/// Restores Smooth Motion driver values and component files.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_sm_restore_json(request: *const u8, length: usize) -> *mut c_char {
    execute_request(request, length, commands_sm::sm_restore)
}

/// Installs DLSS5 pack files and the offline ReShade proxy.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_dlss5_install_json(request: *const u8, length: usize) -> *mut c_char {
    execute_request(request, length, commands_dlss5::dlss5_install)
}

/// Returns the tail of the operation log.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_logs_tail_json(request: *const u8, length: usize) -> *mut c_char {
    execute_request(request, length, commands::logs_tail)
}

/// Returns the imported ANR package, if any.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_anr_current_json() -> *mut c_char {
    execute(|| match commands_anr::anr_current() {
        Ok(value) => value,
        Err(error) => error_json(&error),
    })
}

/// Imports an ANR zip, opening the native picker when no path is supplied.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_anr_import_json(request: *const u8, length: usize) -> *mut c_char {
    execute_request(request, length, commands_anr::anr_import)
}

/// Deploys the imported ANR package to a game directory.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_anr_deploy_json(request: *const u8, length: usize) -> *mut c_char {
    execute_request(request, length, commands_anr::anr_deploy)
}

/// Restores an ANR deployment.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_anr_restore_json(request: *const u8, length: usize) -> *mut c_char {
    execute_request(request, length, commands_anr::anr_restore)
}

/// Allocates `size` bytes for a request buffer. Free with `fgm_free_bytes`.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_alloc(size: usize) -> *mut u8 {
    if size == 0 {
        return std::ptr::null_mut();
    }
    let Ok(layout) = std::alloc::Layout::from_size_align(size, 1) else {
        return std::ptr::null_mut();
    };
    unsafe { std::alloc::alloc(layout) }
}

/// Frees a buffer returned by `fgm_alloc`.
///
/// # Safety
/// `pointer` and `size` must come from the matching `fgm_alloc` call.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn fgm_free_bytes(pointer: *mut u8, size: usize) {
    if pointer.is_null() || size == 0 {
        return;
    }
    if let Ok(layout) = std::alloc::Layout::from_size_align(size, 1) {
        unsafe { std::alloc::dealloc(pointer, layout) };
    }
}

/// Frees a string returned by an `fgm_*_json` function.
///
/// # Safety
/// `value` must come from one of this library's JSON functions and be freed once.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn fgm_string_free(value: *mut c_char) {
    if value.is_null() {
        return;
    }
    unsafe {
        drop(CString::from_raw(value));
    }
}

fn scan_library() -> Result<Vec<fgm_domain::library::GameEntry>, AppError> {
    let paths = AppPaths::create_default(None)?;
    paths.ensure_created()?;
    let roots = DriveRoots::roots();
    let games = LibraryScanner::scan(&roots);

    let store = LibraryStore::new(paths.library().join("library.json"));
    let mut cache = store.load()?;
    cache.schema_version = 1;
    cache.games = games;
    store.save(&cache)?;
    Ok(cache.games)
}

fn list_library() -> Result<Vec<fgm_domain::library::GameEntry>, AppError> {
    let paths = AppPaths::create_default(None)?;
    let store = LibraryStore::new(paths.library().join("library.json"));
    let cache = store.load().unwrap_or_else(|_| LibraryCache::default());
    Ok(cache.games)
}

fn execute(action: impl FnOnce() -> Value) -> *mut c_char {
    let value = catch_unwind(AssertUnwindSafe(action)).unwrap_or_else(|_| {
        json!({
            "ok": false,
            "code": "Internal",
            "context": [["reason", "ffi panic"]]
        })
    });
    to_c_string(value.to_string())
}

fn execute_request(
    request: *const u8,
    length: usize,
    action: fn(&str) -> Result<Value, AppError>,
) -> *mut c_char {
    execute(|| {
        let request = match unsafe { read_request(request, length) } {
            Ok(request) => request,
            Err(error) => return error_json(&error),
        };
        match action(&request) {
            Ok(value) => value,
            Err(error) => error_json(&error),
        }
    })
}

unsafe fn read_request(pointer: *const u8, length: usize) -> Result<String, AppError> {
    if pointer.is_null() || length == 0 {
        return Err(
            AppError::new(ErrorCode::InvalidArgument).with("reason", "request buffer is empty")
        );
    }
    let bytes = unsafe { std::slice::from_raw_parts(pointer, length) };
    std::str::from_utf8(bytes)
        .map(ToString::to_string)
        .map_err(|error| {
            AppError::new(ErrorCode::InvalidArgument).with("reason", error.to_string())
        })
}

fn error_json(error: &AppError) -> Value {
    json!({
        "ok": false,
        "code": format!("{:?}", error.code),
        "context": error.context
    })
}

fn to_c_string(value: String) -> *mut c_char {
    let sanitized = value.replace('\0', "");
    CString::new(sanitized).unwrap_or_default().into_raw()
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::ffi::CStr;

    #[test]
    fn phase_names_are_stable_keys() {
        for (id, expected) in [
            (0, "planned"),
            (1, "backingUp"),
            (2, "writing"),
            (3, "verifying"),
            (4, "installed"),
            (5, "rolledBack"),
            (6, "partial"),
        ] {
            let name = unsafe { CStr::from_ptr(fgm_operation_phase_name(id)) };
            assert_eq!(name.to_str().unwrap(), expected);
        }
    }

    #[test]
    fn unknown_phase_returns_null() {
        assert!(fgm_operation_phase_name(7).is_null());
    }

    #[test]
    fn version_is_nul_terminated() {
        let version = unsafe { CStr::from_ptr(fgm_version()) };
        assert_eq!(version.to_str().unwrap(), fgm_domain::version::VERSION);
    }
}
