use std::fs::{self, File};
use std::path::{Path, PathBuf};
use std::time::{SystemTime, UNIX_EPOCH};

use fgm_domain::library::LibrarySource;
use fgm_infra::library::XboxGameScanner;

#[test]
fn finds_store_games_under_xbox_games() {
    let root = unique_dir("xbox-scan");
    let game = root.join("XboxGames").join("Forza");
    fs::create_dir_all(game.join("Content")).unwrap();
    write_sparse(&game.join("Content").join("Forza.exe"), 3 * 1024 * 1024);

    let games = XboxGameScanner::scan(&[root]);

    assert_eq!(games.len(), 1);
    assert_eq!(games[0].name, "Forza");
    assert_eq!(games[0].source, LibrarySource::Xbox);
    assert_eq!(
        games[0].render_exe,
        Some(game.join("Content").join("Forza.exe"))
    );
    assert_eq!(games[0].target_directory, game.join("Content"));
}

#[test]
fn ignores_folders_without_store_markers() {
    let root = unique_dir("xbox-skip");
    let game = root.join("XboxGames").join("Plain");
    fs::create_dir_all(&game).unwrap();
    write_sparse(&game.join("Plain.exe"), 3 * 1024 * 1024);

    assert!(XboxGameScanner::scan(&[root]).is_empty());
}

fn write_sparse(path: &Path, size: u64) {
    fs::create_dir_all(path.parent().unwrap()).unwrap();
    let file = File::create(path).unwrap();
    file.set_len(size).unwrap();
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
