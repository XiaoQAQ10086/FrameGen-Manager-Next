use std::fs;
use std::path::PathBuf;
use std::time::{SystemTime, UNIX_EPOCH};

use fgm_infra::paths::AppPaths;
use fgm_infra::reframework::ReframeworkService;

#[test]
fn skips_games_without_an_re_engine_marker() {
    let root = unique_dir("reframework-skip");
    fs::create_dir_all(root.join("game")).unwrap();
    let executable = root.join("game").join("game.exe");
    fs::write(&executable, b"not-a-pe").unwrap();
    let paths = AppPaths::new(root.join("data"), false).unwrap();
    paths.ensure_created().unwrap();

    let installed =
        ReframeworkService::install_before_deploy(&executable, &root.join("game"), "0.1.0", &paths)
            .unwrap();

    assert!(!installed);
    assert!(!root.join("game").join("dinput8.dll").exists());
}

#[test]
fn uninstall_ignores_directories_without_our_manifest() {
    let root = unique_dir("reframework-uninstall");
    let game = root.join("game");
    fs::create_dir_all(&game).unwrap();
    fs::write(game.join("dinput8.dll"), b"third-party").unwrap();
    let paths = AppPaths::new(root.join("data"), false).unwrap();
    paths.ensure_created().unwrap();

    let removed = ReframeworkService::uninstall(&game, &paths).unwrap();

    assert!(!removed);
    assert_eq!(fs::read(game.join("dinput8.dll")).unwrap(), b"third-party");
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
