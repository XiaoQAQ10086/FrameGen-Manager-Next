use std::collections::HashSet;
use std::path::{Path, PathBuf};

use fgm_domain::deployment::{DeploymentManifest, DeploymentManifestState};
use fgm_domain::error::{AppError, ErrorCode};

use crate::dlss5::Dlss5Service;
use crate::pack_json::PackJson;
use crate::packs::PackLocator;
use crate::paths::AppPaths;
use crate::smooth_motion::SmoothMotionService;

use super::DeploymentEngine;

#[derive(Clone, Debug)]
pub struct ClearedAsset {
    pub family: String,
    pub target_directory: String,
    pub old_version: String,
    pub new_version: String,
}

#[derive(Clone, Debug)]
pub struct AssetFailure {
    pub family: String,
    pub target_directory: String,
    pub code: String,
    pub reason: String,
}

#[derive(Clone, Debug, Default)]
pub struct AssetUpdateReport {
    pub cleared: Vec<ClearedAsset>,
    pub failed: Vec<AssetFailure>,
}

/// Detects deployed packs whose version no longer matches the bundled pack,
/// clears those deployments, and leaves redeployment to the user.
pub struct AssetUpdateService;

impl AssetUpdateService {
    pub fn reconcile(paths: &AppPaths) -> AssetUpdateReport {
        let mut report = AssetUpdateReport::default();
        let mut seen = HashSet::new();
        for (family, target, manifest) in manifests(paths) {
            let effective = effective_family(&family);
            if !matches!(
                manifest.state,
                DeploymentManifestState::Installed
                    | DeploymentManifestState::Partial
                    | DeploymentManifestState::Installing
            ) {
                continue;
            }
            let Some(pack_id) = pack_id(effective, &manifest) else {
                continue;
            };
            let Some(current_version) = pack_version(&pack_id) else {
                continue;
            };
            if manifest.pack_version.is_empty() || manifest.pack_version == current_version {
                continue;
            }
            if !seen.insert((effective.to_string(), target.clone())) {
                continue;
            }
            match clear(effective, &target, paths) {
                Ok(()) => report.cleared.push(ClearedAsset {
                    family: effective.to_string(),
                    target_directory: target.display().to_string(),
                    old_version: manifest.pack_version.clone(),
                    new_version: current_version,
                }),
                Err(error) => report.failed.push(AssetFailure {
                    family: effective.to_string(),
                    target_directory: target.display().to_string(),
                    code: format!("{:?}", error.code),
                    reason: first_reason(&error),
                }),
            }
        }
        report
    }
}

fn manifests(paths: &AppPaths) -> Vec<(String, PathBuf, DeploymentManifest)> {
    let mut found = Vec::new();
    let Ok(entries) = std::fs::read_dir(paths.backups()) else {
        return found;
    };
    for family_entry in entries.flatten() {
        if !family_entry.path().is_dir() {
            continue;
        }
        let family = family_entry.file_name().to_string_lossy().into_owned();
        let Ok(keys) = std::fs::read_dir(family_entry.path()) else {
            continue;
        };
        for key_entry in keys.flatten() {
            let path = key_entry.path().join("manifest.json");
            let Ok(bytes) = std::fs::read(&path) else {
                continue;
            };
            let Ok(manifest) = serde_json::from_slice::<DeploymentManifest>(&bytes) else {
                continue;
            };
            let Ok(target) = std::path::absolute(Path::new(&manifest.target_directory)) else {
                continue;
            };
            found.push((family.clone(), target, manifest));
        }
    }
    found
}

fn effective_family(family: &str) -> &str {
    if family == "framegen" || family == "framegen.sm86" {
        "framegen.sm86"
    } else {
        family
    }
}

fn pack_id(family: &str, manifest: &DeploymentManifest) -> Option<String> {
    match family {
        "framegen.sm86" => Some("framegen.sm86".to_string()),
        "smooth-motion" => Some(manifest.pack_id.clone()).filter(|id| !id.is_empty()),
        "dlss5" => Some(Dlss5Service::PACK_ID.to_string()),
        "reframework" => Some("reframework".to_string()),
        _ => None,
    }
}

fn pack_version(pack_id: &str) -> Option<String> {
    let directory = PackLocator::find(pack_id)?;
    PackJson::load(&directory.join("pack.json"))
        .ok()
        .map(|pack| pack.version)
}

fn clear(family: &str, target: &Path, paths: &AppPaths) -> Result<(), AppError> {
    if family == "dlss5" {
        let report = Dlss5Service::uninstall(target, paths)?;
        if report.conflicts.is_empty() {
            return Ok(());
        }
        return Err(AppError::new(ErrorCode::Conflict).with("reason", report.conflicts.join(", ")));
    }
    let report = DeploymentEngine::restore(
        family,
        target,
        &paths.backups(),
        &paths.state().join("locks"),
    )?;
    if family == "smooth-motion" {
        let executable = target.join("game.exe");
        let _ =
            SmoothMotionService::restore(&target.display().to_string(), &executable, paths.root());
        let _ = std::fs::remove_dir_all(target.join("RTXMFG"));
    }
    if report.conflicts.is_empty() {
        Ok(())
    } else {
        Err(AppError::new(ErrorCode::Conflict).with("reason", report.conflicts.join(", ")))
    }
}

fn first_reason(error: &AppError) -> String {
    error
        .context
        .iter()
        .find(|(key, _)| key == "reason")
        .map_or_else(|| format!("{:?}", error.code), |(_, value)| value.clone())
}
