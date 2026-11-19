use std::collections::BTreeMap;
use std::fs;
use std::path::{Path, PathBuf};
use std::time::{SystemTime, UNIX_EPOCH};

use fgm_domain::deployment::{DeployedFileRecord, DeploymentManifest, DeploymentManifestState};
use fgm_domain::error::ErrorCode;
use fgm_infra::deployment::BackupStore;

#[test]
fn backup_directory_is_stable_and_sanitized() {
    let dir = TestDir::new("backup-dir");
    let store = BackupStore::new(dir.path().join("backups"));
    let target = dir.path().join("game");

    let first = store.directory("framegen.sm86", &target).unwrap();
    let second = store.directory("framegen.sm86", &target).unwrap();

    assert_eq!(first, second);
    assert!(first.starts_with(dir.path().join("backups").join("framegen.sm86")));
    assert!(first.ends_with(store_key(&target)));

    let sanitized = store.directory("../escape", &target).unwrap();
    assert!(sanitized.starts_with(dir.path().join("backups").join("unknown")));
}

#[test]
fn saves_and_loads_a_manifest() {
    let dir = TestDir::new("backup-roundtrip");
    let store = BackupStore::new(dir.path().join("backups"));
    let manifest = sample_manifest(dir.path().join("game"));

    store.save_manifest(&manifest).unwrap();
    let loaded = store
        .load_manifest(&manifest.family_id, Path::new(&manifest.target_directory))
        .unwrap()
        .expect("manifest");

    assert_eq!(loaded, manifest);
}

#[test]
fn missing_manifest_returns_none() {
    let dir = TestDir::new("backup-missing");
    let store = BackupStore::new(dir.path().join("backups"));

    let loaded = store
        .load_manifest("framegen.sm86", &dir.path().join("game"))
        .unwrap();

    assert!(loaded.is_none());
}

#[test]
fn corrupt_manifest_is_quarantined() {
    let dir = TestDir::new("backup-corrupt");
    let store = BackupStore::new(dir.path().join("backups"));
    let target = dir.path().join("game");
    let manifest_path = store.manifest_path("framegen.sm86", &target).unwrap();
    fs::create_dir_all(manifest_path.parent().unwrap()).unwrap();
    fs::write(&manifest_path, b"{ not json").unwrap();

    let loaded = store.load_manifest("framegen.sm86", &target).unwrap();

    assert!(loaded.is_none());
    assert!(!manifest_path.exists());
    assert!(manifest_path.with_extension("json.bad").exists());
}

#[test]
fn rejects_manifest_without_an_absolute_target() {
    let dir = TestDir::new("backup-invalid");
    let store = BackupStore::new(dir.path().join("backups"));
    let mut manifest = sample_manifest(dir.path().join("game"));
    manifest.target_directory = "relative/game".to_string();

    assert!(store.save_manifest(&manifest).is_err());
}

#[test]
fn backup_file_path_stays_under_the_backup_directory() {
    let dir = TestDir::new("backup-path");
    let store = BackupStore::new(dir.path().join("backups"));
    let backup_directory = dir.path().join("backups").join("framegen.sm86").join("key");

    let path = store
        .backup_file_path(&backup_directory, "runtime/nvngx.dll")
        .unwrap();
    assert_eq!(path, backup_directory.join("runtime").join("nvngx.dll"));

    assert!(
        store
            .backup_file_path(&backup_directory, "../escape.dll")
            .is_err()
    );
}

#[test]
fn stores_and_restores_a_backup_file() {
    let dir = TestDir::new("backup-file");
    let store = BackupStore::new(dir.path().join("backups"));
    let backup_directory = dir.path().join("backups").join("framegen.sm86").join("key");
    let source = dir.path().join("game").join("version.dll");
    let target = dir.path().join("game").join("version.dll");
    fs::create_dir_all(source.parent().unwrap()).unwrap();
    fs::write(&source, b"original").unwrap();

    let backup = store
        .store_file(&backup_directory, "version.dll", &source)
        .unwrap();
    assert_eq!(fs::read(&backup).unwrap(), b"original");

    fs::write(&target, b"deployed").unwrap();
    store
        .restore_file(&backup_directory, "version.dll", &target)
        .unwrap();
    assert_eq!(fs::read(&target).unwrap(), b"original");
}

#[test]
fn restoring_a_missing_backup_is_an_error() {
    let dir = TestDir::new("backup-file-missing");
    let store = BackupStore::new(dir.path().join("backups"));
    let backup_directory = dir.path().join("backups").join("framegen.sm86").join("key");
    let target = dir.path().join("game").join("version.dll");
    fs::create_dir_all(target.parent().unwrap()).unwrap();

    let error = store
        .restore_file(&backup_directory, "version.dll", &target)
        .unwrap_err();

    assert_eq!(error.code, ErrorCode::FileNotFound);
}

fn sample_manifest(target: PathBuf) -> DeploymentManifest {
    DeploymentManifest {
        schema_version: 1,
        family_id: "framegen.sm86".to_string(),
        pack_id: "framegen.sm86".to_string(),
        pack_version: "0.4.2".to_string(),
        target_directory: target.display().to_string(),
        created_unix_ms: 1_700_000_000_000,
        state: DeploymentManifestState::Installed,
        files: vec![DeployedFileRecord {
            relative_path: "version.dll".to_string(),
            sha256: "a".repeat(64),
            size: 123,
            existed_before: false,
            backup_relative_path: None,
            role: "proxy".to_string(),
            last_write_time_utc_ticks: Some(42),
        }],
        options: BTreeMap::from([("api".to_string(), "dxgi".to_string())]),
    }
}

fn store_key(target: &Path) -> String {
    fgm_infra::fs::DeploymentKey::for_path(target).unwrap()
}

struct TestDir(PathBuf);

impl TestDir {
    fn new(name: &str) -> Self {
        let nanos = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .unwrap()
            .as_nanos();
        let path = std::env::temp_dir().join(format!("fgm-{name}-{}-{nanos}", std::process::id()));
        fs::create_dir_all(&path).unwrap();
        Self(path)
    }

    fn path(&self) -> &Path {
        &self.0
    }
}

impl Drop for TestDir {
    fn drop(&mut self) {
        let _ = fs::remove_dir_all(&self.0);
    }
}
