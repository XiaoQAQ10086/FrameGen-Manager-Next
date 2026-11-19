use std::fs::{self, File};
use std::path::{Path, PathBuf};
use std::time::{SystemTime, UNIX_EPOCH};

use fgm_domain::library::LibrarySource;
use fgm_infra::library::EpicGameScanner;

#[test]
fn reads_application_manifests() {
    let dir = TestDir::new("epic-scan");
    let manifests = dir.path().join("manifests");
    let game = dir.path().join("EpicGames").join("MyGame");
    fs::create_dir_all(&manifests).unwrap();
    write_sparse(&game.join("MyGame.exe"), 3 * 1024 * 1024);
    write_item(&manifests.join("A.item"), &game, "MyGame", "app-1", true);

    let games = EpicGameScanner::scan(&manifests);

    assert_eq!(games.len(), 1);
    assert_eq!(games[0].name, "MyGame");
    assert_eq!(games[0].source, LibrarySource::Epic);
    assert_eq!(games[0].app_id.as_deref(), Some("app-1"));
}

#[test]
fn skips_non_applications_and_bad_json() {
    let dir = TestDir::new("epic-skip");
    let manifests = dir.path().join("manifests");
    let game = dir.path().join("EpicGames").join("Tool");
    fs::create_dir_all(&manifests).unwrap();
    write_sparse(&game.join("tool.exe"), 3 * 1024 * 1024);
    write_item(&manifests.join("Tool.item"), &game, "Tool", "tool", false);
    fs::write(manifests.join("Broken.item"), b"{ not json").unwrap();

    assert!(EpicGameScanner::scan(&manifests).is_empty());
}

fn write_item(path: &Path, install: &Path, name: &str, app: &str, is_app: bool) {
    let json = format!(
        r#"{{
  "DisplayName": "{name}",
  "InstallLocation": "{}",
  "AppName": "{app}",
  "bIsApplication": {is_app}
}}"#,
        install.display().to_string().replace('\\', "\\\\")
    );
    fs::write(path, json).unwrap();
}

fn write_sparse(path: &Path, size: u64) {
    fs::create_dir_all(path.parent().unwrap()).unwrap();
    let file = File::create(path).unwrap();
    file.set_len(size).unwrap();
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
