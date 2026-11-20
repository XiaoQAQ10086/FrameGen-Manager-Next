//! Rust-facing command dispatcher used by the napi-rs bridge.
//!
//! It forwards to the same C ABI entry points as the Flutter bridge so both
//! frontends share one implementation and one JSON envelope contract.

use std::ffi::{CStr, CString};

use crate::commands_dlss5::fgm_dlss5_process_hotkey_queue_json;
use crate::commands_gpu_spoof::{
    fgm_gpu_spoof_apply_json, fgm_gpu_spoof_inspect_json, fgm_gpu_spoof_restore_json,
};
use crate::commands_library::{
    fgm_library_forget_json, fgm_library_remove_json, fgm_library_removed_json,
    fgm_library_restore_removed_json, fgm_pick_executable_json, fgm_proxy_status_json,
};
use crate::commands_logs::fgm_logs_clear_json;
use crate::commands_operation::{fgm_operation_cancel_json, fgm_operation_progress_json};
use crate::commands_system::{
    fgm_open_folder_json, fgm_open_hags_json, fgm_open_path_json, fgm_paths_json,
    fgm_restart_computer_json,
};
use crate::commands_window::{fgm_window_restore_json, fgm_window_save_json};

type JsonPointer = *mut std::ffi::c_char;

fn take(pointer: JsonPointer) -> Result<String, String> {
    if pointer.is_null() {
        return Err("ffi returned a null pointer".to_string());
    }
    let text = unsafe { CStr::from_ptr(pointer) }
        .to_string_lossy()
        .into_owned();
    unsafe { crate::fgm_string_free(pointer) };
    Ok(text)
}

type RequestFn = extern "C" fn(*const u8, usize) -> JsonPointer;

fn request(call: RequestFn, payload: &str) -> Result<String, String> {
    let buffer = CString::new(payload).map_err(|error| error.to_string())?;
    let length = buffer.as_bytes().len();
    take(call(buffer.as_ptr() as *const u8, length))
}

fn payload(payload: Option<&str>) -> &str {
    payload.unwrap_or("{}")
}

/// Dispatches `name` to the matching FFI entry and returns its JSON envelope.
pub fn invoke(name: &str, body: Option<&str>) -> Result<String, String> {
    match name {
        "version" => Ok(fgm_domain::version::VERSION.to_string()),
        "library.list" => take(crate::fgm_library_list_json()),
        "library.scan" => take(crate::fgm_library_scan_json()),
        "library.removed" => take(fgm_library_removed_json()),
        "library.addManual" => request(crate::fgm_library_add_manual_json, payload(body)),
        "library.remove" => request(fgm_library_remove_json, payload(body)),
        "library.restoreRemoved" => request(fgm_library_restore_removed_json, payload(body)),
        "library.forget" => request(fgm_library_forget_json, payload(body)),
        "pickExecutable" => take(fgm_pick_executable_json()),
        "cover" => request(crate::fgm_cover_json, payload(body)),
        "gameModes" => request(crate::fgm_game_modes_json, payload(body)),
        "launch" => request(crate::fgm_launch_game_json, payload(body)),
        "proxyStatus" => request(fgm_proxy_status_json, payload(body)),
        "gpu" => take(crate::fgm_gpu_json()),
        "paths" => take(fgm_paths_json()),
        "settings.get" => take(crate::fgm_settings_get_json()),
        "settings.set" => request(crate::fgm_settings_set_json, payload(body)),
        "logs.tail" => request(crate::fgm_logs_tail_json, payload(body)),
        "logs.clear" => take(fgm_logs_clear_json()),
        "diagnostics.export" => request(crate::fgm_diagnostics_export_json, payload(body)),
        "openFolder" => request(fgm_open_folder_json, payload(body)),
        "openPath" => request(fgm_open_path_json, payload(body)),
        "openHags" => take(fgm_open_hags_json()),
        "restartComputer" => take(fgm_restart_computer_json()),
        "deploy" => request(crate::fgm_deploy_json, payload(body)),
        "restore" => request(crate::fgm_restore_json, payload(body)),
        "inspect" => request(crate::fgm_inspect_json, payload(body)),
        "sm.inspect" => request(crate::fgm_sm_inspect_json, payload(body)),
        "sm.inspectMany" => request(crate::fgm_sm_inspect_many_json, payload(body)),
        "sm.enable" => request(crate::fgm_sm_enable_json, payload(body)),
        "sm.disable" => request(crate::fgm_sm_disable_json, payload(body)),
        "sm.restore" => request(crate::fgm_sm_restore_json, payload(body)),
        "dlss5.install" => request(crate::fgm_dlss5_install_json, payload(body)),
        "dlss5.inspect" => request(crate::fgm_dlss5_inspect_json, payload(body)),
        "dlss5.restore" => request(crate::fgm_dlss5_restore_json, payload(body)),
        "dlss5.uninstall" => request(crate::fgm_dlss5_uninstall_json, payload(body)),
        "dlss5.cleanLegacy" => request(crate::fgm_dlss5_clean_legacy_json, payload(body)),
        "dlss5.setHotkey" => request(crate::fgm_dlss5_set_hotkey_json, payload(body)),
        "dlss5.processHotkeyQueue" => take(fgm_dlss5_process_hotkey_queue_json()),
        "anr.current" => take(crate::fgm_anr_current_json()),
        "anr.import" => request(crate::fgm_anr_import_json, payload(body)),
        "anr.download" => request(crate::fgm_anr_download_json, payload(body)),
        "anr.deploy" => request(crate::fgm_anr_deploy_json, payload(body)),
        "anr.restore" => request(crate::fgm_anr_restore_json, payload(body)),
        "gpuSpoof.inspect" => take(fgm_gpu_spoof_inspect_json()),
        "gpuSpoof.apply" => request(fgm_gpu_spoof_apply_json, payload(body)),
        "gpuSpoof.restore" => request(fgm_gpu_spoof_restore_json, payload(body)),
        "update.check" => take(crate::fgm_update_check_json()),
        "update.download" => take(crate::fgm_update_download_json()),
        "update.install" => request(crate::fgm_update_install_json, payload(body)),
        "update.open" => request(crate::fgm_update_open_json, payload(body)),
        "operation.progress" => take(fgm_operation_progress_json()),
        "operation.cancel" => take(fgm_operation_cancel_json()),
        "window.restore" => take(fgm_window_restore_json()),
        "window.save" => take(fgm_window_save_json()),
        other => Err(format!("unknown command: {other}")),
    }
}
