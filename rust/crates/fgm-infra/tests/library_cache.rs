use std::fs::{self, File};
use std::path::{Path, PathBuf};
use std::time::{SystemTime, UNIX_EPOCH};

use fgm_domain::library::LibrarySource;
use fgm_infra::library::{LibraryCache, LibraryStore, LooseGameScanner};

#[test]
fn scans_known_folders_for_games() {
    let dir = TestDir::new("library-scan");
    let game_dir = dir.path().join("Games").join("MyGame");
    write_sparse(&game_dir.join("MyGame.exe"), 3 * 1024 * 1024);

    let games = LooseGameScanner::scan(&[dir.path().to_path_buf()]);

    assert_eq!(games.len(), 1);
    assert_eq!(games[0].name, "MyGame");
    assert_eq!(games[0].source, LibrarySource::Loose);
    assert_eq!(games[0].render_exe, Some(game_dir.join("MyGame.exe")));
    assert_eq!(games[0].target_directory, game_dir);
}

#[test]
fn skips_empty_and_too_deep_folders() {
    let dir = TestDir::new("library-skip");
    fs::create_dir_all(dir.path().join("Games").join("Empty")).unwrap();
    write_sparse(
        &dir.path()
            .join("Games")
            .join("TooDeep")
            .join("a")
            .join("b")
            .join("c")
            .join("d")
            .join("game.exe"),
        3 * 1024 * 1024,
    );

    let games = LooseGameScanner::scan(&[dir.path().to_path_buf()]);

    assert!(games.is_empty());
}

#[test]
fn library_cache_round_trips() {
    let dir = TestDir::new("library-cache");
    let path = dir.path().join("library.json");
    let store = LibraryStore::new(&path);
    let mut cache = LibraryCache::default();
    cache.games.push(fgm_domain::library::GameEntry {
        id: "loose:abc".to_string(),
        name: "Game".to_string(),
        source: LibrarySource::Loose,
        install_directory: PathBuf::from("C:\\Games\\Game"),
        render_exe: Some(PathBuf::from("C:\\Games\\Game\\game.exe")),
        target_directory: PathBuf::from("C:\\Games\\Game"),
        graphics_api: fgm_domain::library::GraphicsApi::Dx12,
        app_id: None,
        is_manual: false,
    });

    store.save(&cache).unwrap();
    let loaded = store.load().unwrap();

    assert_eq!(loaded, cache);
}

#[test]
fn corrupt_library_cache_is_quarantined() {
    let dir = TestDir::new("library-corrupt");
    let path = dir.path().join("library.json");
    fs::write(&path, b"{ not json").unwrap();
    let store = LibraryStore::new(&path);

    let loaded = store.load().unwrap();

    assert_eq!(loaded, LibraryCache::default());
    assert!(!path.exists());
    assert!(path.with_extension("json.bad").exists());
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
