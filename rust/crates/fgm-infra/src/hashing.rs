use std::fs::File;
use std::io::Read;
use std::path::Path;

use fgm_domain::error::{AppError, ErrorCode};
use sha2::{Digest, Sha256};

/// Streaming SHA-256 so large pack payloads never load into memory.
pub struct FileHasher;

impl FileHasher {
    pub fn sha256_file(path: &Path) -> Result<String, AppError> {
        let mut file = File::open(path).map_err(|error| io_error(path, error))?;
        let mut hasher = Sha256::new();
        let mut buffer = [0_u8; 64 * 1024];

        loop {
            let read = file
                .read(&mut buffer)
                .map_err(|error| io_error(path, error))?;
            if read == 0 {
                break;
            }
            hasher.update(&buffer[..read]);
        }

        let digest = hasher.finalize();
        let mut hex = String::with_capacity(digest.len() * 2);
        for byte in digest {
            use std::fmt::Write as _;
            write!(&mut hex, "{byte:02x}").expect("writing to a String cannot fail");
        }
        Ok(hex)
    }
}

fn io_error(path: &Path, error: std::io::Error) -> AppError {
    let code = match error.kind() {
        std::io::ErrorKind::NotFound => ErrorCode::FileNotFound,
        std::io::ErrorKind::PermissionDenied => ErrorCode::AccessDenied,
        _ => ErrorCode::Internal,
    };

    AppError::new(code)
        .with("path", path.display().to_string())
        .with("reason", error.to_string())
}
