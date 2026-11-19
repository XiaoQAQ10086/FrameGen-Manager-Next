use std::ffi::c_char;
use std::path::PathBuf;

use fgm_domain::error::{AppError, ErrorCode};
use fgm_infra::packs::PackLocator;
use fgm_infra::paths::AppPaths;
use fgm_infra::process::ProcessLauncher;
use serde::Deserialize;
use serde_json::{Value, json};

use super::commands::{absolute, parse};
use super::{error_json, execute, execute_request};

const REPOSITORY: &str = "https://github.com/XiaoQAQ10086/FrameGen-Manager-Next";
const NETDISK_URL: &str = "https://pan.quark.cn/s/adea7ed4f2b2";
const ANR_BAIDU_URL: &str = "https://pan.baidu.com/s/1DWG8f4767zpjD0Rk4HBX2w?pwd=1234";
const ANR_QUARK_URL: &str = "https://pan.quark.cn/s/5708e53de73b";

pub(crate) fn paths() -> Result<Value, AppError> {
    let paths = AppPaths::create_default(None)?;
    let bundled = bundled_packs().unwrap_or_else(|| paths.packs());
    let licenses = std::env::current_exe()
        .ok()
        .and_then(|exe| exe.parent().map(|parent| parent.join("licenses")))
        .unwrap_or_else(|| paths.root().join("licenses"));
    Ok(json!({
        "ok": true,
        "dataRoot": paths.root(),
        "packsDirectory": paths.packs(),
        "bundledPacksDirectory": bundled,
        "licensesDirectory": licenses,
        "logsDirectory": paths.logs(),
        "repository": REPOSITORY,
        "netdiskUrl": NETDISK_URL,
        "anrBaiduUrl": ANR_BAIDU_URL,
        "anrQuarkUrl": ANR_QUARK_URL,
    }))
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct OpenFolderRequest {
    which: String,
}

pub(crate) fn open_folder(request: &str) -> Result<Value, AppError> {
    let request: OpenFolderRequest = parse(request)?;
    let paths = AppPaths::create_default(None)?;
    let target = match request.which.as_str() {
        "bundledPacks" => bundled_packs().unwrap_or_else(|| paths.packs()),
        "packs" => paths.packs(),
        "licenses" => std::env::current_exe()
            .ok()
            .and_then(|exe| exe.parent().map(|parent| parent.join("licenses")))
            .ok_or_else(|| AppError::new(ErrorCode::FileNotFound))?,
        "logs" => paths.logs(),
        "data" => paths.root().to_path_buf(),
        other => {
            return Err(AppError::new(ErrorCode::InvalidArgument).with("which", other.to_string()));
        }
    };
    ProcessLauncher::open_folder(&target)?;
    Ok(json!({ "ok": true }))
}

#[derive(Deserialize)]
struct OpenPathRequest {
    path: String,
}

pub(crate) fn open_path(request: &str) -> Result<Value, AppError> {
    let request: OpenPathRequest = parse(request)?;
    let path = absolute(std::path::Path::new(&request.path))?;
    let target = if path.is_file() {
        path.parent().unwrap_or(&path).to_path_buf()
    } else {
        path
    };
    ProcessLauncher::open_folder(&target)?;
    Ok(json!({ "ok": true }))
}

pub(crate) fn open_hags() -> Result<Value, AppError> {
    ProcessLauncher::open_settings_page("ms-settings:display-advancedgraphics")?;
    Ok(json!({ "ok": true }))
}

pub(crate) fn restart_computer() -> Result<Value, AppError> {
    ProcessLauncher::restart_computer()?;
    Ok(json!({ "ok": true }))
}

fn bundled_packs() -> Option<PathBuf> {
    if let Ok(executable) = std::env::current_exe()
        && let Some(directory) = executable.parent()
    {
        let bundled = directory.join("packs");
        if bundled.is_dir() {
            return Some(bundled);
        }
    }
    PackLocator::roots().into_iter().find(|root| root.is_dir())
}

#[unsafe(no_mangle)]
pub extern "C" fn fgm_paths_json() -> *mut c_char {
    execute(|| match paths() {
        Ok(value) => value,
        Err(error) => error_json(&error),
    })
}

#[unsafe(no_mangle)]
pub extern "C" fn fgm_open_folder_json(request: *const u8, length: usize) -> *mut c_char {
    execute_request(request, length, open_folder)
}

#[unsafe(no_mangle)]
pub extern "C" fn fgm_open_path_json(request: *const u8, length: usize) -> *mut c_char {
    execute_request(request, length, open_path)
}

#[unsafe(no_mangle)]
pub extern "C" fn fgm_open_hags_json() -> *mut c_char {
    execute(|| match open_hags() {
        Ok(value) => value,
        Err(error) => error_json(&error),
    })
}

#[unsafe(no_mangle)]
pub extern "C" fn fgm_restart_computer_json() -> *mut c_char {
    execute(|| match restart_computer() {
        Ok(value) => value,
        Err(error) => error_json(&error),
    })
}
