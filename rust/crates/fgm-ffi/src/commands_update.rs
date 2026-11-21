use std::path::{Path, PathBuf};
use std::sync::atomic::AtomicBool;
use std::time::{SystemTime, UNIX_EPOCH};

use fgm_domain::error::{AppError, ErrorCode};
use fgm_domain::version::VERSION;
use fgm_infra::fs::AtomicFile;
use fgm_infra::paths::AppPaths;
use fgm_infra::process::ProcessLauncher;
use fgm_infra::updates::{
    UpdateChecker, UpdateDownloader, UpdateInfo, UpdateLauncher, UpdateManifest, UpdateSource,
};
use serde::{Deserialize, Serialize};
use serde_json::{Value, json};

use super::commands::{absolute, data_paths, parse};

const CHECK_TTL_SECONDS: u64 = 24 * 60 * 60;

#[derive(Default, Deserialize)]
#[serde(rename_all = "camelCase")]
struct UpdateCheckRequest {
    #[serde(default)]
    force: bool,
    #[serde(default)]
    data_dir: Option<String>,
}

#[derive(Deserialize, Serialize)]
struct CachedUpdate {
    checked_at: u64,
    available: bool,
    current_version: String,
    manifest: UpdateManifest,
}

fn cache_path(paths: &AppPaths) -> PathBuf {
    paths.state().join("update-check.json")
}

fn load_cached(paths: &AppPaths, force: bool) -> Option<UpdateInfo> {
    if force {
        return None;
    }
    let bytes = std::fs::read(cache_path(paths)).ok()?;
    let cached: CachedUpdate = serde_json::from_slice(&bytes).ok()?;
    let now = SystemTime::now().duration_since(UNIX_EPOCH).ok()?.as_secs();
    if now.saturating_sub(cached.checked_at) >= CHECK_TTL_SECONDS {
        return None;
    }
    Some(UpdateInfo {
        available: cached.available,
        current_version: cached.current_version,
        manifest: cached.manifest,
    })
}

fn save_cached(paths: &AppPaths, info: &UpdateInfo) {
    let Ok(now) = SystemTime::now().duration_since(UNIX_EPOCH) else {
        return;
    };
    let cached = CachedUpdate {
        checked_at: now.as_secs(),
        available: info.available,
        current_version: info.current_version.clone(),
        manifest: info.manifest.clone(),
    };
    if let Ok(bytes) = serde_json::to_vec_pretty(&cached) {
        let _ = AtomicFile::write(&cache_path(paths), &bytes);
    }
}

fn checked_info(paths: &AppPaths, force: bool) -> Result<UpdateInfo, AppError> {
    if let Some(info) = load_cached(paths, force) {
        return Ok(info);
    }
    let info = UpdateChecker::check(
        &UpdateSource::manifest_urls(),
        VERSION,
        &AtomicBool::new(false),
    )?;
    save_cached(paths, &info);
    Ok(info)
}

pub(crate) fn update_check(request: &str) -> Result<Value, AppError> {
    let request: UpdateCheckRequest = parse(request)?;
    let paths = data_paths(request.data_dir.as_deref())?;
    let info = checked_info(&paths, request.force)?;
    Ok(check_reply(&info))
}

pub(crate) fn update_download() -> Result<Value, AppError> {
    let paths = AppPaths::create_default(None)?;
    let info = checked_info(&paths, false)?;
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
