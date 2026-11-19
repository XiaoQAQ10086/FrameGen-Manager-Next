use std::path::Path;
use std::sync::atomic::AtomicBool;

use fgm_domain::error::AppError;

use super::winhttp;

/// HTTP/HTTPS downloader backed by WinHTTP (Windows certificate store).
pub struct Downloader;

impl Downloader {
    pub fn download(
        url: &str,
        destination: &Path,
        cancelled: &AtomicBool,
    ) -> Result<u64, AppError> {
        winhttp::download_to(url, destination, cancelled, |_| {})
    }

    pub fn download_with_progress(
        url: &str,
        destination: &Path,
        cancelled: &AtomicBool,
        progress: impl FnMut(u64),
    ) -> Result<u64, AppError> {
        winhttp::download_to(url, destination, cancelled, progress)
    }
}
