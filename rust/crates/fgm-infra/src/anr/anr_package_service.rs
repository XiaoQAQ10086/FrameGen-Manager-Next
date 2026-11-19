use std::path::{Path, PathBuf};
use std::time::{SystemTime, UNIX_EPOCH};

use fgm_domain::error::{AppError, ErrorCode};
use fgm_domain::safety::SafePath;
use serde_json::Value;

use crate::archive::ZipArchive;
use crate::fs::AtomicFile;

use super::anr_package_info::AnrPackageInfo;
use super::anr_package_kind::AnrPackageKind;

/// Imports ANR full/addon zip packages into the data directory.
pub struct AnrPackageService;

impl AnrPackageService {
    pub fn import(zip_path: &Path, data_root: &Path) -> Result<AnrPackageInfo, AppError> {
        let bytes = std::fs::read(zip_path).map_err(|error| {
            AppError::new(ErrorCode::FileNotFound)
                .with("path", zip_path.display().to_string())
                .with("reason", error.to_string())
        })?;
        let temp = temp_directory(data_root);
        std::fs::create_dir_all(&temp).map_err(|error| io_error(&temp, error))?;
        ZipArchive::extract_to(&bytes, &temp)?;

        let (kind, source_root) = if let Some(game) = find_game_directory(&temp) {
            (AnrPackageKind::Full, game)
        } else if let Some(addon) = find_addon_directory(&temp) {
            (AnrPackageKind::Addon, addon)
        } else {
            let _ = std::fs::remove_dir_all(&temp);
            return Err(AppError::new(ErrorCode::PackInvalid).with(
                "reason",
                "the archive is neither an ANR full nor addon package",
            ));
        };

        let version = find_version(&temp).unwrap_or_else(|| "unknown".to_string());
        let version = SafePath::normalize_relative(&version)
            .unwrap_or_else(|_| "unknown".to_string())
            .replace('/', "_");
        let package_root = data_root.join("packs").join("anr-fg");
        let payload = package_root.join(&version).join("payload");
        if payload.exists() {
            std::fs::remove_dir_all(&payload).map_err(|error| io_error(&payload, error))?;
        }
        std::fs::create_dir_all(&payload).map_err(|error| io_error(&payload, error))?;

        match kind {
            AnrPackageKind::Full => copy_directory_contents(&source_root, &payload)?,
            AnrPackageKind::Addon => {
                let destination = payload.join("Apex Neural Rendering FG");
                copy_directory_contents(&source_root, &destination)?;
            }
        }

        let info = AnrPackageInfo {
            kind,
            version,
            payload_directory: payload,
        };
        let current = serde_json::to_vec_pretty(&info).map_err(|error| {
            AppError::new(ErrorCode::Internal).with("reason", error.to_string())
        })?;
        AtomicFile::write(&package_root.join("current.json"), &current)?;
        let _ = std::fs::remove_dir_all(&temp);
        Ok(info)
    }

    pub fn load_current(data_root: &Path) -> Option<AnrPackageInfo> {
        let path = data_root.join("packs").join("anr-fg").join("current.json");
        let bytes = std::fs::read(path).ok()?;
        let info: AnrPackageInfo = serde_json::from_slice(&bytes).ok()?;
        info.payload_directory.is_dir().then_some(info)
    }
}

fn find_game_directory(root: &Path) -> Option<PathBuf> {
    find_directory(root, "game", &|directory| {
        directory.join("dxgi.dll").is_file() && directory.join("OptiScaler.ini").is_file()
    })
}

fn find_addon_directory(root: &Path) -> Option<PathBuf> {
    find_directory(root, "Apex Neural Rendering FG", &|directory| {
        directory.join("dlssg").join("nvngx_dlssg.dll").is_file()
    })
}

fn find_directory(root: &Path, name: &str, predicate: &impl Fn(&Path) -> bool) -> Option<PathBuf> {
    let mut stack = vec![root.to_path_buf()];
    while let Some(directory) = stack.pop() {
        let Ok(entries) = std::fs::read_dir(&directory) else {
            continue;
        };
        for entry in entries.flatten() {
            let path = entry.path();
            if !path.is_dir() {
                continue;
            }
            if path
                .file_name()
                .is_some_and(|candidate| candidate.eq_ignore_ascii_case(name))
                && predicate(&path)
            {
                return Some(path);
            }
            stack.push(path);
        }
    }
    None
}

fn find_version(root: &Path) -> Option<String> {
    let mut stack = vec![root.to_path_buf()];
    while let Some(directory) = stack.pop() {
        let Ok(entries) = std::fs::read_dir(&directory) else {
            continue;
        };
        for entry in entries.flatten() {
            let path = entry.path();
            if path.is_dir() {
                stack.push(path);
                continue;
            }
            if !path
                .file_name()
                .is_some_and(|name| name.eq_ignore_ascii_case("nr_fg_package.json"))
            {
                continue;
            }
            let Ok(text) = std::fs::read_to_string(&path) else {
                continue;
            };
            let Ok(json) = serde_json::from_str::<Value>(&text) else {
                continue;
            };
            if let Some(version) = json.get("version").and_then(Value::as_str) {
                return Some(version.to_string());
            }
        }
    }
    None
}

fn copy_directory_contents(source: &Path, destination: &Path) -> Result<(), AppError> {
    std::fs::create_dir_all(destination).map_err(|error| io_error(destination, error))?;
    for entry in std::fs::read_dir(source).map_err(|error| io_error(source, error))? {
        let entry = entry.map_err(|error| io_error(source, error))?;
        let from = entry.path();
        let to = destination.join(entry.file_name());
        if from.is_dir() {
            copy_directory_contents(&from, &to)?;
        } else if from.is_file() {
            std::fs::copy(&from, &to).map_err(|error| io_error(&to, error))?;
        }
    }
    Ok(())
}

fn temp_directory(data_root: &Path) -> PathBuf {
    let nanos = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map_or(0, |value| value.as_nanos());
    data_root
        .join("cache")
        .join(format!("anr-import-{}-{nanos}", std::process::id()))
}

fn io_error(path: &Path, error: std::io::Error) -> AppError {
    let code = match error.kind() {
        std::io::ErrorKind::PermissionDenied => ErrorCode::AccessDenied,
        _ => ErrorCode::Internal,
    };
    AppError::new(code)
        .with("path", path.display().to_string())
        .with("reason", error.to_string())
}
