use std::fs;
use std::path::PathBuf;
use std::time::{SystemTime, UNIX_EPOCH};

use fgm_domain::library::{GameEntry, GraphicsApi, LibrarySource};
use fgm_infra::library::{LibraryCache, LibraryService, LibraryStore};
use fgm_infra::paths::AppPaths;

#[test]
fn add_manual_keeps_scanned_games_and_puts_the_manual_entry_first() {
    let root = unique_dir("library-manual");
    fs::create_dir_all(root.join("game")).unwrap();
    let executable = root.join("game").join("demo.exe");
    fs::write(&executable, b"not-a-pe").unwrap();

    let paths = AppPaths::new(root.join("data"), false).unwrap();
    paths.ensure_created().unwrap();
    let store = LibraryStore::new(paths.library().join("library.json"));
    let scanned = GameEntry {
        id: "steam-1".to_string(),
        name: "Scanned".to_string(),
        source: LibrarySource::Steam,
        install_directory: root.join("steam"),
        render_exe: None,
        target_directory: root.join("steam"),
        graphics_api: GraphicsApi::Dx12,
        app_id: Some("42".to_string()),
        is_manual: false,
    };
    let mut cache = LibraryCache::default();
    cache.games.push(scanned.clone());
    store.save(&cache).unwrap();

    let entry = LibraryService::add_manual(&paths, &executable).unwrap();

    assert_eq!(entry.source, LibrarySource::Manual);
    assert!(entry.is_manual);
    assert_eq!(entry.install_directory, root.join("game"));
    assert_eq!(entry.target_directory, root.join("game"));
    assert_eq!(entry.render_exe, Some(executable));
    assert_eq!(entry.name, "demo");

    let games = LibraryService::list(&paths);
    assert_eq!(games.len(), 2);
    assert_eq!(games[0], entry);
    assert_eq!(games[1], scanned);
}

#[test]
fn add_manual_replaces_an_existing_entry_for_the_same_directory() {
    let root = unique_dir("library-manual-replace");
    fs::create_dir_all(root.join("game")).unwrap();
    let executable = root.join("game").join("demo.exe");
    fs::write(&executable, b"not-a-pe").unwrap();

    let paths = AppPaths::new(root.join("data"), false).unwrap();
    paths.ensure_created().unwrap();

    LibraryService::add_manual(&paths, &executable).unwrap();
    LibraryService::add_manual(&paths, &executable).unwrap();

    assert_eq!(LibraryService::list(&paths).len(), 1);
}

#[test]
fn add_manual_rejects_a_missing_file() {
    let root = unique_dir("library-manual-missing");
    let paths = AppPaths::new(root.join("data"), false).unwrap();
    paths.ensure_created().unwrap();

    let error = LibraryService::add_manual(&paths, &root.join("missing.exe")).unwrap_err();

    assert_eq!(error.code, fgm_domain::error::ErrorCode::FileNotFound);
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
