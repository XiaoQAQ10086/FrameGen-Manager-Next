use std::path::Path;

use fgm_domain::error::{AppError, ErrorCode};
use fgm_domain::version::VERSION;
use serde::Deserialize;
use serde_json::{Value, json};

use super::commands::{absolute, data_paths, parse};

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct SmInspectRequest {
    exe_path: String,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct SmEnableRequest {
    game: String,
    exe_path: String,
    target_dir: String,
    #[serde(default)]
    app_version: Option<String>,
    #[serde(default)]
    data_dir: Option<String>,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct SmChangeRequest {
    game: String,
    exe_path: String,
    target_dir: String,
    #[serde(default)]
    data_dir: Option<String>,
}

pub(crate) fn sm_inspect(request: &str) -> Result<Value, AppError> {
    let request: SmInspectRequest = parse(request)?;
    let executable = absolute(Path::new(&request.exe_path))?;
    let state = fgm_infra::smooth_motion::SmoothMotionService::inspect(&executable);
    Ok(json!({
        "ok": true,
        "enabled": state.enabled,
        "profiles": state.profile_names,
        "ours": state.ours,
        "detail": state.detail,
    }))
}

pub(crate) fn sm_enable(request: &str) -> Result<Value, AppError> {
    let request: SmEnableRequest = parse(request)?;
    let executable = absolute(Path::new(&request.exe_path))?;
    let target_directory = absolute(Path::new(&request.target_dir))?;
    let paths = data_paths(request.data_dir.as_deref())?;
    paths.ensure_created()?;
    let gpu = fgm_infra::gpu::GpuDetector::detect().ok_or_else(|| {
        AppError::new(ErrorCode::GpuUnsupported).with("reason", "NVIDIA GPU not detected")
    })?;
    let app_version = request.app_version.unwrap_or_else(|| VERSION.to_string());
    let change = fgm_infra::smooth_motion::SmoothMotionFacade::enable(
        gpu.route,
        gpu.driver_supported,
        &request.game,
        &executable,
        &target_directory,
        &app_version,
        &paths,
    )?;
    super::commands::log_event(&paths, "info", "sm enable", &[("game", &request.game)]);
    Ok(change_json(change))
}

pub(crate) fn sm_disable(request: &str) -> Result<Value, AppError> {
    let request: SmChangeRequest = parse(request)?;
    let executable = absolute(Path::new(&request.exe_path))?;
    let target_directory = absolute(Path::new(&request.target_dir))?;
    let paths = data_paths(request.data_dir.as_deref())?;
    paths.ensure_created()?;
    let change = fgm_infra::smooth_motion::SmoothMotionFacade::disable(
        &request.game,
        &executable,
        &target_directory,
        &paths,
    )?;
    super::commands::log_event(&paths, "info", "sm disable", &[("game", &request.game)]);
    Ok(change_json(change))
}

pub(crate) fn sm_restore(request: &str) -> Result<Value, AppError> {
    let request: SmChangeRequest = parse(request)?;
    let executable = absolute(Path::new(&request.exe_path))?;
    let target_directory = absolute(Path::new(&request.target_dir))?;
    let paths = data_paths(request.data_dir.as_deref())?;
    paths.ensure_created()?;
    let change = fgm_infra::smooth_motion::SmoothMotionFacade::restore(
        &request.game,
        &executable,
        &target_directory,
        &paths,
    )?;
    super::commands::log_event(&paths, "info", "sm restore", &[("game", &request.game)]);
    Ok(change_json(change))
}

fn change_json(change: fgm_infra::smooth_motion::SmoothMotionChange) -> Value {
    json!({
        "ok": change.ok,
        "enabled": change.enabled,
        "profiles": change.profile_names,
        "detail": change.detail,
    })
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct SmInspectManyRequest {
    exe_paths: Vec<String>,
}

pub(crate) fn sm_inspect_many(request: &str) -> Result<Value, AppError> {
    let request: SmInspectManyRequest = parse(request)?;
    let executables = request
        .exe_paths
        .iter()
        .filter_map(|path| absolute(Path::new(path)).ok())
        .collect::<Vec<_>>();
    let states = fgm_infra::smooth_motion::SmoothMotionService::inspect_many(&executables);
    let values = states
        .into_iter()
        .map(|state| {
            json!({
                "enabled": state.enabled,
                "profiles": state.profile_names,
                "ours": state.ours,
                "detail": state.detail,
            })
        })
        .collect::<Vec<_>>();
    Ok(json!({ "ok": true, "states": values }))
}
