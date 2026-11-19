use std::path::{Path, PathBuf};

use fgm_domain::deployment::DeployedFileRecord;

use super::backup_store::BackupStore;
use super::deployment_file::DeploymentFile;
use super::deployment_support::prune_empty_directories;

pub(crate) fn rollback(
    planned: &[(DeploymentFile, PathBuf)],
    records: &[DeployedFileRecord],
    store: &BackupStore,
    backup_directory: &Path,
    target_directory: &Path,
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

    let directories: std::collections::BTreeSet<PathBuf> = planned
        .iter()
        .filter_map(|(_, target)| target.parent().map(Path::to_path_buf))
        .collect();
    prune_empty_directories(target_directory, &directories);

    complete
}
