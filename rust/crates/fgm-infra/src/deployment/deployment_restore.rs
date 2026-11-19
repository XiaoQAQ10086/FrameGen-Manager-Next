use std::collections::BTreeSet;
use std::path::{Path, PathBuf};

use fgm_domain::deployment::DeploymentManifestState;
use fgm_domain::error::{AppError, ErrorCode};

use super::backup_store::BackupStore;
use super::deployment_support::{
    acquire_target_lock, hash_file, io_error, prune_empty_directories, validate_target_directory,
};
use super::restore_report::RestoreReport;

impl super::deployment_engine::DeploymentEngine {
    pub fn restore(
        family: &str,
        target_directory: &Path,
        backups_root: &Path,
        lock_directory: &Path,
    ) -> Result<RestoreReport, AppError> {
        Self::restore_keeping(family, target_directory, backups_root, lock_directory, &[])
    }

    /// Restores the deployment while leaving records whose role is listed in
    /// `keep_roles` in place (for example the ReShade proxy and its INI).
    pub fn restore_keeping(
        family: &str,
        target_directory: &Path,
        backups_root: &Path,
        lock_directory: &Path,
        keep_roles: &[&str],
    ) -> Result<RestoreReport, AppError> {
        validate_target_directory(target_directory)?;
        let _lock = acquire_target_lock(lock_directory, family, target_directory)?;

        let store = BackupStore::new(backups_root.to_path_buf());
        let Some(mut manifest) = store.load_manifest(family, target_directory)? else {
            return Err(AppError::new(ErrorCode::ManifestMissing)
                .with("path", target_directory.display().to_string())
                .with("family", family));
        };

        let backup_directory = store.directory(family, target_directory)?;
        let mut report = RestoreReport::default();
        let mut touched_directories: BTreeSet<PathBuf> = BTreeSet::new();
        let kept: Vec<_> = manifest
            .files
            .iter()
            .filter(|record| {
                keep_roles
                    .iter()
                    .any(|role| record.role.eq_ignore_ascii_case(role))
            })
            .cloned()
            .collect();

        for record in manifest.files.iter().filter(|record| {
            !keep_roles
                .iter()
                .any(|role| record.role.eq_ignore_ascii_case(role))
        }) {
            let target =
                match crate::paths::TargetPath::combine(target_directory, &record.relative_path) {
                    Ok(target) => target,
                    Err(error) => {
                        report.warnings.push(format!(
                            "{}: {}",
                            record.relative_path,
                            first_reason(&error)
                        ));
                        continue;
                    }
                };

            let backup = record.backup_relative_path.as_deref().filter(|relative| {
                store
                    .backup_file_path(&backup_directory, relative)
                    .is_ok_and(|path| path.is_file())
            });

            if target.is_file() {
                let current = hash_file(&target)?;
                if !current.eq_ignore_ascii_case(&record.sha256) {
                    report.conflicts.push(record.relative_path.clone());
                    continue;
                }

                if let Some(relative) = backup {
                    store.restore_file(&backup_directory, relative, &target)?;
                    report.restored_files += 1;
                } else {
                    std::fs::remove_file(&target).map_err(|error| io_error(&target, error))?;
                    report.removed_files += 1;
                }
                if let Some(parent) = target.parent() {
                    touched_directories.insert(parent.to_path_buf());
                }
                continue;
            }

            if target.exists() {
                report.conflicts.push(record.relative_path.clone());
                continue;
            }

            if let Some(relative) = backup {
                store.restore_file(&backup_directory, relative, &target)?;
                report.restored_files += 1;
            } else {
                report.warnings.push(format!(
                    "{}: deployed file and backup are missing",
                    record.relative_path
                ));
            }
        }

        if !report.conflicts.is_empty() {
            manifest.state = DeploymentManifestState::Partial;
            store.save_manifest(&manifest)?;
        } else {
            for relative in &manifest.owned_directories {
                match crate::paths::TargetPath::combine(target_directory, relative) {
                    Ok(path) if path.is_dir() => {
                        if let Err(error) = std::fs::remove_dir_all(&path) {
                            report.warnings.push(format!(
                                "{relative}: could not remove owned directory ({error})"
                            ));
                        }
                    }
                    Ok(_) => {}
                    Err(error) => {
                        report
                            .warnings
                            .push(format!("{relative}: {}", first_reason(&error)));
                    }
                }
            }

            if kept.is_empty() {
                let manifest_path = store.manifest_path(family, target_directory)?;
                let _ = std::fs::remove_file(manifest_path);
                if let Ok(directory) = store.directory(family, target_directory) {
                    let _ = std::fs::remove_dir(directory);
                }
            } else {
                manifest.files = kept;
                manifest.state = DeploymentManifestState::Restored;
                store.save_manifest(&manifest)?;
            }
        }

        if report.conflicts.is_empty() {
            prune_empty_directories(target_directory, &touched_directories);
        }

        Ok(report)
    }
}

fn first_reason(error: &AppError) -> &str {
    error
        .context
        .iter()
        .find(|(key, _)| key == "reason")
        .map_or("invalid path", |(_, value)| value.as_str())
}
