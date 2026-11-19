use std::fs;
use std::path::{Path, PathBuf};
use std::time::{SystemTime, UNIX_EPOCH};

use fgm_infra::smooth_motion::{
    SmProfileRecord, SmProfileStore, SmProfileTarget, SmoothMotionService,
};

#[test]
fn profile_store_round_trips_and_deletes() {
    let dir = TestDir::new("sm-store");
    let store = SmProfileStore::new(dir.path().join("sm-profiles"));
    let target = dir.path().join("game");
    fs::create_dir_all(&target).unwrap();
    let record = SmProfileRecord::new(
        "Game",
        "C:\\Game\\game.exe",
        target.display().to_string(),
        vec![SmProfileTarget {
            profile: "Game".to_string(),
            ours: false,
            had: true,
            value: Some(1),
        }],
    );

    store.save(&record).unwrap();
    assert_eq!(store.load(&target).unwrap(), Some(record));

    store.delete(&target).unwrap();
    assert!(store.load(&target).unwrap().is_none());
}

#[test]
fn corrupt_profile_store_is_quarantined() {
    let dir = TestDir::new("sm-corrupt");
    let target = dir.path().join("game");
    fs::create_dir_all(&target).unwrap();
    let store = SmProfileStore::new(dir.path().join("sm-profiles"));
    let path = store.path_for(&target).unwrap();
    fs::create_dir_all(path.parent().unwrap()).unwrap();
    fs::write(&path, b"{ not json").unwrap();

    assert!(store.load(&target).unwrap().is_none());
    assert!(path.with_extension("json.bad").exists());
}

#[test]
fn inspect_does_not_panic_without_a_profile() {
    let exe = std::env::current_exe().unwrap();
    let state = SmoothMotionService::inspect(&exe);

    // Either NVAPI is unavailable or the test executable has no profile.
    assert!(state.enabled.is_none() || !state.profile_names.is_empty());
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
