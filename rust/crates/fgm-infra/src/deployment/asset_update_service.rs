use std::collections::HashSet;
use std::path::{Path, PathBuf};
use std::time::UNIX_EPOCH;

use fgm_domain::deployment::{DeploymentManifest, DeploymentManifestState};
use fgm_domain::error::{AppError, ErrorCode};
use serde::{Deserialize, Serialize};

use crate::dlss5::Dlss5Service;
use crate::fs::AtomicFile;
use crate::pack_json::PackJson;
use crate::packs::PackLocator;
use crate::paths::AppPaths;
use crate::smooth_motion::SmoothMotionService;
use crate::updates::update_checker::is_newer;

use super::DeploymentEngine;

#[derive(Clone, Debug)]
pub struct ClearedAsset {
    pub family: String,
    pub target_directory: String,
    pub old_version: String,
    pub new_version: String,
}

#[derive(Clone, Debug)]
pub struct AssetUpgrade {
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
    pub upgrades: Vec<AssetUpgrade>,
    pub failed: Vec<AssetFailure>,
}

#[derive(Debug, Deserialize, PartialEq, Eq, Serialize)]
struct BackupFingerprint {
    manifests: u64,
    bytes: u64,
    newest_nanos: u128,
    /// Bundled pack versions; a shipped asset update must re-run reconciliation
    /// even though no manifest changed on disk.
    packs: String,
}

/// Detects deployed packs whose version no longer matches the bundled pack,
/// clears those deployments, and leaves redeployment to the user.
pub struct AssetUpdateService;

impl AssetUpdateService {
    pub fn reconcile(paths: &AppPaths) -> AssetUpdateReport {
        let fingerprint = backup_fingerprint(paths);
        if load_fingerprint(paths).as_ref() == Some(&fingerprint) {
            return AssetUpdateReport::default();
        }
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
            if manifest.pack_version.is_empty()
                || !is_newer(&current_version, &manifest.pack_version)
            {
                continue;
            }
            if !seen.insert((effective.to_string(), target.clone())) {
                continue;
            }
            // REFramework is a small, self-contained deployment. Upgrading it
            // in place preserves the user's backup and avoids a clear/redeploy
            // round trip.
            if effective == "reframework" {
                report.upgrades.push(AssetUpgrade {
                    family: effective.to_string(),
                    target_directory: target.display().to_string(),
                    old_version: manifest.pack_version.clone(),
                    new_version: current_version,
                });
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
        if report.failed.is_empty() && report.upgrades.is_empty() {
            save_fingerprint(paths, &backup_fingerprint(paths));
        }
        report
    }
}

/// Forces the next reconcile to inspect manifests again.
pub fn invalidate_fingerprint(paths: &AppPaths) {
    let _ = std::fs::remove_file(fingerprint_path(paths));
}

fn fingerprint_path(paths: &AppPaths) -> PathBuf {
    paths.state().join("assets-fingerprint.json")
}

fn load_fingerprint(paths: &AppPaths) -> Option<BackupFingerprint> {
    let bytes = std::fs::read(fingerprint_path(paths)).ok()?;
    serde_json::from_slice(&bytes).ok()
}

fn save_fingerprint(paths: &AppPaths, fingerprint: &BackupFingerprint) {
    if let Ok(bytes) = serde_json::to_vec_pretty(fingerprint) {
        let _ = AtomicFile::write(&fingerprint_path(paths), &bytes);
    }
}

fn backup_fingerprint(paths: &AppPaths) -> BackupFingerprint {
    let mut fingerprint = BackupFingerprint {
        manifests: 0,
        bytes: 0,
        newest_nanos: 0,
        packs: bundled_versions(),
    };
    let Ok(families) = std::fs::read_dir(paths.backups()) else {
        return fingerprint;
    };
    for family in families.flatten() {
        let Ok(keys) = std::fs::read_dir(family.path()) else {
            continue;
        };
        for key in keys.flatten() {
            let path = key.path().join("manifest.json");
            let Ok(metadata) = std::fs::metadata(&path) else {
                continue;
            };
            fingerprint.manifests += 1;
            fingerprint.bytes += metadata.len();
            if let Ok(modified) = metadata.modified()
                && let Ok(age) = modified.duration_since(UNIX_EPOCH)
            {
                fingerprint.newest_nanos = fingerprint.newest_nanos.max(age.as_nanos());
            }
        }
    }
    fingerprint
}

fn bundled_versions() -> String {
    let mut versions = std::collections::BTreeMap::new();
    for root in PackLocator::roots() {
        let Ok(entries) = std::fs::read_dir(root) else {
            continue;
        };
        for entry in entries.flatten() {
            let directory = entry.path();
            if !directory.is_dir() {
                continue;
            }
            let Some(id) = directory.file_name().map(|name| name.to_string_lossy()) else {
                continue;
            };
            if versions.contains_key(id.as_ref()) {
                continue;
            }
            if let Ok(pack) = PackJson::load(&directory.join("pack.json")) {
                versions.insert(id.into_owned(), pack.version);
            }
        }
    }
    versions
        .into_iter()
        .map(|(id, version)| format!("{id}:{version}"))
        .collect::<Vec<_>>()
        .join(",")
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

#[cfg(test)]
mod tests {
    use super::*;
    use std::time::{SystemTime, UNIX_EPOCH};

    fn paths() -> AppPaths {
        let stamp = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .unwrap()
            .as_nanos();
        AppPaths::new(
            std::env::temp_dir().join(format!("fgm-assets-{stamp}")),
            false,
        )
        .unwrap()
    }

    #[test]
    fn fingerprint_tracks_manifest_changes() {
        let paths = paths();
        assert_eq!(backup_fingerprint(&paths).manifests, 0);
        let directory = paths.backups().join("framegen.sm86").join("key");
        std::fs::create_dir_all(&directory).unwrap();
        std::fs::write(directory.join("manifest.json"), b"{}").unwrap();
        let fingerprint = backup_fingerprint(&paths);
        assert_eq!(fingerprint.manifests, 1);
        assert!(fingerprint.bytes >= 2);
        save_fingerprint(&paths, &fingerprint);
        assert_eq!(load_fingerprint(&paths), Some(fingerprint));
        let _ = std::fs::remove_dir_all(paths.root());
    }
}
