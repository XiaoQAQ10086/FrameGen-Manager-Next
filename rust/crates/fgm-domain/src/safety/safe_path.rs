use crate::error::{AppError, ErrorCode};

/// Validates relative paths from packs and configuration before they reach the
/// file system. This is pure string validation; it never touches the disk.
pub struct SafePath;

impl SafePath {
    pub const MAX_RELATIVE_PATH_LENGTH: usize = 240;
    pub const MAX_SEGMENT_LENGTH: usize = 255;

    /// Normalizes separators to `/` and rejects anything that could escape a
    /// deployment root or collide with a Windows device name.
    pub fn normalize_relative(value: &str) -> Result<String, AppError> {
        if value.trim().is_empty() {
            return Err(fail(ErrorCode::InvalidPath, value, "path is empty"));
        }

        if value != value.trim() {
            return Err(fail(
                ErrorCode::InvalidPath,
                value,
                "path has leading or trailing whitespace",
            ));
        }

        let normalized = value.replace('\\', "/");

        if normalized.starts_with('/') {
            return Err(fail(
                ErrorCode::UnsafePath,
                value,
                "absolute, rooted or UNC paths are not allowed",
            ));
        }

        if normalized.contains(':') {
            return Err(fail(
                ErrorCode::UnsafePath,
                value,
                "drive letters and alternate data streams are not allowed",
            ));
        }

        if normalized
            .chars()
            .any(|character| matches!(character, '<' | '>' | '"' | '|' | '?' | '*'))
        {
            return Err(fail(
                ErrorCode::InvalidPath,
                value,
                "path contains characters that are invalid on Windows",
            ));
        }

        if normalized.chars().any(char::is_control) {
            return Err(fail(
                ErrorCode::InvalidPath,
                value,
                "path contains control characters",
            ));
        }

        for segment in normalized.split('/') {
            if segment.is_empty() || segment == "." || segment == ".." {
                return Err(fail(
                    ErrorCode::UnsafePath,
                    value,
                    "empty, '.' and '..' segments are not allowed",
                ));
            }

            if segment.chars().count() > Self::MAX_SEGMENT_LENGTH {
                return Err(fail(
                    ErrorCode::InvalidPath,
                    value,
                    "path segment is too long",
                ));
            }

            if segment.starts_with(' ') || segment.ends_with(' ') || segment.ends_with('.') {
                return Err(fail(
                    ErrorCode::InvalidPath,
                    value,
                    "segment has an unsafe leading space or trailing dot/space",
                ));
            }

            if is_reserved_device_name(segment) {
                return Err(fail(
                    ErrorCode::InvalidPath,
                    value,
                    "path uses a reserved Windows device name",
                ));
            }
        }

        if normalized.chars().count() > Self::MAX_RELATIVE_PATH_LENGTH {
            return Err(fail(ErrorCode::InvalidPath, value, "path is too long"));
        }

        Ok(normalized)
    }
}

fn is_reserved_device_name(segment: &str) -> bool {
    let base = segment
        .split_once('.')
        .map_or(segment, |(before, _)| before);
    matches!(
        base.to_ascii_uppercase().as_str(),
        "CON"
            | "PRN"
            | "AUX"
            | "NUL"
            | "CONIN$"
            | "CONOUT$"
            | "COM1"
            | "COM2"
            | "COM3"
            | "COM4"
            | "COM5"
            | "COM6"
            | "COM7"
            | "COM8"
            | "COM9"
            | "LPT1"
            | "LPT2"
            | "LPT3"
            | "LPT4"
            | "LPT5"
            | "LPT6"
            | "LPT7"
            | "LPT8"
            | "LPT9"
    )
}

fn fail(code: ErrorCode, path: &str, reason: &str) -> AppError {
    AppError::new(code)
        .with("path", path)
        .with("reason", reason)
}
