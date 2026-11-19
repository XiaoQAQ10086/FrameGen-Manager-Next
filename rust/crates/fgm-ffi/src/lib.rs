#![allow(unsafe_code)]

use std::ffi::{CString, c_char};
use std::panic::{AssertUnwindSafe, catch_unwind};
use std::sync::LazyLock;

use fgm_core::operation::OperationPhase;
use fgm_domain::error::{AppError, ErrorCode};
use fgm_infra::library::LibraryService;
use fgm_infra::paths::AppPaths;
use serde_json::{Value, json};

mod commands;
mod commands_anr;
mod commands_diagnostics;
mod commands_dlss5;
mod commands_gpu_spoof;
mod commands_library;
mod commands_sm;
mod commands_update;
mod commands_window;

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

/// Adds a manually selected executable to the library.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_library_add_manual_json(request: *const u8, length: usize) -> *mut c_char {
    execute_request(request, length, commands_library::add_manual)
}

/// Launches a library game through its launcher or render executable.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_launch_game_json(request: *const u8, length: usize) -> *mut c_char {
    execute_request(request, length, commands_library::launch)
}

/// Opens the native picker and returns the chosen executable path.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_pick_executable_json() -> *mut c_char {
    execute(|| match commands_library::pick_executable() {
        Ok(value) => value,
        Err(error) => error_json(&error),
    })
}

/// Returns manifest-only deployment badges for every library game.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_game_modes_json(request: *const u8, length: usize) -> *mut c_char {
    execute_request(request, length, commands_library::game_modes)
}

/// Returns the cached cover path for a Steam game, downloading it on demand.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_cover_json(request: *const u8, length: usize) -> *mut c_char {
    execute_request(request, length, commands_library::cover)
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

/// Inspects DLSS5 state, ReShade presence and legacy residue.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_dlss5_inspect_json(request: *const u8, length: usize) -> *mut c_char {
    execute_request(request, length, commands_dlss5::dlss5_inspect)
}

/// Deletes legacy RenoDX/DLSS5 files from a game directory.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_dlss5_clean_legacy_json(request: *const u8, length: usize) -> *mut c_char {
    execute_request(request, length, commands_dlss5::dlss5_clean_legacy)
}

/// Sets the ReShade overlay hotkey for an installed DLSS5 game.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_dlss5_set_hotkey_json(request: *const u8, length: usize) -> *mut c_char {
    execute_request(request, length, commands_dlss5::dlss5_set_hotkey)
}

/// Removes the DLSS5 deployment, restoring files that predated it.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_dlss5_uninstall_json(request: *const u8, length: usize) -> *mut c_char {
    execute_request(request, length, commands_dlss5::dlss5_uninstall)
}

/// Checks the update manifest against the running version.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_update_check_json() -> *mut c_char {
    execute(|| match commands_update::update_check() {
        Ok(value) => value,
        Err(error) => error_json(&error),
    })
}

/// Downloads the latest installer without launching it.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_update_download_json() -> *mut c_char {
    execute(|| match commands_update::update_download() {
        Ok(value) => value,
        Err(error) => error_json(&error),
    })
}

/// Launches a verified installer from the update staging directory.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_update_install_json(request: *const u8, length: usize) -> *mut c_char {
    execute_request(request, length, commands_update::update_install)
}

/// Opens an http(s) URL with the system handler.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_update_open_json(request: *const u8, length: usize) -> *mut c_char {
    execute_request(request, length, commands_update::update_open)
}

/// Saves a diagnostics bundle through the native save dialog.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_diagnostics_export_json(request: *const u8, length: usize) -> *mut c_char {
    execute_request(request, length, commands_diagnostics::export)
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

/// Downloads the latest ANR full package and imports it.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_anr_download_json(request: *const u8, length: usize) -> *mut c_char {
    execute_request(request, length, commands_anr::anr_download)
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
    LibraryService::scan(&paths)
}

fn list_library() -> Result<Vec<fgm_domain::library::GameEntry>, AppError> {
    let paths = AppPaths::create_default(None)?;
    Ok(LibraryService::list(&paths))
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
mod tests;
