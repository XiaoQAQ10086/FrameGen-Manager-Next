use std::fs::{File, OpenOptions};
use std::io::Write;
use std::path::{Path, PathBuf};
use std::sync::atomic::{AtomicU64, Ordering};

use fgm_domain::error::{AppError, ErrorCode};

static NEXT_TEMP: AtomicU64 = AtomicU64::new(0);

/// Writes a file through a sibling temp file and an atomic rename.
pub struct AtomicFile;

impl AtomicFile {
    pub fn write(path: &Path, contents: &[u8]) -> Result<(), AppError> {
        let parent = parent_directory(path)?;
        std::fs::create_dir_all(parent).map_err(|error| io_error(parent, error))?;

        let temp = temp_path(path);
        let result = write_temp(&temp, contents)
            .and_then(|()| std::fs::rename(&temp, path).map_err(|error| io_error(path, error)));

        if result.is_err() {
            let _ = std::fs::remove_file(&temp);
        }
        result
    }

    pub fn write_text(path: &Path, contents: &str) -> Result<(), AppError> {
        Self::write(path, contents.as_bytes())
    }

    /// Copies a file through a sibling temp file and an atomic rename.
    pub fn copy(source: &Path, destination: &Path) -> Result<(), AppError> {
        let mut input = File::open(source).map_err(|error| io_error(source, error))?;
        if !source.is_file() {
            return Err(AppError::new(ErrorCode::FileNotFound)
                .with("path", source.display().to_string())
                .with("reason", "source is not a file"));
        }

        let parent = parent_directory(destination)?;
        std::fs::create_dir_all(parent).map_err(|error| io_error(parent, error))?;

        let temp = temp_path(destination);
        let result = copy_temp(&mut input, &temp).and_then(|()| {
            std::fs::rename(&temp, destination).map_err(|error| io_error(destination, error))
        });

        if result.is_err() {
            let _ = std::fs::remove_file(&temp);
        }
        result
    }
}

fn parent_directory(path: &Path) -> Result<&Path, AppError> {
    path.parent().ok_or_else(|| {
        AppError::new(ErrorCode::InvalidPath)
            .with("path", path.display().to_string())
            .with("reason", "path has no parent directory")
    })
}

fn write_temp(path: &Path, contents: &[u8]) -> Result<(), AppError> {
    let mut file = OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(path)
        .map_err(|error| io_error(path, error))?;

    file.write_all(contents)
        .map_err(|error| io_error(path, error))?;
    file.sync_all().map_err(|error| io_error(path, error))
}

fn copy_temp(input: &mut File, path: &Path) -> Result<(), AppError> {
    let mut output = OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(path)
        .map_err(|error| io_error(path, error))?;

    std::io::copy(input, &mut output).map_err(|error| io_error(path, error))?;
    output.sync_all().map_err(|error| io_error(path, error))
}

fn temp_path(path: &Path) -> PathBuf {
    let file_name = path
        .file_name()
        .map_or_else(|| "file".into(), |name| name.to_string_lossy().into_owned());
    let counter = NEXT_TEMP.fetch_add(1, Ordering::Relaxed);

    path.with_file_name(format!(
        "{file_name}.{}.{}.tmp",
        std::process::id(),
        counter
    ))
}

fn io_error(path: &Path, error: std::io::Error) -> AppError {
    let code = match error.raw_os_error() {
        Some(32 | 33) => ErrorCode::FileLocked,
        _ => match error.kind() {
            std::io::ErrorKind::PermissionDenied => ErrorCode::AccessDenied,
            std::io::ErrorKind::NotFound => ErrorCode::FileNotFound,
            _ => ErrorCode::Internal,
        },
    };

    AppError::new(code)
        .with("path", path.display().to_string())
        .with("reason", error.to_string())
}
