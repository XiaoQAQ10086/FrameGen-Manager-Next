use std::collections::BTreeMap;
use std::fs;
use std::path::{Path, PathBuf};
use std::sync::atomic::AtomicBool;
use std::time::{SystemTime, UNIX_EPOCH};

use fgm_domain::error::ErrorCode;
use fgm_infra::deployment::{DeploymentEngine, DeploymentFile};
use fgm_infra::fs::TargetLock;
use fgm_infra::hashing::FileHasher;

#[test]
fn deploys_and_restores_with_a_backup() {
    let fixture = Fixture::new("engine-roundtrip");

    let report = fixture.apply(&fixture.files()).unwrap();
    assert_eq!(report.files_written, 2);
    assert_eq!(report.files_backed_up, 1);
    assert_eq!(
        fs::read_to_string(fixture.target.join("version.dll")).unwrap(),
        "new-proxy"
    );
    assert_eq!(
        fs::read_to_string(fixture.target.join("runtime.dll")).unwrap(),
        "new-runtime"
    );

    let restore = fixture.restore().unwrap();
    assert_eq!(restore.restored_files, 1);
    assert_eq!(restore.removed_files, 1);
    assert!(restore.conflicts.is_empty());
    assert_eq!(
        fs::read_to_string(fixture.target.join("version.dll")).unwrap(),
        "original"
    );
    assert!(!fixture.target.join("runtime.dll").exists());
}

#[test]
fn restore_preserves_a_user_modified_file() {
    let fixture = Fixture::new("engine-conflict");
    fixture.apply(&fixture.files()).unwrap();
    fs::write(fixture.target.join("runtime.dll"), b"user-modified").unwrap();

    let restore = fixture.restore().unwrap();

    assert_eq!(restore.conflicts, vec!["runtime.dll"]);
    assert_eq!(
        fs::read_to_string(fixture.target.join("runtime.dll")).unwrap(),
        "user-modified"
    );
    let manifest = fixture
        .backups
        .join("framegen.sm86")
        .join(fgm_infra::fs::DeploymentKey::for_path(&fixture.target).unwrap())
        .join("manifest.json");
    assert!(manifest.exists());
}

#[test]
fn source_hash_mismatch_fails_before_writing() {
    let fixture = Fixture::new("engine-hash");
    let mut files = fixture.files();
    files[0].sha256 = "a".repeat(64);

    let error = fixture.apply(&files).unwrap_err();

    assert_eq!(error.code, ErrorCode::PackHashMismatch);
    assert_eq!(
        fs::read_to_string(fixture.target.join("version.dll")).unwrap(),
        "original"
    );
}

#[test]
fn cancellation_prevents_writes() {
    let fixture = Fixture::new("engine-cancel");
    let cancelled = AtomicBool::new(true);

    let error = fixture
        .apply_with_cancel(&fixture.files(), &cancelled)
        .unwrap_err();

    assert_eq!(error.code, ErrorCode::OperationCancelled);
    assert_eq!(
        fs::read_to_string(fixture.target.join("version.dll")).unwrap(),
        "original"
    );
}

#[test]
fn target_lock_contention_is_reported() {
    let fixture = Fixture::new("engine-lock");
    let _lock = TargetLock::try_acquire(&fixture.locks, "framegen.sm86", &fixture.target)
        .unwrap()
        .expect("lock");

    let error = fixture.apply(&fixture.files()).unwrap_err();

    assert_eq!(error.code, ErrorCode::FileLocked);
}

#[test]
fn redeploy_reuses_the_original_backup() {
    let fixture = Fixture::new("engine-redeploy");
    fixture.apply(&fixture.files()).unwrap();

    let report = fixture.apply(&fixture.files()).unwrap();

    assert_eq!(report.files_written, 2);
    assert_eq!(report.files_backed_up, 0);
    assert_eq!(
        fs::read_to_string(fixture.target.join("version.dll")).unwrap(),
        "new-proxy"
    );
}

#[test]
fn restore_recreates_a_missing_target_from_backup() {
    let fixture = Fixture::new("engine-restore-missing");
    fixture.apply(&fixture.files()).unwrap();
    fs::remove_file(fixture.target.join("version.dll")).unwrap();

    let restore = fixture.restore().unwrap();

    assert_eq!(restore.restored_files, 1);
    assert_eq!(
        fs::read_to_string(fixture.target.join("version.dll")).unwrap(),
        "original"
    );
}

