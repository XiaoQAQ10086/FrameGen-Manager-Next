use std::path::{Path, PathBuf};

use fgm_domain::error::{AppError, ErrorCode};

use crate::fs::AtomicFile;

use super::app_settings::AppSettings;

/// Reads and writes `config/app.json`; corrupt files are quarantined.
pub struct SettingsStore {
    path: PathBuf,
}

impl SettingsStore {
    pub fn new(path: impl Into<PathBuf>) -> Self {
        Self { path: path.into() }
    }

    pub fn path(&self) -> &Path {
        &self.path
    }

    pub fn load(&self) -> Result<AppSettings, AppError> {
        let bytes = match std::fs::read(&self.path) {
            Ok(bytes) => bytes,
            Err(error) if error.kind() == std::io::ErrorKind::NotFound => {
                return Ok(AppSettings::default());
            }
            Err(error) => return Err(io_error(&self.path, error)),
        };

        match serde_json::from_slice(&bytes) {
            Ok(settings) => Ok(settings),
            Err(_) => {
                let bad = self.path.with_extension("json.bad");
                let _ = std::fs::remove_file(&bad);
                let _ = std::fs::rename(&self.path, bad);
                Ok(AppSettings::default())
            }
        }
    }

    pub fn save(&self, settings: &AppSettings) -> Result<(), AppError> {
        let bytes = serde_json::to_vec_pretty(settings).map_err(|error| {
            AppError::new(ErrorCode::Internal).with("reason", error.to_string())
        })?;
        AtomicFile::write(&self.path, &bytes)
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
