use std::path::{Path, PathBuf};

use fgm_domain::error::{AppError, ErrorCode};
use serde::{Deserialize, Serialize};

use crate::fs::AtomicFile;
use crate::paths::AppPaths;

/// One deferred `ReShade.ini` hotkey write.
#[derive(Clone, Debug, Deserialize, Serialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct HotkeyRequest {
    pub target_dir: String,
    #[serde(default)]
    pub exe_path: Option<String>,
    pub virtual_key: u32,
}

/// `state/hotkey-queue.json`: retried by the UI every 30 seconds.
pub struct HotkeyQueue;

impl HotkeyQueue {
    pub fn path(paths: &AppPaths) -> PathBuf {
        paths.state().join("hotkey-queue.json")
    }

    pub fn load(paths: &AppPaths) -> Result<Vec<HotkeyRequest>, AppError> {
        let path = Self::path(paths);
        let bytes = match std::fs::read(&path) {
            Ok(bytes) => bytes,
            Err(error) if error.kind() == std::io::ErrorKind::NotFound => return Ok(Vec::new()),
            Err(error) => {
                return Err(AppError::new(ErrorCode::FileNotFound)
                    .with("path", path.display().to_string())
                    .with("reason", error.to_string()));
            }
        };
        serde_json::from_slice(&bytes).map_err(|error| {
            AppError::new(ErrorCode::Internal)
                .with("path", path.display().to_string())
                .with("reason", error.to_string())
        })
    }

    pub fn save(paths: &AppPaths, entries: &[HotkeyRequest]) -> Result<(), AppError> {
        let path = Self::path(paths);
        if entries.is_empty() {
            let _ = std::fs::remove_file(&path);
            return Ok(());
        }
        let bytes = serde_json::to_vec_pretty(entries).map_err(|error| {
            AppError::new(ErrorCode::Internal).with("reason", error.to_string())
        })?;
        AtomicFile::write(&path, &bytes)
    }

    pub fn enqueue(
        paths: &AppPaths,
        target_directory: &Path,
        exe_path: Option<&Path>,
        virtual_key: u32,
    ) -> Result<(), AppError> {
        let mut entries = Self::load(paths)?;
        let target = target_directory.to_string_lossy().into_owned();
        entries.retain(|entry| entry.target_dir != target);
        entries.push(HotkeyRequest {
            target_dir: target,
            exe_path: exe_path.map(|path| path.to_string_lossy().into_owned()),
            virtual_key,
        });
        Self::save(paths, &entries)
    }
}
