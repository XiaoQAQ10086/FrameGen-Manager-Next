use std::path::Path;

use fgm_domain::error::{AppError, ErrorCode};
use fgm_domain::pack::AssetPack;

/// Strict `pack.json` reader. Unknown fields, comments and trailing commas are
/// rejected so typos cannot silently change deployment behavior.
pub struct PackJson;

impl PackJson {
    pub fn load(path: &Path) -> Result<AssetPack, AppError> {
        let json = std::fs::read_to_string(path).map_err(|error| {
            let code = match error.kind() {
                std::io::ErrorKind::NotFound => ErrorCode::PackNotFound,
                std::io::ErrorKind::PermissionDenied => ErrorCode::AccessDenied,
                _ => ErrorCode::Internal,
            };
            AppError::new(code)
                .with("path", path.display().to_string())
                .with("reason", error.to_string())
        })?;

        serde_json::from_str(&json).map_err(|error| {
            AppError::new(ErrorCode::PackInvalid)
                .with("path", path.display().to_string())
                .with("reason", error.to_string())
        })
    }
}
