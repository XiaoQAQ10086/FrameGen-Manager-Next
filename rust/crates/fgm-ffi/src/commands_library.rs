use std::ffi::c_char;
use std::path::Path;

use fgm_domain::error::{AppError, ErrorCode};
use fgm_infra::anr::AnrPackageService;
use fgm_infra::deployment::{DeploymentSnapshot, ProxyStatus};
use fgm_infra::library::{CoverService, LibraryService};
use fgm_infra::pack_json::PackJson;
use fgm_infra::packs::PackLocator;
use fgm_infra::platform::FilePicker;
use serde::Deserialize;
use serde_json::{Value, json};

use super::commands::{absolute, data_paths, parse};
use super::commands_operation::{begin, finish, report_scan, report_total};
use super::{error_json, execute, execute_request};

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct AddManualRequest {
    #[serde(default)]
    exe_path: Option<String>,
    #[serde(default)]
    data_dir: Option<String>,
}

/// Scans Steam, Epic, Xbox and loose folders and refreshes the cache.
pub(crate) fn scan_library() -> Result<Vec<fgm_domain::library::GameEntry>, AppError> {
    let paths = data_paths(None)?;
    paths.ensure_created()?;
    let operation = begin("libraryScan");
    report_total(&operation, 4);
    let result = LibraryService::scan_with_progress_cancelled(
        &paths,
        &operation.cancelled,
        |phase, completed, found| {
            report_scan(&operation, phase, completed, found);
        },
    );
    finish();
    let games = result?;
    super::commands::log_event(
        &paths,
        "info",
        "library scan",
        &[("games", &games.len().to_string())],
    );
    Ok(games)
}

/// Returns the cached library without scanning.
pub(crate) fn list_library() -> Result<Vec<fgm_domain::library::GameEntry>, AppError> {
    let paths = data_paths(None)?;
    Ok(LibraryService::list(&paths))
}

