use std::fs;
use std::path::PathBuf;
use std::time::{SystemTime, UNIX_EPOCH};

use fgm_infra::diagnostics::DiagnosticsService;
use fgm_infra::paths::AppPaths;

#[test]
fn bundle_contains_the_log_tail_and_a_summary() {
    let root = unique_dir("diagnostics");
    let paths = AppPaths::new(root.join("data"), false).unwrap();
    paths.ensure_created().unwrap();
    fs::write(paths.logs().join("fgm.log"), b"{\"event\":\"deploy\"}\n").unwrap();

    let bytes = DiagnosticsService::build_bundle(&paths).unwrap();

    assert_eq!(&bytes[..4], &[0x50, 0x4b, 0x03, 0x04]);
    assert!(contains(&bytes, b"logs/fgm.log"));
    assert!(contains(&bytes, b"summary.json"));
    assert!(contains(&bytes, b"\"games\""));
    assert!(!contains(&bytes, root.to_string_lossy().as_bytes()));
}

fn contains(haystack: &[u8], needle: &[u8]) -> bool {
    haystack
        .windows(needle.len())
        .any(|window| window == needle)
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
