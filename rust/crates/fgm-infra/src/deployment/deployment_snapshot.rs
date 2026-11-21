use std::collections::BTreeMap;
use std::path::Path;

use fgm_domain::deployment::{DeploymentManifest, DeploymentManifestState, DeploymentState};

use super::backup_store::BackupStore;
use super::proxy_status::{ProxyEntry, ProxyStatus};

const FAMILIES: [&str; 4] = ["framegen.sm86", "dlss5", "anr", "reframework"];

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

    pub fn proxies(&self, target_directory: &Path) -> Vec<ProxyEntry> {
        let loaded = self
            .manifests
            .iter()
            .filter_map(|(family, manifest)| manifest.as_ref().map(|manifest| (*family, manifest)))
            .collect::<Vec<_>>();
        ProxyStatus::inspect_loaded(target_directory, &loaded)
    }
}
