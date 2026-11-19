use std::path::Path;

use fgm_domain::error::{AppError, ErrorCode};

use crate::hashing::sha256_hex;

/// Stable short key for a deployment target directory.
pub struct DeploymentKey;

impl DeploymentKey {
    pub fn for_path(path: &Path) -> Result<String, AppError> {
        let absolute = std::path::absolute(path).map_err(|error| {
            AppError::new(ErrorCode::InvalidPath)
                .with("path", path.display().to_string())
                .with("reason", error.to_string())
        })?;

        let mut normalized = absolute.to_string_lossy().replace('/', "\\").to_lowercase();
        while normalized.len() > 3 && normalized.ends_with('\\') {
            normalized.pop();
        }

        Ok(sha256_hex(normalized.as_bytes())[..16].to_string())
    }
}
