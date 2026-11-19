use std::path::{Path, PathBuf};

use fgm_domain::error::{AppError, ErrorCode};

use crate::fs::AtomicFile;

use super::library_cache::LibraryCache;

/// Loads and saves `library/library.json` without touching game directories.
pub struct LibraryStore {
    path: PathBuf,
}

impl LibraryStore {
    pub fn new(path: impl Into<PathBuf>) -> Self {
        Self { path: path.into() }
    }

    pub fn path(&self) -> &Path {
        &self.path
    }

    pub fn load(&self) -> Result<LibraryCache, AppError> {
        let bytes = match std::fs::read(&self.path) {
            Ok(bytes) => bytes,
            Err(error) if error.kind() == std::io::ErrorKind::NotFound => {
                return Ok(LibraryCache::default());
            }
            Err(error) => return Err(io_error(&self.path, error)),
        };

        match serde_json::from_slice(&bytes) {
            Ok(cache) => Ok(cache),
            Err(_) => {
                quarantine(&self.path);
                Ok(LibraryCache::default())
            }
        }
    }

    pub fn save(&self, cache: &LibraryCache) -> Result<(), AppError> {
        let bytes = serde_json::to_vec_pretty(cache).map_err(|error| {
            AppError::new(ErrorCode::Internal).with("reason", error.to_string())
        })?;
        AtomicFile::write(&self.path, &bytes)
    }
}

fn quarantine(path: &Path) -> Option<PathBuf> {
    let bad = path.with_extension("json.bad");
    let _ = std::fs::remove_file(&bad);
    std::fs::rename(path, &bad).ok().map(|()| bad)
}

fn io_error(path: &Path, error: std::io::Error) -> AppError {
    let code = match error.kind() {
        std::io::ErrorKind::NotFound => ErrorCode::FileNotFound,
        std::io::ErrorKind::PermissionDenied => ErrorCode::AccessDenied,
        _ => ErrorCode::Internal,
    };
    AppError::new(code)
        .with("path", path.display().to_string())
        .with("reason", error.to_string())
}
