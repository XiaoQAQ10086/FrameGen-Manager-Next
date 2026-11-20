use std::path::Path;

use fgm_domain::error::{AppError, ErrorCode};
use fgm_infra::anr::{AnrDeploymentService, AnrPackageService};
use fgm_infra::paths::AppPaths;
use fgm_infra::platform::FilePicker;
use serde::Deserialize;
use serde_json::{Value, json};

use super::commands::{absolute, data_paths, parse};

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct AnrImportRequest {
    #[serde(default)]
    zip_path: Option<String>,
    #[serde(default)]
    data_dir: Option<String>,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct AnrDeployRequest {
    target_dir: String,
    #[serde(default)]
    exe_path: Option<String>,
    #[serde(default)]
    proxy: Option<String>,
    #[serde(default)]
    data_dir: Option<String>,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct AnrRestoreRequest {
    target_dir: String,
    #[serde(default)]
    data_dir: Option<String>,
}

pub(crate) fn anr_current() -> Result<Value, AppError> {
    let paths = AppPaths::create_default(None)?;
    Ok(match AnrPackageService::load_current(paths.root()) {
        Some(info) => json!({ "ok": true, "package": info }),
        None => json!({ "ok": true, "package": null }),
    })
}

pub(crate) fn anr_import(request: &str) -> Result<Value, AppError> {
    let request: AnrImportRequest = parse(request)?;
    let zip = match request.zip_path {
        Some(path) => absolute(Path::new(&path))?,
        None => match FilePicker::pick_anr_zip() {
            Some(path) => path,
            None => return Ok(json!({ "ok": true, "cancelled": true })),
        },
    };
    let paths = data_paths(request.data_dir.as_deref())?;
    paths.ensure_created()?;
    let info = AnrPackageService::import(&zip, paths.root())?;
    Ok(json!({ "ok": true, "package": info }))
}

pub(crate) fn anr_deploy(request: &str) -> Result<Value, AppError> {
    let request: AnrDeployRequest = parse(request)?;
    let target = absolute(Path::new(&request.target_dir))?;
    let executable = request
        .exe_path
        .as_deref()
        .map(Path::new)
        .map(absolute)
        .transpose()?;
    let paths = data_paths(request.data_dir.as_deref())?;
    paths.ensure_created()?;
    let package = AnrPackageService::load_current(paths.root()).ok_or_else(|| {
        AppError::new(ErrorCode::PackNotFound).with("reason", "no ANR package imported")
    })?;
    let operation = super::commands_operation::begin("anrDeploy");
    let result = AnrDeploymentService::deploy(
        &package,
        &target,
        executable.as_deref(),
        &paths,
        &operation.cancelled,
        Some(operation.as_ref()),
        request.proxy.as_deref(),
    );
    super::commands_operation::finish();
    let report = result?;
    super::commands::log_event(
        &paths,
        "info",
        "anr deploy",
        &[
            ("target", &request.target_dir),
            ("files", &report.files_written.to_string()),
        ],
    );
    Ok(json!({
        "ok": true,
        "kind": report.kind,
        "filesWritten": report.files_written,
        "proxyModule": report.proxy_module,
    }))
}

pub(crate) fn anr_restore(request: &str) -> Result<Value, AppError> {
    let request: AnrRestoreRequest = parse(request)?;
    let target = absolute(Path::new(&request.target_dir))?;
    let paths = data_paths(request.data_dir.as_deref())?;
    paths.ensure_created()?;
    AnrDeploymentService::restore(&target, &paths)?;
    super::commands::log_event(
        &paths,
        "info",
        "anr restore",
        &[("target", &request.target_dir)],
    );
    Ok(json!({ "ok": true }))
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct AnrDownloadRequest {
    #[serde(default)]
    data_dir: Option<String>,
}

pub(crate) fn anr_download(request: &str) -> Result<Value, AppError> {
    let request: AnrDownloadRequest = parse(request)?;
    let paths = data_paths(request.data_dir.as_deref())?;
    paths.ensure_created()?;
    let operation = super::commands_operation::begin("anrDownload");
    let result = fgm_infra::anr::AnrOnlineService::download_latest(
        paths.root(),
        &operation.cancelled,
        |done| super::commands_operation::report_bytes(&operation, done),
        |total| super::commands_operation::report_total(&operation, total),
    );
    super::commands_operation::finish();
    let info = result?;
    Ok(json!({ "ok": true, "package": info }))
}
