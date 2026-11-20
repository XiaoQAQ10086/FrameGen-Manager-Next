pub mod downloader;

mod winhttp;

pub use downloader::Downloader;

use fgm_domain::error::{AppError, ErrorCode};

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
