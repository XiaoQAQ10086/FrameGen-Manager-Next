use std::ffi::c_char;

use fgm_domain::error::{AppError, ErrorCode};
use fgm_infra::gpu_spoof::{DisplayAdapter, GpuRestoreMode, GpuSpoofInspection, GpuSpoofService};
use serde::Deserialize;
use serde_json::{Value, json};

use super::commands::{data_paths, parse};
use super::{error_json, execute, execute_request};

pub(crate) fn inspect() -> Result<Value, AppError> {
    let paths = fgm_infra::paths::AppPaths::create_default(None)?;
    paths.ensure_created()?;
    let inspection = GpuSpoofService::inspect(&paths)?;
    Ok(inspection_reply(&inspection))
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct ApplyRequest {
    adapter_index: usize,
    preset: String,
    #[serde(default)]
    data_dir: Option<String>,
}

pub(crate) fn apply(request: &str) -> Result<Value, AppError> {
    let request: ApplyRequest = parse(request)?;
    let paths = data_paths(request.data_dir.as_deref())?;
    paths.ensure_created()?;
    let adapter = GpuSpoofService::apply(&paths, request.adapter_index, &request.preset)?;
    Ok(json!({ "ok": true, "adapter": adapter_json(0, &adapter) }))
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct RestoreRequest {
    adapter_index: usize,
    mode: String,
    #[serde(default)]
    data_dir: Option<String>,
}

pub(crate) fn restore(request: &str) -> Result<Value, AppError> {
    let request: RestoreRequest = parse(request)?;
    let mode = match request.mode.as_str() {
        "driver" => GpuRestoreMode::Driver,
        "backup" => GpuRestoreMode::Backup,
        _ => {
            return Err(
                AppError::new(ErrorCode::InvalidArgument).with("mode", request.mode.clone())
            );
        }
    };
    let paths = data_paths(request.data_dir.as_deref())?;
    paths.ensure_created()?;
    let adapter = GpuSpoofService::restore(&paths, request.adapter_index, mode)?;
    Ok(json!({ "ok": true, "adapter": adapter_json(0, &adapter) }))
}

fn inspection_reply(inspection: &GpuSpoofInspection) -> Value {
    json!({
        "ok": true,
        "elevated": inspection.elevated,
        "adapters": inspection.adapters.iter().enumerate()
            .map(|(index, adapter)| adapter_json(index, adapter))
            .collect::<Vec<Value>>(),
        "presets": inspection.presets,
        "applied": inspection.applied.as_ref().map(|manifest| json!({
            "appliedName": manifest.applied_name,
            "originalDeviceDesc": manifest.original_device_desc,
            "driverName": manifest.driver_name,
        })),
    })
}

fn adapter_json(index: usize, adapter: &DisplayAdapter) -> Value {
    json!({
        "index": index,
        "displayName": adapter.display_name,
        "driverName": adapter.driver_name,
        "deviceDesc": adapter.device_desc,
        "instancePath": adapter.instance_path,
    })
}

/// Inspects NVIDIA display adapters and the GPU-name backup.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_gpu_spoof_inspect_json() -> *mut c_char {
    execute(|| match inspect() {
        Ok(value) => value,
        Err(error) => error_json(&error),
    })
}

/// Writes a whitelisted adapter name after backing up the original.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_gpu_spoof_apply_json(request: *const u8, length: usize) -> *mut c_char {
    execute_request(request, length, apply)
}

/// Restores the driver name or the original `DeviceDesc`.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_gpu_spoof_restore_json(request: *const u8, length: usize) -> *mut c_char {
    execute_request(request, length, restore)
}
