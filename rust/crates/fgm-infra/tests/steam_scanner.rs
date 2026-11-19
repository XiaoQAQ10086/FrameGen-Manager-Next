use std::fs::{self, File};
use std::path::{Path, PathBuf};
use std::time::{SystemTime, UNIX_EPOCH};

use fgm_infra::library::SteamGameScanner;

#[test]
fn scans_steam_libraries_and_manifests() {
    let dir = TestDir::new("steam-scan");
    let root = dir.path().join("Steam");
    let second = dir.path().join("SteamLibrary");
    write_game(&root, "100", "Game One", "GameOne");
    write_game(&second, "200", "Game Two", "GameTwo");
    write_junk(&root, "999", "SteamVR", "SteamVR");

    fs::create_dir_all(root.join("steamapps")).unwrap();
    fs::write(
        root.join("steamapps").join("libraryfolders.vdf"),
        format!(
            "\"libraryfolders\"\n{{\n  \"0\"\n  {{\n    \"path\" \"{}\"\n  }}\n  \"1\"\n  {{\n    \"path\" \"{}\"\n  }}\n}}\n",
            escape(&root),
            escape(&second)
        ),
    )
    .unwrap();

    let games = SteamGameScanner::scan(&root);

    assert_eq!(games.len(), 2);
    let ids: Vec<&str> = games.iter().map(|game| game.id.as_str()).collect();
    assert!(ids.contains(&"steam:100"));
    assert!(ids.contains(&"steam:200"));
}

#[test]
fn skips_steam_junk_apps() {
    let dir = TestDir::new("steam-junk");
    let root = dir.path().join("Steam");
    write_junk(&root, "10", "SteamVR", "SteamVR");
    write_game(&root, "11", "Game Three", "GameThree");

    let games = SteamGameScanner::scan(&root);

    assert_eq!(games.len(), 1);
    assert_eq!(games[0].id, "steam:11");
}

fn write_game(root: &Path, app_id: &str, name: &str, install_dir: &str) {
    let steamapps = root.join("steamapps");
    fs::create_dir_all(&steamapps).unwrap();
    fs::write(
        steamapps.join(format!("appmanifest_{app_id}.acf")),
        format!(
            "\"AppState\"\n{{\n  \"appid\" \"{app_id}\"\n  \"name\" \"{name}\"\n  \"installdir\" \"{install_dir}\"\n}}\n"
        ),
    )
    .unwrap();
    let game_dir = steamapps.join("common").join(install_dir);
    fs::create_dir_all(&game_dir).unwrap();
    sparse(
        &game_dir.join(format!("{install_dir}.exe")),
        3 * 1024 * 1024,
    );
}

fn write_junk(root: &Path, app_id: &str, name: &str, install_dir: &str) {
    let steamapps = root.join("steamapps");
    fs::create_dir_all(&steamapps).unwrap();
    fs::write(
        steamapps.join(format!("appmanifest_{app_id}.acf")),
        format!(
            "\"AppState\"\n{{\n  \"appid\" \"{app_id}\"\n  \"name\" \"{name}\"\n  \"installdir\" \"{install_dir}\"\n}}\n"
        ),
    )
    .unwrap();
    let game_dir = steamapps.join("common").join(install_dir);
    fs::create_dir_all(&game_dir).unwrap();
    sparse(&game_dir.join("thing.exe"), 3 * 1024 * 1024);
}

fn escape(path: &Path) -> String {
    path.display().to_string().replace('\\', "\\\\")
}

fn sparse(path: &Path, size: u64) {
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
