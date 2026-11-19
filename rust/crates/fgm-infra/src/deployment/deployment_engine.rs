use std::collections::{BTreeMap, HashMap, HashSet};
use std::path::{Path, PathBuf};
use std::sync::atomic::{AtomicBool, Ordering};

use fgm_domain::deployment::{DeployedFileRecord, DeploymentManifest, DeploymentManifestState};
use fgm_domain::error::{AppError, ErrorCode};

use crate::fs::AtomicFile;

use super::backup_store::BackupStore;
use super::deployment_file::DeploymentFile;
use super::deployment_report::DeploymentReport;
use super::deployment_support::{
    acquire_target_lock, backup_relative_path, ensure_sources_match, hash_file, io_error,
    now_unix_ms, validate_target_directory,
};

/// Copies planned files into a target directory with backups, manifest state
/// transitions and rollback on failure.
pub struct DeploymentEngine;

impl DeploymentEngine {
    #[allow(clippy::too_many_arguments)]
    pub fn apply(
        files: &[DeploymentFile],
        target_directory: &Path,
        family: &str,
        pack_id: &str,
        pack_version: &str,
        options: &BTreeMap<String, String>,
        backups_root: &Path,
        lock_directory: &Path,
        cancelled: &AtomicBool,
    ) -> Result<DeploymentReport, AppError> {
        validate_target_directory(target_directory)?;
        let _lock = acquire_target_lock(lock_directory, family, target_directory)?;
        if cancelled.load(Ordering::Relaxed) {
            return Err(cancelled_error(family));
        }

        let store = BackupStore::new(backups_root.to_path_buf());
        let previous = store.load_manifest(family, target_directory)?;
        let previous_by_path: HashMap<String, DeployedFileRecord> = previous
            .as_ref()
            .map(|manifest| {
                manifest
                    .files
                    .iter()
                    .map(|record| (record.relative_path.to_ascii_lowercase(), record.clone()))
                    .collect()
            })
            .unwrap_or_default();

        let planned = plan_files(files, target_directory)?;
        ensure_sources_match(&planned)?;

        let backup_directory = store.directory(family, target_directory)?;
        std::fs::create_dir_all(&backup_directory)
            .map_err(|error| io_error(&backup_directory, error))?;

        let mut records = Vec::with_capacity(planned.len());
        let mut files_backed_up = 0;

        for (file, target) in &planned {
            let record = prepare_record(
                &store,
                &backup_directory,
                &previous_by_path,
                file,
                target,
                &mut files_backed_up,
            )?;
            records.push(record);
        }

        let mut manifest = previous.unwrap_or_else(DeploymentManifest::default);
        manifest.schema_version = 1;
        manifest.family_id = family.to_string();
        manifest.pack_id = pack_id.to_string();
        manifest.pack_version = pack_version.to_string();
        manifest.target_directory = target_directory.display().to_string();
        manifest.created_unix_ms = if manifest.created_unix_ms == 0 {
            now_unix_ms()
        } else {
            manifest.created_unix_ms
        };
        manifest.state = DeploymentManifestState::BackingUp;
        manifest.files = records.clone();
        manifest.options = options.clone();
        store.save_manifest(&manifest)?;

        manifest.state = DeploymentManifestState::Writing;
        store.save_manifest(&manifest)?;

        let write_result = write_files(&planned, cancelled, family);
        if let Err(error) = write_result {
            let complete = rollback(&planned, &records, &store, &backup_directory);
            manifest.state = if complete {
                DeploymentManifestState::RolledBack
            } else {
                DeploymentManifestState::Partial
            };
            let _ = store.save_manifest(&manifest);
            return Err(error.with("rollback", if complete { "complete" } else { "partial" }));
        }

        manifest.state = DeploymentManifestState::Installed;
        if let Err(error) = store.save_manifest(&manifest) {
            let complete = rollback(&planned, &records, &store, &backup_directory);
            manifest.state = if complete {
                DeploymentManifestState::RolledBack
            } else {
                DeploymentManifestState::Partial
            };
            let _ = store.save_manifest(&manifest);
            return Err(error.with("rollback", if complete { "complete" } else { "partial" }));
        }

        Ok(DeploymentReport {
            state: DeploymentManifestState::Installed,
            files_written: planned.len(),
            files_backed_up,
            warnings: Vec::new(),
        })
    }
}