pub(crate) fn add_manual(request: &str) -> Result<Value, AppError> {
    let request: AddManualRequest = parse(request)?;
    let executable = match request.exe_path {
        Some(path) => absolute(Path::new(&path))?,
        None => match FilePicker::pick_executable(None) {
            Some(path) => path,
            None => return Ok(json!({ "ok": true, "cancelled": true })),
        },
    };
    let paths = data_paths(request.data_dir.as_deref())?;
    paths.ensure_created()?;
    let game = LibraryService::add_manual(&paths, &executable)?;
    Ok(json!({ "ok": true, "game": game }))
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct LaunchRequest {
    game_id: String,
    #[serde(default)]
    data_dir: Option<String>,
}

pub(crate) fn launch(request: &str) -> Result<Value, AppError> {
    let request: LaunchRequest = parse(request)?;
    if request.game_id.trim().is_empty() {
        return Err(AppError::new(ErrorCode::InvalidArgument).with("gameId", request.game_id));
    }
    let paths = data_paths(request.data_dir.as_deref())?;
    LibraryService::launch(&paths, &request.game_id)?;
    Ok(json!({ "ok": true }))
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct CoverRequest {
    game_id: String,
    #[serde(default)]
    data_dir: Option<String>,
}

pub(crate) fn cover(request: &str) -> Result<Value, AppError> {
    let request: CoverRequest = parse(request)?;
    let paths = data_paths(request.data_dir.as_deref())?;
    let game = LibraryService::list(&paths)
        .into_iter()
        .find(|game| game.id == request.game_id)
        .ok_or_else(|| {
            AppError::new(ErrorCode::FileNotFound).with("gameId", request.game_id.clone())
        })?;
    let path = CoverService::cover(&paths, &game)?;
    Ok(json!({
        "ok": true,
        "path": path.map(|path| path.to_string_lossy().into_owned()),
    }))
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct ModesRequest {
    #[serde(default)]
    data_dir: Option<String>,
}

/// Manifest-only deployment badges for every library game.
pub(crate) fn game_modes(request: &str) -> Result<Value, AppError> {
    let request: ModesRequest = parse(request)?;
    let paths = data_paths(request.data_dir.as_deref())?;
    let framegen_pack_version = pack_version("framegen.sm86");
    let dlss5_version = pack_version("dlss5.reshade-gitc");
    let anr_pack_version =
        AnrPackageService::load_current(paths.root()).map(|package| package.version);

    let games = LibraryService::list(&paths);
    let executables: Vec<std::path::PathBuf> = games
        .iter()
        .filter_map(|game| game.render_exe.clone())
        .collect();
    let smooth_motion = fgm_infra::smooth_motion::SmoothMotionService::inspect_many(&executables);
    let mut smooth_index = 0_usize;

    let modes: Vec<Value> = games
        .into_iter()
        .map(|game| {
            let smooth_state = if game.render_exe.is_some() {
                let state = smooth_motion.get(smooth_index);
                smooth_index += 1;
                state
            } else {
                None
            };
            let smooth = smooth_state
                .and_then(|state| state.enabled)
                .map(|enabled| if enabled { "on" } else { "off" });
            let smooth_profiles = smooth_state
                .map(|state| state.profile_names.clone())
                .unwrap_or_default();
            let smooth_ours = smooth_state.is_some_and(|state| state.ours);
            let smooth_detail = smooth_state.and_then(|state| state.detail.clone());
            let target = &game.target_directory;
            let snapshot = DeploymentSnapshot::new(target, &paths.backups());
            let framegen = Some(snapshot.state("framegen.sm86", framegen_pack_version.as_deref()));
            let dlss5 = Some(snapshot.state("dlss5", dlss5_version.as_deref()));
            let anr = Some(snapshot.state("anr", anr_pack_version.as_deref()));
            let anr_version = snapshot.version("anr");
            let proxies = snapshot
                .proxies(target)
                .into_iter()
                .map(|entry| {
                    json!({
                        "module": entry.module,
                        "owner": entry.owner.key(),
                        "ownedByFramegen": entry.owned_by_framegen,
                    })
                })
                .collect::<Vec<Value>>();
            let smooth_component = Some(snapshot.state("smooth-motion", None));
            let smooth_files_ok = snapshot
                .files_present("smooth-motion", target)
                .unwrap_or(true);
            let framegen_installed_version = snapshot.version("framegen.sm86");
            json!({
                "gameId": game.id,
                "framegen": framegen,
                "framegenVersion": framegen_installed_version,
                "framegenPackVersion": framegen_pack_version,
                "dlss5": dlss5,
                "anr": anr,
                "anrVersion": anr_version,
                "anrPackVersion": anr_pack_version,
                "proxies": proxies,
                "smoothMotion": smooth,
                "smoothMotionProfiles": smooth_profiles,
                "smoothMotionOurs": smooth_ours,
                "smoothMotionDetail": smooth_detail,
                "smoothMotionComponent": smooth_component,
                "smoothMotionFilesOk": smooth_files_ok,
            })
        })
        .collect();
    Ok(json!({ "ok": true, "modes": modes }))
}

fn pack_version(pack_id: &str) -> Option<String> {
    let directory = PackLocator::find(pack_id)?;
    PackJson::load(&directory.join("pack.json"))
        .ok()
        .map(|pack| pack.version)
}

#[derive(Default, Deserialize)]
struct PickExecutableRequest {
    #[serde(default)]
    title: Option<String>,
}

/// Opens the native picker and returns the chosen executable path.
pub(crate) fn pick_executable(request: &str) -> Result<Value, AppError> {
    let request: PickExecutableRequest = parse(request)?;
    Ok(
        match FilePicker::pick_executable(request.title.as_deref()) {
            Some(path) => json!({ "ok": true, "path": path.to_string_lossy() }),
            None => json!({ "ok": true, "cancelled": true }),
        },
    )
}

#[unsafe(no_mangle)]
pub extern "C" fn fgm_pick_executable_json() -> *mut c_char {
    execute(|| match pick_executable("{}") {
        Ok(value) => value,
        Err(error) => error_json(&error),
    })
}

/// Picker with a localized title, used by the napi bridge.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_pick_executable_request_json(
    request: *const u8,
    length: usize,
) -> *mut c_char {
    execute_request(request, length, pick_executable)
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct GameIdRequest {
    game_id: String,
    #[serde(default)]
    data_dir: Option<String>,
}

pub(crate) fn removed() -> Result<Value, AppError> {
    let paths = fgm_infra::paths::AppPaths::create_default(None)?;
    Ok(json!({
        "ok": true,
        "games": LibraryService::removed(&paths),
    }))
}

pub(crate) fn remove(request: &str) -> Result<Value, AppError> {
    let request: GameIdRequest = parse(request)?;
    let paths = data_paths(request.data_dir.as_deref())?;
    LibraryService::remove(&paths, &request.game_id)?;
    Ok(json!({ "ok": true }))
}

pub(crate) fn restore_removed(request: &str) -> Result<Value, AppError> {
    let request: GameIdRequest = parse(request)?;
    let paths = data_paths(request.data_dir.as_deref())?;
    LibraryService::restore_removed(&paths, &request.game_id)?;
    Ok(json!({ "ok": true }))
}

pub(crate) fn forget(request: &str) -> Result<Value, AppError> {
    let request: GameIdRequest = parse(request)?;
    let paths = data_paths(request.data_dir.as_deref())?;
    LibraryService::forget(&paths, &request.game_id)?;
    Ok(json!({ "ok": true }))
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct ProxyStatusRequest {
    target_dir: String,
    #[serde(default)]
    data_dir: Option<String>,
}

pub(crate) fn proxy_status(request: &str) -> Result<Value, AppError> {
    let request: ProxyStatusRequest = parse(request)?;
    let target = absolute(Path::new(&request.target_dir))?;
    let paths = data_paths(request.data_dir.as_deref())?;
    let entries = ProxyStatus::inspect(&target, &paths.backups())?;
    Ok(json!({
        "ok": true,
        "proxies": entries.iter().map(|entry| json!({
            "module": entry.module,
            "owner": entry.owner.key(),
            "ownedByFramegen": entry.owned_by_framegen,
        })).collect::<Vec<Value>>(),
    }))
}

/// Removed games and proxy ownership exports.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_library_removed_json() -> *mut c_char {
    execute(|| match removed() {
        Ok(value) => value,
        Err(error) => error_json(&error),
    })
}

#[unsafe(no_mangle)]
pub extern "C" fn fgm_library_remove_json(request: *const u8, length: usize) -> *mut c_char {
    execute_request(request, length, remove)
}

#[unsafe(no_mangle)]
pub extern "C" fn fgm_library_restore_removed_json(
    request: *const u8,
    length: usize,
) -> *mut c_char {
    execute_request(request, length, restore_removed)
}

#[unsafe(no_mangle)]
pub extern "C" fn fgm_library_forget_json(request: *const u8, length: usize) -> *mut c_char {
    execute_request(request, length, forget)
}

#[unsafe(no_mangle)]
pub extern "C" fn fgm_proxy_status_json(request: *const u8, length: usize) -> *mut c_char {
    execute_request(request, length, proxy_status)
}
