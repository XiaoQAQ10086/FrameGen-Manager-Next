use std::fs;
use std::path::{Path, PathBuf};
use std::time::{SystemTime, UNIX_EPOCH};

use fgm_infra::settings::{AppLanguage, AppSettings, SettingsStore};

#[test]
fn missing_file_returns_defaults() {
    let dir = TestDir::new("settings-missing");
    let store = SettingsStore::new(dir.path().join("app.json"));

    let settings = store.load().unwrap();

    assert_eq!(settings, AppSettings::default());
    assert_eq!(settings.language, AppLanguage::System);
}

#[test]
fn settings_round_trip() {
    let dir = TestDir::new("settings-roundtrip");
    let store = SettingsStore::new(dir.path().join("app.json"));
    let settings = AppSettings {
        schema_version: 3,
        language: AppLanguage::English,
        theme: "light".to_string(),
        auto_scan_on_startup: true,
        show_covers: false,
        auto_install_reframework: false,
    };

    store.save(&settings).unwrap();

    assert_eq!(store.load().unwrap(), settings);
}

#[test]
fn corrupt_settings_are_quarantined() {
    let dir = TestDir::new("settings-corrupt");
    let path = dir.path().join("app.json");
    fs::write(&path, b"{ not json").unwrap();
    let store = SettingsStore::new(&path);

    assert_eq!(store.load().unwrap(), AppSettings::default());
    assert!(!path.exists());
    assert!(path.with_extension("json.bad").exists());
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
