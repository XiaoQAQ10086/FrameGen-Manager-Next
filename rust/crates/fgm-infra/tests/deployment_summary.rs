use std::fs;
use std::path::PathBuf;
use std::time::{SystemTime, UNIX_EPOCH};

use fgm_domain::deployment::{DeploymentManifest, DeploymentManifestState, DeploymentState};
use fgm_infra::deployment::{BackupStore, DeploymentSummary};

#[test]
fn reports_manifest_state_without_hashing_files() {
    let root = unique_dir("summary");
    let target = root.join("game");
    fs::create_dir_all(&target).unwrap();
    let backups = root.join("backups");

    let none = DeploymentSummary::state("framegen.sm86", &target, &backups, Some("1.0")).unwrap();
    assert_eq!(none, DeploymentState::NotDeployed);

    let store = BackupStore::new(backups.clone());
    let mut manifest = DeploymentManifest {
        family_id: "framegen.sm86".to_string(),
        pack_version: "1.0".to_string(),
        target_directory: target.display().to_string(),
        state: DeploymentManifestState::Installed,
        ..DeploymentManifest::default()
    };
    store.save_manifest(&manifest).unwrap();

    let installed =
        DeploymentSummary::state("framegen.sm86", &target, &backups, Some("1.0")).unwrap();
    assert_eq!(installed, DeploymentState::Installed);

    let outdated =
        DeploymentSummary::state("framegen.sm86", &target, &backups, Some("2.0")).unwrap();
    assert_eq!(outdated, DeploymentState::Outdated);

    manifest.state = DeploymentManifestState::Partial;
    store.save_manifest(&manifest).unwrap();
    let partial = DeploymentSummary::state("framegen.sm86", &target, &backups, None).unwrap();
    assert_eq!(partial, DeploymentState::Partial);
}

fn unique_dir(name: &str) -> PathBuf {
    let nanos = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap()
        .as_nanos();
    let path = std::env::temp_dir().join(format!("fgm-{name}-{}-{nanos}", std::process::id()));
    fs::create_dir_all(&path).unwrap();
    path
}
