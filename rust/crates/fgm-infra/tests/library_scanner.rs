use std::fs::{self, File};
use std::path::{Path, PathBuf};
use std::time::{SystemTime, UNIX_EPOCH};

use fgm_domain::library::LibrarySource;
use fgm_infra::library::LibraryScanner;

#[test]
fn aggregates_and_deduplicates_scanners() {
    let dir = TestDir::new("library-aggregate");
    let steam = dir.path().join("Steam");
    write_steam_game(&steam, "1", "SteamGame", "SteamGame");
    let epic_manifests = dir.path().join("EpicManifests");
    fs::create_dir_all(&epic_manifests).unwrap();
    let epic_install = dir.path().join("Games").join("EpicGame");
    write_game(&epic_install, "EpicGame.exe");
    write_epic_item(&epic_manifests.join("Epic.item"), &epic_install, "EpicGame");
    let loose_only = dir.path().join("Games").join("LooseOnly");
    write_game(&loose_only, "LooseOnly.exe");

    let games = LibraryScanner::scan_with(
        Some(&steam),
        Some(&epic_manifests),
        &[dir.path().to_path_buf()],
    );

    assert_eq!(games.len(), 3);
    assert_eq!(games[0].source, LibrarySource::Steam);
    assert_eq!(games[1].source, LibrarySource::Epic);
    assert_eq!(games[2].source, LibrarySource::Loose);
    assert_eq!(games[1].name, "EpicGame");
    assert_eq!(games[2].name, "LooseOnly");
}

fn write_steam_game(root: &Path, app_id: &str, name: &str, install_dir: &str) {
    let steamapps = root.join("steamapps");
    fs::create_dir_all(&steamapps).unwrap();
    fs::write(
        steamapps.join(format!("appmanifest_{app_id}.acf")),
        format!(
            "\"AppState\"\n{{\n  \"appid\" \"{app_id}\"\n  \"name\" \"{name}\"\n  \"installdir\" \"{install_dir}\"\n}}\n"
        ),
    )
    .unwrap();
    write_game(
        &steamapps.join("common").join(install_dir),
        &format!("{install_dir}.exe"),
    );
}

fn write_epic_item(path: &Path, install: &Path, name: &str) {
    let json = format!(
        r#"{{"DisplayName":"{name}","InstallLocation":"{}","AppName":"{name}","bIsApplication":true}}"#,
        install.display().to_string().replace('\\', "\\\\")
    );
    fs::write(path, json).unwrap();
}

fn write_game(directory: &Path, exe_name: &str) {
    fs::create_dir_all(directory).unwrap();
    let file = File::create(directory.join(exe_name)).unwrap();
    file.set_len(3 * 1024 * 1024).unwrap();
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
