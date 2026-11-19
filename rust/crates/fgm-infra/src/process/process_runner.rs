use std::io::Read;
use std::path::Path;
use std::process::{Command, Stdio};
use std::sync::atomic::{AtomicBool, Ordering};
use std::thread;
use std::time::{Duration, Instant};

use fgm_domain::error::{AppError, ErrorCode};

use super::process_result::ProcessResult;

const CREATE_NO_WINDOW: u32 = 0x0800_0000;
const POLL_INTERVAL: Duration = Duration::from_millis(25);

/// Runs a child process with captured output, timeout and cooperative cancel.
pub struct ProcessRunner;

impl ProcessRunner {
    pub fn run(
        executable: &Path,
        arguments: &[String],
        working_directory: Option<&Path>,
        timeout: Duration,
        cancelled: &AtomicBool,
    ) -> Result<ProcessResult, AppError> {
        if cancelled.load(Ordering::Relaxed) {
            return Ok(cancelled_result());
        }

        let mut command = Command::new(executable);
        command
            .args(arguments)
            .stdin(Stdio::null())
            .stdout(Stdio::piped())
            .stderr(Stdio::piped());

        if let Some(directory) = working_directory {
            command.current_dir(directory);
        }

        #[cfg(windows)]
        {
            use std::os::windows::process::CommandExt;
            command.creation_flags(CREATE_NO_WINDOW);
        }

        let mut child = command
            .spawn()
            .map_err(|error| spawn_error(executable, error))?;

        let stdout = child.stdout.take().map(|mut stream| {
            thread::spawn(move || {
                let mut bytes = Vec::new();
                let _ = stream.read_to_end(&mut bytes);
                String::from_utf8_lossy(&bytes).into_owned()
            })
        });
        let stderr = child.stderr.take().map(|mut stream| {
            thread::spawn(move || {
                let mut bytes = Vec::new();
                let _ = stream.read_to_end(&mut bytes);
                String::from_utf8_lossy(&bytes).into_owned()
            })
        });

        let deadline = Instant::now() + timeout;
        let mut timed_out = false;
        let exit_code;

        loop {
            match child.try_wait() {
                Ok(Some(status)) => {
                    exit_code = status.code().unwrap_or(-1);
                    break;
                }
                Ok(None) => {
                    if cancelled.load(Ordering::Relaxed) || Instant::now() >= deadline {
                        timed_out = true;
                        exit_code = -1;
                        break;
                    }
                    thread::sleep(POLL_INTERVAL);
                }
                Err(error) => {
                    let _ = child.kill();
                    let _ = child.wait();
                    return Err(
                        AppError::new(ErrorCode::Internal).with("reason", error.to_string())
                    );
                }
            }
        }

        if timed_out {
            let _ = child.kill();
            let _ = child.wait();
        }

        let standard_output = stdout
            .and_then(|handle| handle.join().ok())
            .unwrap_or_default();
        let standard_error = stderr
            .and_then(|handle| handle.join().ok())
            .unwrap_or_default();

        Ok(ProcessResult {
            exit_code,
            standard_output,
            standard_error,
            timed_out,
        })
    }
}

fn cancelled_result() -> ProcessResult {
    ProcessResult {
        exit_code: -1,
        standard_output: String::new(),
        standard_error: String::new(),
        timed_out: true,
    }
}

fn spawn_error(executable: &Path, error: std::io::Error) -> AppError {
    let code = match error.kind() {
        std::io::ErrorKind::NotFound => ErrorCode::FileNotFound,
        std::io::ErrorKind::PermissionDenied => ErrorCode::AccessDenied,
        std::io::ErrorKind::InvalidInput => ErrorCode::InvalidArgument,
        _ => ErrorCode::Internal,
    };
    AppError::new(code)
        .with("path", executable.display().to_string())
        .with("reason", error.to_string())
}
