use std::path::Path;

use fgm_domain::error::{AppError, ErrorCode};
use fgm_infra::library::{CoverService, LibraryService};
use fgm_infra::platform::FilePicker;
use serde::Deserialize;
use serde_json::{Value, json};

use super::commands::{absolute, data_paths, parse};

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct AddManualRequest {
    #[serde(default)]
    exe_path: Option<String>,
    #[serde(default)]
    data_dir: Option<String>,
}

pub(crate) fn add_manual(request: &str) -> Result<Value, AppError> {
    let request: AddManualRequest = parse(request)?;
    let executable = match request.exe_path {
        Some(path) => absolute(Path::new(&path))?,
        None => match FilePicker::pick_executable() {
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
