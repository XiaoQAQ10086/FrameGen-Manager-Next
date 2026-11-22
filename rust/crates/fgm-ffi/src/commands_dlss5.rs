use std::ffi::c_char;
use std::path::{Path, PathBuf};

use fgm_domain::error::{AppError, ErrorCode};
use fgm_domain::gpu::GpuRoute;
use fgm_domain::library::GraphicsApi;
use fgm_domain::version::VERSION;
use fgm_infra::dlss5::Dlss5Service;
use fgm_infra::gpu::GpuDetector;
use fgm_infra::pack_json::PackJson;
use fgm_infra::packs::PackLocator;
use serde::Deserialize;
use serde_json::{Value, json};

use super::commands::{absolute, data_paths, parse};
use super::{error_json, execute};

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct Dlss5InstallRequest {
    #[serde(default)]
    pack_id: Option<String>,
    #[serde(default)]
    pack_dir: Option<String>,
    exe_path: String,
    target_dir: String,
    api: String,
    #[serde(default)]
    proxy: Option<String>,
    #[serde(default)]
    hotkey: Option<u32>,
    #[serde(default)]
    app_version: Option<String>,
    #[serde(default)]
    data_dir: Option<String>,
}

pub(crate) fn dlss5_install(request: &str) -> Result<Value, AppError> {
    let request: Dlss5InstallRequest = parse(request)?;
    let pack_directory = resolve_pack(request.pack_id, request.pack_dir)?
        .or_else(|| PackLocator::find(DEFAULT_PACK))
        .ok_or_else(|| AppError::new(ErrorCode::PackNotFound).with("packId", DEFAULT_PACK))?;
    let executable = absolute(Path::new(&request.exe_path))?;
    let target_directory = absolute(Path::new(&request.target_dir))?;
    let paths = data_paths(request.data_dir.as_deref())?;
    paths.ensure_created()?;
    let gpu = GpuDetector::detect_cached(&paths).ok_or_else(|| {
        AppError::new(ErrorCode::GpuUnsupported).with("reason", "NVIDIA GPU not detected")
    })?;
    if !matches!(gpu.route, GpuRoute::Sm86 | GpuRoute::Sm75) {
        return Err(AppError::new(ErrorCode::GpuUnsupported)
            .with("route", format!("{:?}", gpu.route))
            .with("reason", "DLSS5 requires an RTX 20 or 30 GPU"));
    }
    let api = parse_api(&request.api)?;
    let app_version = request.app_version.unwrap_or_else(|| VERSION.to_string());
    let operation = super::commands_operation::begin("dlss5Install");
    let result = Dlss5Service::install(
        &pack_directory,
        &executable,
        &target_directory,
        api,
        &app_version,
        &paths,
        &operation.cancelled,
        Some(operation.as_ref()),
        request.proxy.as_deref(),
    );
    super::commands_operation::finish();
    let report = result?;
    if let Some(hotkey) = request.hotkey {
        let _ = Dlss5Service::set_hotkey(&target_directory, hotkey, Some(&executable), &paths);
    }
    super::commands::log_event(
        &paths,
        "info",
        "dlss5 install",
        &[
            ("target", &request.target_dir),
            ("files", &report.files_written.to_string()),
        ],
    );

    Ok(json!({
        "ok": true,
        "filesWritten": report.files_written,
        "proxyModule": report.proxy_module,
    }))
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct Dlss5InspectRequest {
    target_dir: String,
    #[serde(default)]
    pack_id: Option<String>,
    #[serde(default)]
    pack_dir: Option<String>,
    #[serde(default)]
    data_dir: Option<String>,
}

pub(crate) fn dlss5_inspect(request: &str) -> Result<Value, AppError> {
    let request: Dlss5InspectRequest = parse(request)?;
    let target_directory = absolute(Path::new(&request.target_dir))?;
    let pack = match resolve_pack(request.pack_id, request.pack_dir)? {
        Some(directory) => Some(PackJson::load(&directory.join("pack.json"))?),
        // The pack is optional for inspection: outdated detection needs it, but
        // a machine without bundled packs must still report the manifest state.
        None => PackLocator::find(DEFAULT_PACK)
            .map(|directory| PackJson::load(&directory.join("pack.json")))
            .transpose()?,
    };
    let paths = data_paths(request.data_dir.as_deref())?;
    let inspection = Dlss5Service::inspect(&target_directory, pack.as_ref(), &paths)?;

    Ok(json!({
        "ok": true,
        "state": inspection.state,
        "hasManifest": inspection.has_manifest,
        "packVersion": inspection.pack_version,
        "proxyModule": inspection.proxy_module,
        "reShadePresent": inspection.re_shade_present,
        "hotkeyVk": inspection.hotkey_vk,
        "legacyFiles": inspection.legacy_files,
        "details": inspection.details,
    }))
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct Dlss5CleanRequest {
    target_dir: String,
}

pub(crate) fn dlss5_clean_legacy(request: &str) -> Result<Value, AppError> {
    let request: Dlss5CleanRequest = parse(request)?;
    let target_directory = absolute(Path::new(&request.target_dir))?;
    let removed = Dlss5Service::clean_legacy(&target_directory)?;
    Ok(json!({ "ok": true, "removed": removed }))
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct Dlss5HotkeyRequest {
    target_dir: String,
    virtual_key: u32,
    #[serde(default)]
    exe_path: Option<String>,
    #[serde(default)]
    data_dir: Option<String>,
}

pub(crate) fn dlss5_set_hotkey(request: &str) -> Result<Value, AppError> {
    let request: Dlss5HotkeyRequest = parse(request)?;
    let target_directory = absolute(Path::new(&request.target_dir))?;
    let paths = data_paths(request.data_dir.as_deref())?;
    let executable = request
        .exe_path
        .as_deref()
        .map(Path::new)
        .map(absolute)
        .transpose()?;
    let outcome = Dlss5Service::set_hotkey(
        &target_directory,
        request.virtual_key,
        executable.as_deref(),
        &paths,
    )?;
    Ok(json!({
        "ok": true,
        "hotkeyVk": request.virtual_key,
        "queued": outcome == fgm_infra::dlss5::HotkeyOutcome::Queued,
        "deferred": outcome == fgm_infra::dlss5::HotkeyOutcome::Deferred,
    }))
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct Dlss5RestoreRequest {
    target_dir: String,
    #[serde(default)]
    data_dir: Option<String>,
}

pub(crate) fn dlss5_restore(request: &str) -> Result<Value, AppError> {
    let request: Dlss5RestoreRequest = parse(request)?;
    let target_directory = absolute(Path::new(&request.target_dir))?;
    let paths = data_paths(request.data_dir.as_deref())?;
    paths.ensure_created()?;
    let report = Dlss5Service::restore_keeping_reshade(&target_directory, &paths)?;
    Ok(json!({
        "ok": true,
        "restored": report.restored_files,
        "removed": report.removed_files,
        "conflicts": report.conflicts,
        "warnings": report.warnings,
    }))
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct Dlss5UninstallRequest {
    target_dir: String,
    #[serde(default)]
    data_dir: Option<String>,
}

pub(crate) fn dlss5_uninstall(request: &str) -> Result<Value, AppError> {
    let request: Dlss5UninstallRequest = parse(request)?;
    let target_directory = absolute(Path::new(&request.target_dir))?;
    let paths = data_paths(request.data_dir.as_deref())?;
    paths.ensure_created()?;
    let report = Dlss5Service::uninstall(&target_directory, &paths)?;
    Ok(json!({
        "ok": true,
        "restored": report.restored_files,
        "removed": report.removed_files,
        "conflicts": report.conflicts,
        "warnings": report.warnings,
    }))
}

const DEFAULT_PACK: &str = "dlss5.reshade-gitc";

fn resolve_pack(
    pack_id: Option<String>,
    pack_dir: Option<String>,
) -> Result<Option<PathBuf>, AppError> {
    if let Some(directory) = pack_dir {
        return Ok(Some(absolute(Path::new(&directory))?));
    }
    let Some(pack_id) = pack_id else {
        return Ok(None);
    };
    PackLocator::find(&pack_id)
        .map(Some)
        .ok_or_else(|| AppError::new(ErrorCode::PackNotFound).with("packId", pack_id))
}

fn parse_api(value: &str) -> Result<GraphicsApi, AppError> {
    match value.to_lowercase().as_str() {
        "dx12" | "d3d12" => Ok(GraphicsApi::Dx12),
        "dx11" | "d3d11" => Ok(GraphicsApi::Dx11),
        "dx10" | "d3d10" => Ok(GraphicsApi::Dx10),
        "dx9" | "d3d9" => Ok(GraphicsApi::Dx9),
        "vulkan" => Ok(GraphicsApi::Vulkan),
        "opengl" => Ok(GraphicsApi::OpenGl),
        _ => Err(AppError::new(ErrorCode::InvalidArgument).with("api", value.to_string())),
    }
}

/// Retries queued DLSS5 hotkey writes; returns how many were applied.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_dlss5_process_hotkey_queue_json() -> *mut c_char {
    execute(|| {
        match fgm_infra::paths::AppPaths::create_default(None)
            .and_then(|paths| Dlss5Service::process_hotkey_queue(&paths))
        {
            Ok(applied) => json!({ "ok": true, "applied": applied }),
            Err(error) => error_json(&error),
        }
    })
}
