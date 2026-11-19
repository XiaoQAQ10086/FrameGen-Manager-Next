use std::collections::BTreeMap;
use std::path::{Path, PathBuf};

use fgm_domain::error::{AppError, ErrorCode};
use fgm_domain::pack::IniSetting;
use fgm_domain::version::VERSION;
use fgm_infra::deployment::{DeploymentEngine, DeploymentInspector, PackDeploymentPlanner};
use fgm_infra::pack_json::PackJson;
use fgm_infra::packs::PackLocator;
use fgm_infra::paths::AppPaths;
use fgm_infra::reframework::ReframeworkService;
use serde::Deserialize;
use serde_json::{Value, json};

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct DeployRequest {
    #[serde(default)]
    pack_id: String,
    #[serde(default)]
    pack_dir: Option<String>,
    target_dir: String,
    #[serde(default)]
    family: Option<String>,
    #[serde(default)]
    proxy_slot: Option<String>,
    #[serde(default)]
    game_exe: Option<String>,
    #[serde(default)]
    game_id: Option<String>,
    #[serde(default)]
    persist_exe: bool,
    #[serde(default)]
    app_version: Option<String>,
    #[serde(default)]
    data_dir: Option<String>,
    #[serde(default)]
    ini: Vec<IniSetting>,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct RestoreRequest {
    target_dir: String,
    family: String,
    #[serde(default)]
    data_dir: Option<String>,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct InspectRequest {
    target_dir: String,
    family: String,
    #[serde(default)]
    pack_id: Option<String>,
    #[serde(default)]
    pack_dir: Option<String>,
    #[serde(default)]
    data_dir: Option<String>,
}

pub(crate) fn deploy(request: &str) -> Result<Value, AppError> {
    let request: DeployRequest = parse(request)?;
    let pack_directory = pack_directory(request.pack_dir.as_deref(), &request.pack_id)?;
    let pack = PackJson::load(&pack_directory.join("pack.json"))?;
    let mut target_directory = absolute(Path::new(&request.target_dir))?;
    let app_version = request.app_version.unwrap_or_else(|| VERSION.to_string());
    let paths = data_paths(request.data_dir.as_deref())?;
    paths.ensure_created()?;
    let game_executable = request
        .game_exe
        .as_deref()
        .map(Path::new)
        .map(absolute)
        .transpose()?;
    if request.persist_exe
        && let (Some(game_id), Some(executable)) = (&request.game_id, &game_executable)
    {
        let game =
            fgm_infra::library::LibraryService::set_target_override(&paths, game_id, executable)?;
        target_directory = game.target_directory;
    }

    let mut options = BTreeMap::new();
    if let Some(slot) = &request.proxy_slot {
        options.insert("proxySlot".to_string(), slot.clone());
    }

    let plan = PackDeploymentPlanner::plan(
        &pack,
        &pack_directory,
        &target_directory,
        &options,
        &request.ini,
        Some(&app_version),
    )?;

    let family = request.family.unwrap_or_else(|| pack.id.clone());

    if let Some(executable) = game_executable.as_deref() {
        ReframeworkService::install_before_deploy(
            executable,
            &target_directory,
            &app_version,
            &paths,
        )?;
    }

    let operation = super::commands_operation::begin("deploy");
    let report = DeploymentEngine::apply(
        &plan.files,
        &target_directory,
        &family,
        &pack.id,
        &pack.version,
        &options,
        &pack.owned_directories,
        game_executable.as_deref(),
        &paths.backups(),
        &paths.state().join("locks"),
        &operation.cancelled,
        Some(operation.as_ref()),
    );
    super::commands_operation::finish();
    let report = report?;

    let files = report.files_written.to_string();
    log_event(
        &paths,
        "info",
        "deploy",
        &[("family", &family), ("packId", &pack.id), ("files", &files)],
    );
    Ok(json!({
        "ok": true,
        "state": report.state,
        "filesWritten": report.files_written,
        "filesBackedUp": report.files_backed_up,
        "warnings": report.warnings,
        "proxySlot": plan.proxy_slot,
    }))
}

pub(crate) fn restore(request: &str) -> Result<Value, AppError> {
    let request: RestoreRequest = parse(request)?;
    let target_directory = absolute(Path::new(&request.target_dir))?;
    let paths = data_paths(request.data_dir.as_deref())?;
    paths.ensure_created()?;

    let report = DeploymentEngine::restore(
        &request.family,
        &target_directory,
        &paths.backups(),
        &paths.state().join("locks"),
    )?;

    log_event(&paths, "info", "restore", &[("family", &request.family)]);
    Ok(json!({
        "ok": true,
        "restoredFiles": report.restored_files,
        "removedFiles": report.removed_files,
        "conflicts": report.conflicts,
        "warnings": report.warnings,
    }))
}

pub(crate) fn inspect(request: &str) -> Result<Value, AppError> {
    let request: InspectRequest = parse(request)?;
    let target_directory = absolute(Path::new(&request.target_dir))?;
    let pack = match (&request.pack_id, &request.pack_dir) {
        (_, Some(directory)) => {
            let directory = absolute(Path::new(directory))?;
            Some(PackJson::load(&directory.join("pack.json"))?)
        }
        (Some(pack_id), None) => {
            let directory = PackLocator::find(pack_id).ok_or_else(|| {
                AppError::new(ErrorCode::PackNotFound).with("packId", pack_id.clone())
            })?;
            Some(PackJson::load(&directory.join("pack.json"))?)
        }
        (None, None) => None,
    };
    let paths = data_paths(request.data_dir.as_deref())?;

    let inspection = DeploymentInspector::inspect(
        &request.family,
        &target_directory,
        &paths.backups(),
        pack.as_ref(),
    )?;

    Ok(json!({
        "ok": true,
        "state": inspection.state,
        "hasManifest": inspection.has_manifest,
        "packVersion": inspection.pack_version,
        "details": inspection.details,
        "options": inspection.options,
    }))
}

pub(crate) fn parse<T: for<'de> Deserialize<'de>>(request: &str) -> Result<T, AppError> {
    serde_json::from_str(request).map_err(|error| {
        AppError::new(ErrorCode::InvalidArgument).with("reason", error.to_string())
    })
}

fn pack_directory(explicit: Option<&str>, pack_id: &str) -> Result<PathBuf, AppError> {
    if let Some(directory) = explicit {
        return absolute(Path::new(directory));
    }
    PackLocator::find(pack_id)
        .ok_or_else(|| AppError::new(ErrorCode::PackNotFound).with("packId", pack_id))
}

pub(crate) fn data_paths(directory: Option<&str>) -> Result<AppPaths, AppError> {
    match directory {
        Some(directory) => AppPaths::new(Path::new(directory), false),
        None => AppPaths::create_default(None),
    }
}

pub(crate) fn absolute(path: &Path) -> Result<PathBuf, AppError> {
    std::path::absolute(path).map_err(|error| {
        AppError::new(ErrorCode::InvalidPath)
            .with("path", path.display().to_string())
            .with("reason", error.to_string())
    })
}

pub(crate) fn settings_get() -> Result<Value, AppError> {
    let paths = AppPaths::create_default(None)?;
    let store = fgm_infra::settings::SettingsStore::new(paths.config().join("app.json"));
    let settings = store.load()?;
    Ok(json!({ "ok": true, "settings": settings }))
}

pub(crate) fn settings_set(request: &str) -> Result<Value, AppError> {
    let settings: fgm_infra::settings::AppSettings = parse(request)?;
    let paths = AppPaths::create_default(None)?;
    paths.ensure_created()?;
    let store = fgm_infra::settings::SettingsStore::new(paths.config().join("app.json"));
    store.save(&settings)?;
    Ok(json!({ "ok": true, "settings": settings }))
}

pub(crate) fn gpu() -> Result<Value, AppError> {
    let info = fgm_infra::gpu::GpuDetector::detect();
    let value = info.map(|info| {
        json!({
            "name": info.name,
            "driverVersion": info.driver_version,
            "route": info.route,
            "driverSupported": info.driver_supported,
        })
    });
    Ok(json!({ "ok": true, "gpu": value }))
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct LogsTailRequest {
    #[serde(default = "default_log_lines")]
    max_lines: usize,
}

const fn default_log_lines() -> usize {
    500
}

pub(crate) fn logs_tail(request: &str) -> Result<Value, AppError> {
    let request: LogsTailRequest = parse(request)?;
    let paths = AppPaths::create_default(None)?;
    let log = fgm_infra::logging::OperationLog::new(paths.logs().join("fgm.log"));
    let lines = log.tail(request.max_lines.min(5_000))?;
    Ok(json!({ "ok": true, "lines": lines }))
}

pub(crate) fn log_event(paths: &AppPaths, level: &str, message: &str, fields: &[(&str, &str)]) {
    let operation_id = std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)
        .map_or(0, |value| value.as_millis() as u64);
    let log = fgm_infra::logging::OperationLog::new(paths.logs().join("fgm.log"));
    let _ = log.append(operation_id, level, message, fields);
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::commands_sm::sm_inspect_many;
    use fgm_infra::hashing::FileHasher;
    use std::fs;
    use std::time::{SystemTime, UNIX_EPOCH};

    #[test]
    fn deploy_inspect_restore_round_trip() {
        let root = unique_dir("ffi-deploy");
        let pack_dir = root.join("pack");
        let target = root.join("game");
        let data = root.join("data");
        fs::create_dir_all(&pack_dir).unwrap();
        fs::create_dir_all(&target).unwrap();
        fs::write(pack_dir.join("version.dll"), b"runtime").unwrap();
        let hash = FileHasher::sha256_file(&pack_dir.join("version.dll")).unwrap();
        let size = fs::metadata(pack_dir.join("version.dll")).unwrap().len();
        fs::write(
            pack_dir.join("pack.json"),
            format!(
                r#"{{
  "schemaVersion": 1,
  "id": "test.pack",
  "version": "1.0.0",
  "displayName": "Test",
  "minAppVersion": "1.0.0",
  "gpuRoutes": ["Any"],
  "files": [{{"source":"version.dll","target":"version.dll","role":"runtime","sha256":"{hash}","size":{size}}}],
  "trust": "bundled"
}}"#
            ),
        )
        .unwrap();

        let deploy_request = json!({
            "packDir": pack_dir,
            "targetDir": target,
            "family": "test.pack",
            "appVersion": "2.3.4",
            "dataDir": data,
        })
        .to_string();
        let deployed = deploy(&deploy_request).unwrap();
        assert_eq!(deployed["ok"], true);
        assert_eq!(deployed["state"], "installed");
        assert_eq!(
            fs::read_to_string(target.join("version.dll")).unwrap(),
            "runtime"
        );

        let inspect_request = json!({
            "packDir": pack_dir,
            "targetDir": target,
            "family": "test.pack",
            "dataDir": data,
        })
        .to_string();
        let inspected = inspect(&inspect_request).unwrap();
        assert_eq!(inspected["state"], "installed");

        let restore_request = json!({
            "targetDir": target,
            "family": "test.pack",
            "dataDir": data,
        })
        .to_string();
        let restored = restore(&restore_request).unwrap();
        assert_eq!(restored["removedFiles"], 1);
        assert!(!target.join("version.dll").exists());

        let _ = fs::remove_dir_all(root);
    }

    #[test]
    fn sm_inspect_many_is_read_only() {
        let exe = std::env::current_exe().unwrap();
        let request = json!({ "exePaths": [exe] }).to_string();
        let value = sm_inspect_many(&request).unwrap();
        assert_eq!(value["ok"], true);
        assert_eq!(value["states"].as_array().unwrap().len(), 1);
    }

    fn unique_dir(name: &str) -> PathBuf {
        let nanos = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .unwrap()
            .as_nanos();
        let path = std::env::temp_dir().join(format!("fgm-{name}-{}-{nanos}", std::process::id()));
        fs::create_dir_all(&path).unwrap();
        path
    }
}
