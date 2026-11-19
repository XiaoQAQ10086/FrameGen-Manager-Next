use std::path::Path;

use fgm_domain::error::{AppError, ErrorCode};

use crate::process::ProcessLauncher;

/// Starts a verified installer from the update staging directory.
pub struct UpdateLauncher;

impl UpdateLauncher {
    pub fn install(downloaded: &Path) -> Result<(), AppError> {
        if !downloaded.is_file() {
            return Err(AppError::new(ErrorCode::FileNotFound)
                .with("path", downloaded.display().to_string())
                .with("reason", "the downloaded installer does not exist"));
        }
        let staging = std::env::temp_dir()
            .join("FrameGen-Manager-Next")
            .join("updates");
        if !downloaded.starts_with(&staging) {
            return Err(AppError::new(ErrorCode::InvalidPath)
                .with("path", downloaded.display().to_string())
                .with(
                    "reason",
                    "the installer must live in the update staging directory",
                ));
        }
        let name = downloaded
            .file_name()
            .map_or_else(String::new, |name| name.to_string_lossy().to_lowercase());
        if !name.ends_with(".exe") || !name.contains("setup") {
            return Err(AppError::new(ErrorCode::InvalidArgument)
                .with("path", downloaded.display().to_string())
                .with("reason", "the update file is not a setup executable"));
        }
        ProcessLauncher::launch_detached(downloaded, None)
    }
}
