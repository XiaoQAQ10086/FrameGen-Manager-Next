use std::path::Path;
use std::sync::atomic::AtomicBool;

use fgm_domain::error::{AppError, ErrorCode};
use fgm_domain::version::VERSION;
use fgm_infra::paths::AppPaths;
use fgm_infra::process::ProcessLauncher;
use fgm_infra::updates::{UpdateChecker, UpdateDownloader, UpdateLauncher, UpdateSource};
use serde::Deserialize;
use serde_json::{Value, json};

use super::commands::{absolute, parse};

pub(crate) fn update_check() -> Result<Value, AppError> {
    let info = UpdateChecker::check(
        &UpdateSource::manifest_urls(),
        VERSION,
        &AtomicBool::new(false),
    )?;
    Ok(check_reply(&info))
}

pub(crate) fn update_download() -> Result<Value, AppError> {
    let info = UpdateChecker::check(
        &UpdateSource::manifest_urls(),
        VERSION,
        &AtomicBool::new(false),
    )?;
    if !info.available {
        return Ok(check_reply(&info));
    }
    let installer = info.manifest.installer.clone().ok_or_else(|| {
        AppError::new(ErrorCode::PackInvalid).with("reason", "the update manifest has no installer")
    })?;
    let operation = super::commands_operation::begin("updateDownload");
    super::commands_operation::report_total(&operation, installer.size);
    let result = UpdateDownloader::download(
        &installer,
        &info.manifest.version,
        &operation.cancelled,
        |done| super::commands_operation::report_bytes(&operation, done),
    );
    super::commands_operation::finish();
    let path = result?;
    let portable = AppPaths::create_default(None).is_ok_and(|paths| paths.is_portable());
    Ok(json!({
        "ok": true,
        "available": true,
        "version": info.manifest.version,
        "currentVersion": info.current_version,
        "pageUrl": info.manifest.page_url,
        "path": path.to_string_lossy(),
        "portable": portable,
    }))
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct UpdateInstallRequest {
    path: String,
}

pub(crate) fn update_install(request: &str) -> Result<Value, AppError> {
    let request: UpdateInstallRequest = parse(request)?;
    let path = absolute(Path::new(&request.path))?;
    UpdateLauncher::install(&path)?;
    Ok(json!({ "ok": true }))
}

#[derive(Deserialize)]
struct UpdateOpenRequest {
    url: String,
}

pub(crate) fn update_open(request: &str) -> Result<Value, AppError> {
    let request: UpdateOpenRequest = parse(request)?;
    ProcessLauncher::open_url(&request.url)?;
    Ok(json!({ "ok": true }))
}

fn check_reply(info: &fgm_infra::updates::UpdateInfo) -> Value {
    let manifest = &info.manifest;
    json!({
        "ok": true,
        "available": info.available,
        "currentVersion": info.current_version,
        "version": manifest.version,
        "notes": manifest.notes,
        "pageUrl": manifest.page_url,
        "installer": manifest.installer.as_ref().map(|installer| json!({
            "name": installer.name,
            "size": installer.size,
        })),
    })
}
