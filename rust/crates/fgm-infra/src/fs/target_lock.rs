use std::fs::{File, OpenOptions};
use std::io::Write;
use std::path::Path;
use std::time::{SystemTime, UNIX_EPOCH};

use fgm_domain::error::{AppError, ErrorCode};
use fgm_domain::safety::SafePath;

use super::deployment_key::DeploymentKey;

/// Cross-process deployment lock backed by an exclusively opened file.
///
/// The handle is held for the lifetime of this value; dropping it releases the
/// lock. `try_acquire` returns `Ok(None)` when another process already holds it.
pub struct TargetLock {
    _file: File,
}

impl TargetLock {
    pub fn try_acquire(
        lock_directory: &Path,
        family: &str,
        target_directory: &Path,
    ) -> Result<Option<Self>, AppError> {
        std::fs::create_dir_all(lock_directory).map_err(|error| io_error(lock_directory, error))?;

        let family = SafePath::normalize_relative(family)
            .map(|value| value.replace('/', "_"))
            .unwrap_or_else(|_| "unknown".to_string());
        let key = DeploymentKey::for_path(target_directory)?;
        let path = lock_directory.join(format!("{family}-{key}.lock"));

        let mut options = OpenOptions::new();
        options.create(true).read(true).write(true);

        #[cfg(windows)]
        {
            use std::os::windows::fs::OpenOptionsExt;
            options.share_mode(0);
        }

        let mut file = match options.open(&path) {
            Ok(file) => file,
            Err(_) => return Ok(None),
        };

        let _ = writeln!(
            file,
            "pid={}\nutc={}",
            std::process::id(),
            SystemTime::now()
                .duration_since(UNIX_EPOCH)
                .map_or(0, |value| value.as_secs())
        );
        let _ = file.flush();

        Ok(Some(Self { _file: file }))
    }
}

fn io_error(path: &Path, error: std::io::Error) -> AppError {
    let code = match error.kind() {
        std::io::ErrorKind::PermissionDenied => ErrorCode::AccessDenied,
        _ => ErrorCode::Internal,
    };
    AppError::new(code)
        .with("path", path.display().to_string())
        .with("reason", error.to_string())
}