#[test]
fn removes_owned_directories_on_restore() {
    let fixture = Fixture::new("engine-owned");
    fixture.apply(&fixture.files()).unwrap();
    fs::create_dir_all(fixture.target.join("owned")).unwrap();
    fs::write(fixture.target.join("owned").join("cache.txt"), b"cache").unwrap();

    fixture.restore().unwrap();

    assert!(!fixture.target.join("owned").exists());
}

#[test]
fn keeps_owned_directories_when_restore_has_conflicts() {
    let fixture = Fixture::new("engine-owned-conflict");
    fixture.apply(&fixture.files()).unwrap();
    fs::create_dir_all(fixture.target.join("owned")).unwrap();
    fs::write(fixture.target.join("runtime.dll"), b"user-modified").unwrap();

    fixture.restore().unwrap();

    assert!(fixture.target.join("owned").exists());
}

#[test]
fn refuses_to_deploy_while_the_game_is_running() {
    let fixture = Fixture::new("engine-running");
    let current = std::env::current_exe().unwrap();

    let error = DeploymentEngine::apply(
        &fixture.files(),
        &fixture.target,
        "framegen.sm86",
        "framegen.sm86",
        "0.4.2",
        &BTreeMap::new(),
        &[],
        Some(&current),
        &fixture.backups,
        &fixture.locks,
        &AtomicBool::new(false),
    )
    .unwrap_err();

    assert_eq!(error.code, ErrorCode::GameRunning);
    assert_eq!(
        fs::read_to_string(fixture.target.join("version.dll")).unwrap(),
        "original"
    );
}

#[test]
fn duplicate_targets_are_rejected() {
    let fixture = Fixture::new("engine-duplicate");
    let mut files = fixture.files();
    files.push(files[0].clone());

    let error = fixture.apply(&files).unwrap_err();

    assert_eq!(error.code, ErrorCode::Conflict);
}

struct Fixture {
    root: PathBuf,
    source: PathBuf,
    target: PathBuf,
    backups: PathBuf,
    locks: PathBuf,
}

impl Fixture {
    fn new(name: &str) -> Self {
        let root = unique_dir(name);
        let source = root.join("pack");
        let target = root.join("game");
        let backups = root.join("backups");
        let locks = root.join("locks");
        fs::create_dir_all(&source).unwrap();
        fs::create_dir_all(&target).unwrap();
        fs::write(source.join("version.dll"), b"new-proxy").unwrap();
        fs::write(source.join("runtime.dll"), b"new-runtime").unwrap();
        fs::write(target.join("version.dll"), b"original").unwrap();
        Self {
            root,
            source,
            target,
            backups,
            locks,
        }
    }

    fn files(&self) -> Vec<DeploymentFile> {
        vec![
            deployment_file(&self.source.join("version.dll"), "version.dll", "proxy"),
            deployment_file(&self.source.join("runtime.dll"), "runtime.dll", "runtime"),
        ]
    }

    fn apply(
        &self,
        files: &[DeploymentFile],
    ) -> Result<fgm_infra::deployment::DeploymentReport, fgm_domain::error::AppError> {
        self.apply_with_cancel(files, &AtomicBool::new(false))
    }

    fn apply_with_cancel(
        &self,
        files: &[DeploymentFile],
        cancelled: &AtomicBool,
    ) -> Result<fgm_infra::deployment::DeploymentReport, fgm_domain::error::AppError> {
        DeploymentEngine::apply(
            files,
            &self.target,
            "framegen.sm86",
            "framegen.sm86",
            "0.4.2",
            &BTreeMap::new(),
            &["owned".to_string()],
            None,
            &self.backups,
            &self.locks,
            cancelled,
        )
    }

    fn restore(&self) -> Result<fgm_infra::deployment::RestoreReport, fgm_domain::error::AppError> {
        DeploymentEngine::restore("framegen.sm86", &self.target, &self.backups, &self.locks)
    }
}

impl Drop for Fixture {
    fn drop(&mut self) {
        let _ = fs::remove_dir_all(&self.root);
    }
}

fn deployment_file(source: &Path, relative: &str, role: &str) -> DeploymentFile {
    let sha256 = FileHasher::sha256_file(source).unwrap();
    let size = fs::metadata(source).unwrap().len() as i64;
    DeploymentFile {
        relative_path: relative.to_string(),
        source: Some(source.to_path_buf()),
        content: None,
        sha256,
        size,
        role: role.to_string(),
    }
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
