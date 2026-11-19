use std::collections::BTreeSet;
use std::path::{Path, PathBuf};
use std::time::{SystemTime, UNIX_EPOCH};

use fgm_domain::error::{AppError, ErrorCode};

use crate::fs::TargetLock;
use crate::hashing::FileHasher;
use crate::paths::TargetPath;

pub(super) fn now_unix_ms() -> i64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map_or(0, |value| value.as_millis() as i64)
}

pub(super) fn validate_target_directory(target: &Path) -> Result<(), AppError> {
    if !target.is_absolute() {
        return Err(AppError::new(ErrorCode::InvalidPath)
            .with("path", target.display().to_string())
            .with("reason", "target directory must be absolute"));
    }
    if !target.is_dir() {
        return Err(AppError::new(ErrorCode::FileNotFound)
            .with("path", target.display().to_string())
            .with("reason", "target directory does not exist"));
    }
    if TargetPath::is_protected_system_directory(target) {
        return Err(AppError::new(ErrorCode::UnsafePath)
            .with("path", target.display().to_string())
            .with("reason", "target is a protected system directory"));
    }
    Ok(())
}

pub(super) fn acquire_target_lock(
    lock_directory: &Path,
    family: &str,
    target: &Path,
) -> Result<TargetLock, AppError> {
    TargetLock::try_acquire(lock_directory, family, target)?.ok_or_else(|| {
        AppError::new(ErrorCode::FileLocked)
            .with("path", target.display().to_string())
            .with("reason", "another deployment holds the target lock")
    })
}

pub(super) fn hash_file(path: &Path) -> Result<String, AppError> {
    FileHasher::sha256_file(path)
}

pub(super) fn ensure_sources_match(
    files: &[(super::deployment_file::DeploymentFile, std::path::PathBuf)],
) -> Result<(), AppError> {
    for (file, _) in files {
        if let Some(source) = &file.source {
            if !source.is_file() {
                return Err(AppError::new(ErrorCode::FileNotFound)
                    .with("path", source.display().to_string())
                    .with("reason", "pack source file is missing"));
            }
            let metadata = std::fs::metadata(source).map_err(|error| io_error(source, error))?;
            if metadata.len() as i64 != file.size {
                return Err(AppError::new(ErrorCode::PackHashMismatch)
                    .with("path", source.display().to_string())
                    .with("reason", "pack source size does not match pack.json"));
            }
            let actual = hash_file(source)?;
            if !actual.eq_ignore_ascii_case(&file.sha256) {
                return Err(AppError::new(ErrorCode::PackHashMismatch)
                    .with("path", source.display().to_string())
                    .with("reason", "pack source hash does not match pack.json"));
            }
        } else if let Some(content) = &file.content {
            if content.len() as i64 != file.size {
                return Err(AppError::new(ErrorCode::PackHashMismatch)
                    .with("path", file.relative_path.clone())
                    .with("reason", "generated file size does not match the plan"));
            }
            let actual = crate::hashing::sha256_hex(content);
            if !actual.eq_ignore_ascii_case(&file.sha256) {
                return Err(AppError::new(ErrorCode::PackHashMismatch)
                    .with("path", file.relative_path.clone())
                    .with("reason", "generated file hash does not match the plan"));
            }
        } else {
            return Err(AppError::new(ErrorCode::InvalidArgument)
                .with("path", file.relative_path.clone())
                .with("reason", "deployment file has no source or content"));
        }
    }
    Ok(())
}

pub(super) fn backup_relative_path(relative_path: &str) -> String {
    format!("files/{relative_path}")
}

pub(super) fn io_error(path: &Path, error: std::io::Error) -> AppError {
    let code = match error.kind() {
        std::io::ErrorKind::NotFound => ErrorCode::FileNotFound,
        std::io::ErrorKind::PermissionDenied => ErrorCode::AccessDenied,
        _ => ErrorCode::Internal,
    };
    AppError::new(code)
        .with("path", path.display().to_string())
        .with("reason", error.to_string())
}

pub(crate) fn prune_empty_directories(target_root: &Path, directories: &BTreeSet<PathBuf>) {
    let mut ordered: Vec<&PathBuf> = directories.iter().collect();
    ordered.sort_by_key(|path| std::cmp::Reverse(path.components().count()));
    for directory in ordered {
        let Ok(relative) = directory.strip_prefix(target_root) else {
            continue;
        };
        if relative.as_os_str().is_empty() {
            continue;
        }
        let mut current = Some(directory.as_path());
        while let Some(path) = current {
            if path == target_root {
                break;
            }
            if !path.exists() {
                current = path.parent();
                continue;
            }
            if std::fs::remove_dir(path).is_err() {
                break;
            }
            current = path.parent();
        }
    }
}
