//! Pending ReShade overlay hotkeys for games where DLSS5 is not installed yet.

use std::collections::BTreeMap;
use std::path::{Path, PathBuf};

use fgm_domain::error::{AppError, ErrorCode};

use crate::fs::AtomicFile;
use crate::paths::AppPaths;

/// Stores a hotkey chosen before deployment and applies it during install.
pub struct Dlss5HotkeyStore;

impl Dlss5HotkeyStore {
    fn path(paths: &AppPaths) -> PathBuf {
        paths.state().join("dlss5-hotkeys.json")
    }

    fn key(target_directory: &Path) -> String {
        target_directory
            .display()
            .to_string()
            .replace('/', "\\")
            .to_ascii_lowercase()
    }

    fn load(paths: &AppPaths) -> BTreeMap<String, u32> {
        std::fs::read(Self::path(paths))
            .ok()
            .and_then(|bytes| serde_json::from_slice(&bytes).ok())
            .unwrap_or_default()
    }

    fn save_all(paths: &AppPaths, values: &BTreeMap<String, u32>) -> Result<(), AppError> {
        let path = Self::path(paths);
        if values.is_empty() {
            let _ = std::fs::remove_file(&path);
            return Ok(());
        }
        let bytes = serde_json::to_vec_pretty(values).map_err(|error| {
            AppError::new(ErrorCode::Internal).with("reason", error.to_string())
        })?;
        AtomicFile::write(&path, &bytes)
    }

    pub fn pending(target_directory: &Path, paths: &AppPaths) -> Option<u32> {
        Self::load(paths).get(&Self::key(target_directory)).copied()
    }

    pub fn save(
        target_directory: &Path,
        virtual_key: u32,
        paths: &AppPaths,
    ) -> Result<(), AppError> {
        let mut values = Self::load(paths);
        values.insert(Self::key(target_directory), virtual_key);
        Self::save_all(paths, &values)
    }

    pub fn clear(target_directory: &Path, paths: &AppPaths) {
        let mut values = Self::load(paths);
        if values.remove(&Self::key(target_directory)).is_some() {
            let _ = Self::save_all(paths, &values);
        }
    }
}
