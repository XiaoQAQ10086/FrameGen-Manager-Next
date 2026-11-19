use std::path::Path;

use fgm_domain::error::{AppError, ErrorCode};
use fgm_domain::library::GraphicsApi;
use fgm_domain::version::VERSION;
use fgm_infra::dlss5::Dlss5Service;
use fgm_infra::packs::PackLocator;
use serde::Deserialize;
use serde_json::{Value, json};

use super::commands::{absolute, data_paths, parse};

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
    app_version: Option<String>,
    #[serde(default)]
    data_dir: Option<String>,
}

pub(crate) fn dlss5_install(request: &str) -> Result<Value, AppError> {
    let request: Dlss5InstallRequest = parse(request)?;
    let pack_id = request
        .pack_id
        .unwrap_or_else(|| "dlss5.reshade-gitc".to_string());
    let pack_directory = match request.pack_dir {
        Some(directory) => absolute(Path::new(&directory))?,
        None => PackLocator::find(&pack_id).ok_or_else(|| {
            AppError::new(ErrorCode::PackNotFound).with("packId", pack_id.clone())
        })?,
    };
    let executable = absolute(Path::new(&request.exe_path))?;
    let target_directory = absolute(Path::new(&request.target_dir))?;
    let paths = data_paths(request.data_dir.as_deref())?;
    paths.ensure_created()?;
    let api = parse_api(&request.api)?;
    let app_version = request.app_version.unwrap_or_else(|| VERSION.to_string());

    let report = Dlss5Service::install(
        &pack_directory,
        &executable,
        &target_directory,
        api,
        &app_version,
        &paths,
    )?;

    Ok(json!({
        "ok": true,
        "filesWritten": report.files_written,
        "proxyModule": report.proxy_module,
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
