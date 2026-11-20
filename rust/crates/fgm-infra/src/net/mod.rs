pub mod downloader;

mod winhttp;

pub use downloader::Downloader;

use std::sync::atomic::AtomicBool;

use fgm_domain::error::{AppError, ErrorCode};

/// Downloads a small text resource and returns its body.
///
/// Used for GitHub's release feed and its asset list, which carry the same
/// information as the API without its per-address request quota.
pub(crate) fn fetch_text(url: &str, cancelled: &AtomicBool) -> Result<String, AppError> {
    let path = std::env::temp_dir().join(format!("fgm-fetch-{}.txt", std::process::id()));
    let downloaded = winhttp::download_to(url, &path, cancelled, |_| {});
    let text = std::fs::read_to_string(&path);
    let _ = std::fs::remove_file(&path);
    downloaded?;
    text.map_err(|error| AppError::new(ErrorCode::NetworkError).with("reason", error.to_string()))
}

/// Maps a non-200 HTTP status to an error.
///
/// A 404 means "this URL exists but has nothing at it", which update sources
/// answer before their manifest is published. Keeping it apart from transport
/// failures lets callers report "no update" instead of "network error".
pub(crate) fn status_error(status: u32) -> AppError {
    let code = if status == 404 {
        ErrorCode::FileNotFound
    } else {
        ErrorCode::NetworkError
    };
    AppError::new(code)
        .with("status", status.to_string())
        .with("reason", format!("HTTP status {status}"))
}
