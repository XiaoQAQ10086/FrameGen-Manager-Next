use std::path::Path;
use std::sync::atomic::AtomicBool;
use std::time::Duration;

use fgm_domain::error::ErrorCode;
use fgm_infra::process::ProcessRunner;

fn run_cmd(script: &str, timeout: Duration) -> fgm_infra::process::ProcessResult {
    ProcessRunner::run(
        Path::new("cmd.exe"),
        &["/C".to_string(), script.to_string()],
        None,
        timeout,
        &AtomicBool::new(false),
    )
    .unwrap()
}

#[test]
fn captures_standard_output_and_exit_code() {
    let result = run_cmd("echo hello", Duration::from_secs(5));

    assert!(result.standard_output.contains("hello"));
    assert_eq!(result.exit_code, 0);
    assert!(!result.timed_out);
}

#[test]
fn captures_standard_error() {
    let result = run_cmd("echo oops 1>&2", Duration::from_secs(5));

    assert!(result.standard_error.contains("oops"));
}

#[test]
fn reports_nonzero_exit_codes() {
    let result = run_cmd("exit 7", Duration::from_secs(5));

    assert_eq!(result.exit_code, 7);
    assert!(!result.timed_out);
}

#[test]
fn times_out_and_kills_the_process() {
    let result = ProcessRunner::run(
        Path::new("powershell.exe"),
        &[
            "-NoProfile".to_string(),
            "-Command".to_string(),
            "Start-Sleep -Seconds 5".to_string(),
        ],
        None,
        Duration::from_millis(200),
        &AtomicBool::new(false),
    )
    .unwrap();

    assert!(result.timed_out);
    assert_eq!(result.exit_code, -1);
}

#[test]
fn returns_immediately_when_already_cancelled() {
    let result = ProcessRunner::run(
        Path::new("cmd.exe"),
        &["/C".to_string(), "echo never".to_string()],
        None,
        Duration::from_secs(5),
        &AtomicBool::new(true),
    )
    .unwrap();

    assert!(result.timed_out);
    assert!(result.standard_output.is_empty());
}

#[test]
fn missing_executable_is_an_error() {
    let error = ProcessRunner::run(
        Path::new("definitely-not-running-fgm-test.exe"),
        &[],
        None,
        Duration::from_secs(1),
        &AtomicBool::new(false),
    )
    .unwrap_err();

    assert_eq!(error.code, ErrorCode::FileNotFound);
}
