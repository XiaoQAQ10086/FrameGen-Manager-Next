use std::collections::BTreeMap;
use std::path::Path;

use fgm_domain::deployment::{DeploymentManifest, DeploymentManifestState, DeploymentState};

use super::backup_store::BackupStore;
use super::proxy_status::{ProxyEntry, ProxyStatus};

const FAMILIES: [&str; 5] = [
    "framegen.sm86",
    "smooth-motion",
    "dlss5",
    "anr",
    "reframework",
];

/// Loads every deployment manifest for one game directory exactly once.
pub struct DeploymentSnapshot {
    manifests: BTreeMap<&'static str, Option<DeploymentManifest>>,
}

impl DeploymentSnapshot {
    pub fn new(target_directory: &Path, backups_root: &Path) -> Self {
        let store = BackupStore::new(backups_root.to_path_buf());
        let manifests = FAMILIES
            .into_iter()
            .map(|family| {
                (
                    family,
                    store.load_manifest(family, target_directory).ok().flatten(),
                )
            })
            .collect();
        Self { manifests }
    }

    pub fn state(&self, family: &str, current_pack_version: Option<&str>) -> DeploymentState {
        let Some(manifest) = self.manifests.get(family).and_then(Option::as_ref) else {
            return DeploymentState::NotDeployed;
        };
        match manifest.state {
            DeploymentManifestState::Installed => {
                if current_pack_version.is_some_and(|version| version != manifest.pack_version) {
                    DeploymentState::Outdated
                } else {
                    DeploymentState::Installed
                }
            }
            DeploymentManifestState::Partial
            | DeploymentManifestState::Installing
            | DeploymentManifestState::BackingUp
            | DeploymentManifestState::Writing
            | DeploymentManifestState::Verifying => DeploymentState::Partial,
            DeploymentManifestState::Planned
            | DeploymentManifestState::RolledBack
            | DeploymentManifestState::Restored => DeploymentState::NotDeployed,
        }
    }

    pub fn version(&self, family: &str) -> Option<String> {
        self.manifests
            .get(family)
            .and_then(Option::as_ref)
            .map(|manifest| manifest.pack_version.clone())
    }

    pub fn files_present(&self, family: &str, target_directory: &Path) -> Option<bool> {
        let manifest = self.manifests.get(family).and_then(Option::as_ref)?;
        Some(
            manifest
                .files
                .iter()
                .all(|file| target_directory.join(&file.relative_path).is_file()),
        )
    }

    pub fn proxies(&self, target_directory: &Path) -> Vec<ProxyEntry> {
        let loaded = self
            .manifests
            .iter()
            .filter_map(|(family, manifest)| manifest.as_ref().map(|manifest| (*family, manifest)))
            .collect::<Vec<_>>();
        ProxyStatus::inspect_loaded(target_directory, &loaded)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::paths::AppPaths;
    use fgm_domain::deployment::{DeployedFileRecord, DeploymentManifest};
    use std::time::{SystemTime, UNIX_EPOCH};

    #[test]
    fn reports_missing_deployed_files() {
        let stamp = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .unwrap()
            .as_nanos();
        let paths = AppPaths::new(
            std::env::temp_dir().join(format!("fgm-snapshot-{stamp}")),
            false,
        )
        .unwrap();
        let target = paths.root().join("game");
        std::fs::create_dir_all(&target).unwrap();
        let manifest = DeploymentManifest {
            family_id: "smooth-motion".to_string(),
            target_directory: target.display().to_string(),
            files: vec![DeployedFileRecord {
                relative_path: "version.dll".to_string(),
                ..DeployedFileRecord::default()
            }],
            ..DeploymentManifest::default()
        };
        BackupStore::new(paths.backups())
            .save_manifest(&manifest)
            .unwrap();

        let snapshot = DeploymentSnapshot::new(&target, &paths.backups());
        assert_eq!(
            snapshot.files_present("smooth-motion", &target),
            Some(false)
        );

        std::fs::write(target.join("version.dll"), b"x").unwrap();
        let snapshot = DeploymentSnapshot::new(&target, &paths.backups());
        assert_eq!(snapshot.files_present("smooth-motion", &target), Some(true));
        let _ = std::fs::remove_dir_all(paths.root());
    }
}
