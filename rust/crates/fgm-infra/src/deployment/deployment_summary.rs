use std::path::Path;

use fgm_domain::deployment::{DeploymentManifestState, DeploymentState};
use fgm_domain::error::AppError;

use super::backup_store::BackupStore;

/// Manifest-only deployment state used for library badges, where hashing every
/// deployed file would be too expensive.
pub struct DeploymentSummary;

impl DeploymentSummary {
    pub fn state(
        family: &str,
        target_directory: &Path,
        backups_root: &Path,
        current_pack_version: Option<&str>,
    ) -> Result<DeploymentState, AppError> {
        let store = BackupStore::new(backups_root.to_path_buf());
        let Some(manifest) = store.load_manifest(family, target_directory)? else {
            return Ok(DeploymentState::NotDeployed);
        };
        Ok(match manifest.state {
            DeploymentManifestState::Installed => {
                if current_pack_version.is_some_and(|version| version != manifest.pack_version) {
                    DeploymentState::Outdated
                } else {
                    DeploymentState::Installed
                }
            }
            DeploymentManifestState::Partial
            | DeploymentManifestState::BackingUp
            | DeploymentManifestState::Writing
            | DeploymentManifestState::Verifying => DeploymentState::Partial,
            DeploymentManifestState::Planned
            | DeploymentManifestState::RolledBack
            | DeploymentManifestState::Restored => DeploymentState::NotDeployed,
        })
    }
}