fn plan_files(
    files: &[DeploymentFile],
    target_directory: &Path,
) -> Result<Vec<(DeploymentFile, PathBuf)>, AppError> {
    let mut planned = Vec::with_capacity(files.len());
    let mut seen = HashSet::new();

    for file in files {
        let key = file.relative_path.to_ascii_lowercase();
        if !seen.insert(key) {
            return Err(AppError::new(ErrorCode::Conflict)
                .with("path", file.relative_path.clone())
                .with("reason", "duplicate deployment target"));
        }

        let target = crate::paths::TargetPath::combine(target_directory, &file.relative_path)?;
        planned.push((file.clone(), target));
    }

    Ok(planned)
}

fn prepare_record(
    store: &BackupStore,
    backup_directory: &Path,
    previous: &HashMap<String, DeployedFileRecord>,
    file: &DeploymentFile,
    target: &Path,
    files_backed_up: &mut usize,
) -> Result<DeployedFileRecord, AppError> {
    if target.exists() && !target.is_file() {
        return Err(AppError::new(ErrorCode::Conflict)
            .with("path", target.display().to_string())
            .with("reason", "deployment target is a directory"));
    }

    let mut record = DeployedFileRecord {
        relative_path: file.relative_path.clone(),
        sha256: file.sha256.clone(),
        size: file.size,
        existed_before: false,
        backup_relative_path: None,
        role: file.role.clone(),
        last_write_time_utc_ticks: None,
    };

    if !target.is_file() {
        return Ok(record);
    }

    let current = hash_file(target)?;
    let reusable = previous
        .get(&file.relative_path.to_ascii_lowercase())
        .and_then(|old| old.backup_relative_path.clone())
        .filter(|relative| {
            store
                .backup_file_path(backup_directory, relative)
                .is_ok_and(|path| path.is_file())
        });

    if current.eq_ignore_ascii_case(&file.sha256) {
        if let Some(relative) = reusable {
            record.existed_before = true;
            record.backup_relative_path = Some(relative);
        }
        return Ok(record);
    }

    record.existed_before = true;
    record.backup_relative_path = Some(if let Some(relative) = reusable {
        relative
    } else {
        let relative = backup_relative_path(&file.relative_path);
        store.store_file(backup_directory, &relative, target)?;
        *files_backed_up += 1;
        relative
    });
    Ok(record)
}

fn write_files(
    planned: &[(DeploymentFile, PathBuf)],
    cancelled: &AtomicBool,
    family: &str,
) -> Result<(), AppError> {
    for (file, target) in planned {
        if cancelled.load(Ordering::Relaxed) {
            return Err(cancelled_error(family));
        }

        AtomicFile::copy(&file.source, target)?;
        let actual = hash_file(target)?;
        if !actual.eq_ignore_ascii_case(&file.sha256) {
            return Err(AppError::new(ErrorCode::PackHashMismatch)
                .with("path", target.display().to_string())
                .with("reason", "deployed file hash does not match the plan"));
        }
    }
    Ok(())
}

fn rollback(
    planned: &[(DeploymentFile, PathBuf)],
    records: &[DeployedFileRecord],
    store: &BackupStore,
    backup_directory: &Path,
) -> bool {
    let mut complete = true;

    for ((_, target), record) in planned.iter().zip(records).rev() {
        if record.existed_before {
            let Some(relative) = record.backup_relative_path.as_deref() else {
                complete = false;
                continue;
            };
            if store
                .restore_file(backup_directory, relative, target)
                .is_err()
            {
                complete = false;
            }
        } else if target.exists() && std::fs::remove_file(target).is_err() {
            complete = false;
        }
    }

    complete
}

fn cancelled_error(family: &str) -> AppError {
    AppError::new(ErrorCode::OperationCancelled).with("family", family)
}
