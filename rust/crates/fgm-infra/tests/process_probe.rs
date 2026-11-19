use std::path::Path;

use fgm_infra::process::GameProcessProbe;

#[test]
fn finds_the_current_process_by_executable_path() {
    let current = std::env::current_exe().unwrap();

    assert!(GameProcessProbe::is_running(&current));
}

#[test]
fn returns_false_for_an_unknown_executable() {
    assert!(!GameProcessProbe::is_running(Path::new(
        "definitely-not-running-fgm-test.exe"
    )));
}

#[test]
fn returns_false_for_an_empty_path() {
    assert!(!GameProcessProbe::is_running(Path::new("")));
}
