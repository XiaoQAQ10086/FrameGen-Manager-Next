use std::path::{Path, PathBuf};

use fgm_domain::error::{AppError, ErrorCode};

use crate::fs::{AtomicFile, DeploymentKey};

use super::sm_profile_record::SmProfileRecord;

/// Stores the pre-existing Smooth Motion values per target directory.
pub struct SmProfileStore {
    root: PathBuf,
}

impl SmProfileStore {
    pub fn new(root: impl Into<PathBuf>) -> Self {
        Self { root: root.into() }
    }

    pub fn path_for(&self, target_directory: &Path) -> Result<PathBuf, AppError> {
        Ok(self.root.join(format!(
            "{}.json",
            DeploymentKey::for_path(target_directory)?
        )))
    }

    pub fn load(&self, target_directory: &Path) -> Result<Option<SmProfileRecord>, AppError> {
        let path = self.path_for(target_directory)?;
        let bytes = match std::fs::read(&path) {
            Ok(bytes) => bytes,
            Err(error) if error.kind() == std::io::ErrorKind::NotFound => return Ok(None),
            Err(error) => return Err(io_error(&path, error)),
        };
        match serde_json::from_slice(&bytes) {
            Ok(record) => Ok(Some(record)),
            Err(_) => {
                let bad = path.with_extension("json.bad");
                let _ = std::fs::remove_file(&bad);
                let _ = std::fs::rename(&path, bad);
                Ok(None)
            }
        }
    }

    pub fn save(&self, record: &SmProfileRecord) -> Result<(), AppError> {
        let target = Path::new(&record.target_directory);
        let path = self.path_for(target)?;
        let bytes = serde_json::to_vec_pretty(record).map_err(|error| {
            AppError::new(ErrorCode::Internal).with("reason", error.to_string())
        })?;
        AtomicFile::write(&path, &bytes)
    }

    pub fn delete(&self, target_directory: &Path) -> Result<(), AppError> {
        let path = self.path_for(target_directory)?;
        match std::fs::remove_file(&path) {
            Ok(()) => Ok(()),
            Err(error) if error.kind() == std::io::ErrorKind::NotFound => Ok(()),
            Err(error) => Err(io_error(&path, error)),
        }
    }
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
