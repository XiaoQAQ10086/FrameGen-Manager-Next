use std::path::{Path, PathBuf};
use std::sync::atomic::AtomicBool;

use fgm_domain::error::{AppError, ErrorCode};

use crate::hashing::FileHasher;
use crate::net::Downloader;

use super::update_installer::UpdateInstaller;
use super::update_source::UpdateSource;

/// Downloads an installer, trying each mirror until size and hash match.
pub struct UpdateDownloader;

impl UpdateDownloader {
    pub fn download(
        installer: &UpdateInstaller,
        version: &str,
        cancelled: &AtomicBool,
        mut progress: impl FnMut(u64),
    ) -> Result<PathBuf, AppError> {
        let name = Path::new(&installer.name)
            .file_name()
            .map_or_else(String::new, |name| name.to_string_lossy().into_owned());
        if name.is_empty() || installer.urls.is_empty() {
            return Err(AppError::new(ErrorCode::PackInvalid)
                .with("reason", "installer name or URLs are missing"));
        }

        let directory = std::env::temp_dir()
            .join("FrameGen-Manager-Next")
            .join("updates")
            .join(version);
        std::fs::create_dir_all(&directory).map_err(|error| {
            AppError::new(ErrorCode::Internal)
                .with("path", directory.display().to_string())
                .with("reason", error.to_string())
        })?;

        let destination = directory.join(&name);
        let part = directory.join(format!("{name}.part"));
        let mut last_error = None;

        for url in installer.urls.iter().flat_map(|url| {
            let mut candidates = UpdateSource::mirrors(url);
            if candidates.is_empty() {
                candidates.push(url.clone());
            }
            candidates
        }) {
            let _ = std::fs::remove_file(&part);
            let downloaded =
                Downloader::download_with_progress(&url, &part, cancelled, &mut progress);
            if let Err(error) = downloaded {
                last_error = Some(error);
                continue;
            }

            let size_ok = std::fs::metadata(&part)
                .map(|metadata| metadata.len() == installer.size)
                .unwrap_or(false);
            let hash_ok = FileHasher::sha256_file(&part)
                .map(|hash| hash.eq_ignore_ascii_case(&installer.sha256))
                .unwrap_or(false);
            if !size_ok || !hash_ok {
                last_error = Some(
                    AppError::new(ErrorCode::PackHashMismatch)
                        .with("path", part.display().to_string()),
                );
                let _ = std::fs::remove_file(&part);
                continue;
            }

            std::fs::rename(&part, &destination).map_err(|error| {
                AppError::new(ErrorCode::Internal)
                    .with("path", destination.display().to_string())
                    .with("reason", error.to_string())
            })?;
            return Ok(destination);
        }

        Err(last_error.unwrap_or_else(|| {
            AppError::new(ErrorCode::NetworkError).with("reason", "no installer mirror")
        }))
    }
}
