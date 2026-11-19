use std::fs::{self, File};
use std::path::{Path, PathBuf};
use std::time::{SystemTime, UNIX_EPOCH};

use fgm_infra::library::RenderExeSelector;

#[test]
fn prefers_win64_shipping_in_binaries_win64() {
    let dir = TestDir::new("render-score");
    let exe = dir
        .path()
        .join("MyGame")
        .join("Binaries")
        .join("Win64")
        .join("MyGame-Win64-Shipping.exe");
    write_sparse(&exe, 3 * 1024 * 1024);
    write_sparse(
        &dir.path().join("MyGame").join("Launcher.exe"),
        4 * 1024 * 1024,
    );

    let selected = RenderExeSelector::select(&dir.path().join("MyGame")).unwrap();

    assert_eq!(selected.path, exe);
    assert!(selected.score >= 60);
}

#[test]
fn prefers_the_larger_file_on_a_score_tie() {
    let dir = TestDir::new("render-tie");
    let game = dir.path().join("Game");
    let small = game.join("Small.exe");
    let large = game.join("Large.exe");
    write_sparse(&small, 3 * 1024 * 1024);
    write_sparse(&large, 12 * 1024 * 1024);

    let selected = RenderExeSelector::select(&game).unwrap();

    assert_eq!(selected.path, large);
}

#[test]
fn skips_crash_handlers_and_launchers() {
    let dir = TestDir::new("render-exclude");
    let game = dir.path().join("Game");
    write_sparse(&game.join("UnityCrashHandler64.exe"), 5 * 1024 * 1024);
    write_sparse(&game.join("SomeLauncher.exe"), 5 * 1024 * 1024);

    assert!(RenderExeSelector::select(&game).is_none());
}

#[test]
fn ignores_files_deeper_than_five_levels() {
    let dir = TestDir::new("render-depth");
    let game = dir.path().join("Game");
    write_sparse(
        &game
            .join("a")
            .join("b")
            .join("c")
            .join("d")
            .join("e")
            .join("f")
            .join("game.exe"),
        3 * 1024 * 1024,
    );

    assert!(RenderExeSelector::select(&game).is_none());
}

#[test]
fn ignores_files_smaller_than_64_kib() {
    let dir = TestDir::new("render-small");
    let game = dir.path().join("Game");
    write_sparse(&game.join("game.exe"), 1024);

    assert!(RenderExeSelector::select(&game).is_none());
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
