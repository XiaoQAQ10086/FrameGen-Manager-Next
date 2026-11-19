use std::path::Path;

use fgm_domain::deployment::{DeploymentManifest, DeploymentState};
use fgm_domain::error::{AppError, ErrorCode};
use fgm_domain::pack::AssetPack;

use crate::paths::TargetPath;

use super::backup_store::BackupStore;
use super::deployment_inspection::DeploymentInspection;
use super::deployment_support::hash_file;

/// Read-only deployment state inspection for the library and UI.
pub struct DeploymentInspector;

impl DeploymentInspector {
    pub fn inspect(
        family: &str,
        target_directory: &Path,
        backups_root: &Path,
        pack: Option<&AssetPack>,
    ) -> Result<DeploymentInspection, AppError> {
        if !target_directory.is_absolute() {
            return Err(AppError::new(ErrorCode::InvalidPath)
                .with("path", target_directory.display().to_string())
                .with("reason", "target directory must be absolute"));
        }

        let store = BackupStore::new(backups_root.to_path_buf());
        match store.load_manifest(family, target_directory)? {
            Some(manifest) => inspect_manifest(&manifest, target_directory, pack),
            None => inspect_without_manifest(target_directory, pack),
        }
    }
}

fn inspect_manifest(
    manifest: &DeploymentManifest,
    target_directory: &Path,
    pack: Option<&AssetPack>,
) -> Result<DeploymentInspection, AppError> {
    if manifest.state == fgm_domain::deployment::DeploymentManifestState::Restored {
        return Ok(DeploymentInspection {
            state: DeploymentState::NotDeployed,
            pack_version: Some(manifest.pack_version.clone()),
            has_manifest: true,
            details: vec!["pack files were restored; optional components are kept".to_string()],
            options: Some(manifest.options.clone()),
        });
    }

    let mut conflicts = 0;
    let mut missing = 0;
    let mut details = Vec::new();

    for record in &manifest.files {
        let target = match TargetPath::combine(target_directory, &record.relative_path) {
            Ok(target) => target,
            Err(error) => {
                conflicts += 1;
                details.push(format!(
                    "{}: {}",
                    record.relative_path,
                    first_reason(&error)
                ));
                continue;
            }
        };

        if !target.is_file() {
            missing += 1;
            details.push(format!("{}: missing", record.relative_path));
            continue;
        }

        match hash_file(&target) {
            Ok(hash) if hash.eq_ignore_ascii_case(&record.sha256) => {}
            Ok(_) => {
                conflicts += 1;
                details.push(format!("{}: modified", record.relative_path));
            }
            Err(error) => {
                conflicts += 1;
                details.push(format!(
                    "{}: {}",
                    record.relative_path,
                    first_reason(&error)
                ));
            }
        }
    }

    let outdated = pack.is_some_and(|pack| pack.version != manifest.pack_version);
    let state = if conflicts > 0 {
        DeploymentState::Conflict
    } else if missing > 0 {
        DeploymentState::Partial
    } else if outdated {
        DeploymentState::Outdated
    } else {
        DeploymentState::Installed
    };

    Ok(DeploymentInspection {
        state,
        pack_version: Some(manifest.pack_version.clone()),
        has_manifest: true,
        details,
        options: Some(manifest.options.clone()),
    })
}

fn inspect_without_manifest(
    target_directory: &Path,
    pack: Option<&AssetPack>,
) -> Result<DeploymentInspection, AppError> {
    let Some(pack) = pack else {
        return Ok(DeploymentInspection {
            state: DeploymentState::NotDeployed,
            pack_version: None,
            has_manifest: false,
            details: Vec::new(),
            options: None,
        });
    };

    let mut matches = 0;
    let mut details = Vec::new();
    for file in pack.files.iter().flatten() {
        let target = match TargetPath::combine(target_directory, &file.target) {
            Ok(target) => target,
            Err(_) => continue,
        };
        if !target.is_file() {
            continue;
        }
        let Some(expected) = file.sha256.as_deref() else {
            continue;
        };
        if hash_file(&target).is_ok_and(|hash| hash.eq_ignore_ascii_case(expected)) {
            matches += 1;
            details.push(format!("{}: matches the pack", file.target));
        }
    }

    Ok(DeploymentInspection {
        state: if matches > 0 {
            DeploymentState::InstalledWithoutManifest
        } else {
            DeploymentState::NotDeployed
        },
        pack_version: None,
        has_manifest: false,
        details,
        options: None,
    })
}

fn first_reason(error: &AppError) -> &str {
    error
        .context
        .iter()
        .find(|(key, _)| key == "reason")
        .map_or("inspection failed", |(_, value)| value.as_str())
}
