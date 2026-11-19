use std::fs::{self, File};
use std::path::PathBuf;
use std::process::{Command, Output};
use std::time::{SystemTime, UNIX_EPOCH};

#[test]
fn scans_and_lists_a_loose_library() {
    let dir = unique_dir("library-cli");
    let root = dir.join("root");
    let data = dir.join("data");
    let steam = dir.join("steam");
    let epic = dir.join("epic");
    fs::create_dir_all(&steam).unwrap();
    fs::create_dir_all(&epic).unwrap();
    let game = root.join("Games").join("MyGame");
    fs::create_dir_all(&game).unwrap();
    let file = File::create(game.join("MyGame.exe")).unwrap();
    file.set_len(3 * 1024 * 1024).unwrap();

    let scan = run(&[
        "library",
        "scan",
        "--root",
        root.to_str().unwrap(),
        "--steam-dir",
        steam.to_str().unwrap(),
        "--epic-manifests",
        epic.to_str().unwrap(),
        "--data-dir",
        data.to_str().unwrap(),
    ]);
    assert!(scan.status.success(), "{}", stderr(&scan));
    assert!(stdout(&scan).contains("OK 1 games"));
    assert!(data.join("library").join("library.json").is_file());

    let list = run(&["library", "list", "--data-dir", data.to_str().unwrap()]);
    assert!(list.status.success(), "{}", stderr(&list));
    assert!(stdout(&list).contains("MyGame"));

    let _ = fs::remove_dir_all(&dir);
}

fn run(args: &[&str]) -> Output {
    Command::new(env!("CARGO_BIN_EXE_fgm-next"))
        .args(args)
        .output()
        .unwrap()
}

fn stdout(output: &Output) -> String {
    String::from_utf8_lossy(&output.stdout).into_owned()
}

fn stderr(output: &Output) -> String {
    String::from_utf8_lossy(&output.stderr).into_owned()
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
