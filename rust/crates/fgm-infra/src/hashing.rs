use std::fmt::Write as _;
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
        Ok(hex(digest))
    }
}

pub(crate) fn sha256_hex(bytes: &[u8]) -> String {
    hex(Sha256::digest(bytes))
}

fn hex(digest: impl IntoIterator<Item = u8>) -> String {
    let mut value = String::new();
    for byte in digest {
        write!(&mut value, "{byte:02x}").expect("writing to a String cannot fail");
    }
    value
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
