use std::path::{Path, PathBuf};

use fgm_domain::deployment::DeploymentManifest;
use fgm_domain::error::{AppError, ErrorCode};
use fgm_domain::safety::SafePath;

use crate::fs::{AtomicFile, DeploymentKey};
use crate::paths::TargetPath;

/// Owns backup manifests and their directory layout.
pub struct BackupStore {
    root: PathBuf,
}

impl BackupStore {
    pub fn new(root: impl Into<PathBuf>) -> Self {
        Self { root: root.into() }
    }

    pub fn root(&self) -> &Path {
        &self.root
    }

    pub fn directory(&self, family: &str, target: &Path) -> Result<PathBuf, AppError> {
        let family = SafePath::normalize_relative(family)
            .map(|value| value.replace('/', "_"))
            .unwrap_or_else(|_| "unknown".to_string());

        let key = DeploymentKey::for_path(target)?;
        let current = self.root.join(&family).join(&key);
        // C# 2.x recorded frame generation under `framegen`; the Rust engine
        // uses the pack id. Keep reading the old directory so upgrades retain
        // manifests, versions and backups.
        if family == "framegen.sm86" && !current.exists() {
            let legacy = self.root.join("framegen").join(&key);
            if legacy.exists() {
                return Ok(legacy);
            }
        }
        Ok(current)
    }

    pub fn manifest_path(&self, family: &str, target: &Path) -> Result<PathBuf, AppError> {
        Ok(self.directory(family, target)?.join("manifest.json"))
    }

    pub fn load_manifest(
        &self,
        family: &str,
        target: &Path,
    ) -> Result<Option<DeploymentManifest>, AppError> {
        let path = self.manifest_path(family, target)?;
        let bytes = match std::fs::read(&path) {
            Ok(bytes) => bytes,
            Err(error) if error.kind() == std::io::ErrorKind::NotFound => return Ok(None),
            Err(error) => return Err(io_error(&path, error)),
        };

        match serde_json::from_slice::<DeploymentManifest>(&bytes) {
            Ok(manifest) => Ok(Some(manifest)),
            Err(_) => {
                quarantine(&path);
                Ok(None)
            }
        }
    }

    pub fn save_manifest(&self, manifest: &DeploymentManifest) -> Result<(), AppError> {
        if manifest.family_id.trim().is_empty() || manifest.target_directory.trim().is_empty() {
            return Err(AppError::new(ErrorCode::InvalidArgument).with(
                "reason",
                "manifest family and target directory are required",
            ));
        }

        let target = Path::new(&manifest.target_directory);
        if !target.is_absolute() {
            return Err(AppError::new(ErrorCode::InvalidArgument)
                .with("path", manifest.target_directory.clone())
                .with("reason", "manifest target directory must be absolute"));
        }

        let directory = self.directory(&manifest.family_id, target)?;
        std::fs::create_dir_all(&directory).map_err(|error| io_error(&directory, error))?;

        let bytes = serde_json::to_vec_pretty(manifest).map_err(|error| {
            AppError::new(ErrorCode::Internal).with("reason", error.to_string())
        })?;
        AtomicFile::write(&directory.join("manifest.json"), &bytes)
    }

    pub fn backup_file_path(
        &self,
        backup_directory: &Path,
        relative_path: &str,
    ) -> Result<PathBuf, AppError> {
        TargetPath::combine(backup_directory, relative_path)
    }

    /// Copies a target file into the backup directory using an atomic replace.
    pub fn store_file(
        &self,
        backup_directory: &Path,
        relative_path: &str,
        source: &Path,
    ) -> Result<PathBuf, AppError> {
        let destination = self.backup_file_path(backup_directory, relative_path)?;
        AtomicFile::copy(source, &destination)?;
        Ok(destination)
    }

    /// Restores a backed-up file over its target using an atomic replace.
    pub fn restore_file(
        &self,
        backup_directory: &Path,
        relative_path: &str,
        target: &Path,
    ) -> Result<(), AppError> {
        let source = self.backup_file_path(backup_directory, relative_path)?;
        if !source.is_file() {
            return Err(AppError::new(ErrorCode::FileNotFound)
                .with("path", source.display().to_string())
                .with("reason", "backup file is missing"));
        }
        AtomicFile::copy(&source, target)
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
