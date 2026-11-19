use std::fs;
use std::path::{Path, PathBuf};
use std::time::{SystemTime, UNIX_EPOCH};

use fgm_infra::fs::{AtomicFile, DeploymentKey, TargetLock};

#[test]
fn deployment_key_is_stable_across_case_and_separators() {
    let left = Path::new("C:\\Temp\\FrameGen\\Game");
    let right = Path::new("c:/temp/framegen/game/");

    let left_key = DeploymentKey::for_path(left).unwrap();
    let right_key = DeploymentKey::for_path(right).unwrap();

    assert_eq!(left_key, right_key);
    assert_eq!(left_key.len(), 16);
    assert!(left_key.bytes().all(|byte| byte.is_ascii_hexdigit()));
    assert_eq!(left_key, left_key.to_ascii_lowercase());
}

#[test]
fn deployment_key_differs_for_different_paths() {
    assert_ne!(
        DeploymentKey::for_path(Path::new("C:\\Games\\A")).unwrap(),
        DeploymentKey::for_path(Path::new("C:\\Games\\B")).unwrap()
    );
}

#[test]
fn atomic_write_creates_and_replaces() {
    let dir = TestDir::new("fs-atomic");
    let path = dir.path().join("nested").join("manifest.json");

    AtomicFile::write_text(&path, "first").unwrap();
    assert_eq!(fs::read_to_string(&path).unwrap(), "first");

    AtomicFile::write_text(&path, "second").unwrap();
    assert_eq!(fs::read_to_string(&path).unwrap(), "second");

    let leftovers = fs::read_dir(path.parent().unwrap())
        .unwrap()
        .filter_map(Result::ok)
        .filter(|entry| entry.file_name().to_string_lossy().ends_with(".tmp"))
        .count();
    assert_eq!(leftovers, 0);
}

#[test]
fn target_lock_is_exclusive_and_releases_on_drop() {
    let dir = TestDir::new("fs-lock");
    let lock_dir = dir.path().join("locks");
    let target = dir.path().join("game");
    fs::create_dir_all(&target).unwrap();

    let first = TargetLock::try_acquire(&lock_dir, "framegen.sm86", &target)
        .unwrap()
        .expect("first lock");
    assert!(
        TargetLock::try_acquire(&lock_dir, "framegen.sm86", &target)
            .unwrap()
            .is_none()
    );
    drop(first);

    assert!(
        TargetLock::try_acquire(&lock_dir, "framegen.sm86", &target)
            .unwrap()
            .is_some()
    );
}

#[test]
fn target_lock_rejects_unsafe_family_names() {
    let dir = TestDir::new("fs-lock-family");
    let lock_dir = dir.path().join("locks");
    let target = dir.path().join("game");
    fs::create_dir_all(&target).unwrap();

    let _lock = TargetLock::try_acquire(&lock_dir, "../escape", &target)
        .unwrap()
        .expect("lock with sanitized family");

    let entries: Vec<_> = fs::read_dir(&lock_dir)
        .unwrap()
        .filter_map(Result::ok)
        .map(|entry| entry.file_name().to_string_lossy().into_owned())
        .collect();
    assert_eq!(entries.len(), 1);
    assert!(entries[0].starts_with("unknown-"));
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
