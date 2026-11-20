use std::path::Path;
use std::sync::atomic::AtomicBool;

use fgm_domain::error::AppError;

use super::winhttp;

#[link(name = "urlmon")]
unsafe extern "system" {
    fn URLDownloadToFileW(
        caller: *mut std::ffi::c_void,
        url: *const u16,
        file_name: *const u16,
        reserved: u32,
        callback: *mut std::ffi::c_void,
    ) -> i32;
}

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

    // WinINET follows the same proxy/TLS settings as the launcher UI.
    pub fn download_wininet(url: &str, destination: &Path) -> Result<u64, AppError> {
        if let Some(parent) = destination.parent() {
            std::fs::create_dir_all(parent).map_err(|error| {
                AppError::new(fgm_domain::error::ErrorCode::Internal)
                    .with("path", parent.display().to_string())
                    .with("reason", error.to_string())
            })?;
        }
        let url = wide(url);
        let file = wide(&destination.to_string_lossy());
        let status = unsafe {
            URLDownloadToFileW(
                std::ptr::null_mut(),
                url.as_ptr(),
                file.as_ptr(),
                0,
                std::ptr::null_mut(),
            )
        };
        let size = destination
            .metadata()
            .map(|metadata| metadata.len())
            .unwrap_or(0);
        if status < 0 || size == 0 {
            let _ = std::fs::remove_file(destination);
            return Err(AppError::new(fgm_domain::error::ErrorCode::NetworkError)
                .with("reason", format!("WinINET download failed ({status})")));
        }
        Ok(size)
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

fn wide(value: &str) -> Vec<u16> {
    value.encode_utf16().chain(std::iter::once(0)).collect()
}
