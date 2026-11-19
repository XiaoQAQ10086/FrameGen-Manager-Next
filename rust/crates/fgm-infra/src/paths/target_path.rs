use std::path::{Path, PathBuf};

use fgm_domain::error::{AppError, ErrorCode};
use fgm_domain::safety::SafePath;

/// Combines pack/config relative paths with a deployment root and verifies the
/// result still lives under that root, including through junctions/symlinks.
pub struct TargetPath;

impl TargetPath {
    pub fn combine(root: &Path, relative: &str) -> Result<PathBuf, AppError> {
        if root.as_os_str().is_empty() || !root.is_absolute() {
            return Err(AppError::new(ErrorCode::InvalidPath)
                .with("reason", "root directory must be an absolute path"));
        }

        let normalized = SafePath::normalize_relative(relative)?;
        let root = std::path::absolute(root).map_err(|error| {
            AppError::new(ErrorCode::InvalidPath)
                .with("path", root.display().to_string())
                .with("reason", error.to_string())
        })?;

        let mut candidate = root.clone();
        for segment in normalized.split('/') {
            candidate.push(segment);
        }

        let canonical_root = canonicalize_with_missing(&root)?;
        let canonical_candidate = canonicalize_with_missing(&candidate)?;

        if !stays_under(&canonical_root, &canonical_candidate) {
            return Err(AppError::new(ErrorCode::UnsafePath)
                .with("path", candidate.display().to_string())
                .with("reason", "path escapes its root directory"));
        }

        Ok(candidate)
    }

    /// Rejects drive roots, the Windows/System directories, and the Program
    /// Files roots themselves. Subdirectories under Program Files are allowed
    /// because games commonly install there.
    pub fn is_protected_system_directory(path: &Path) -> bool {
        let Ok(candidate) = std::path::absolute(path) else {
            return false;
        };

        if candidate.parent().is_none() {
            return true;
        }

        for variable in ["SystemRoot", "windir"] {
            if let Some(directory) = std::env::var_os(variable) {
                let directory = PathBuf::from(directory);
                if directory.is_absolute() && is_under_lexical(&directory, &candidate) {
                    return true;
                }
            }
        }

        for variable in ["ProgramFiles", "ProgramFiles(x86)"] {
            if let Some(directory) = std::env::var_os(variable) {
                let directory = PathBuf::from(directory);
                if directory.is_absolute() && same_path(&directory, &candidate) {
                    return true;
                }
            }
        }

        false
    }
}

fn canonicalize_with_missing(path: &Path) -> Result<PathBuf, AppError> {
    if path.exists() {
        return std::fs::canonicalize(path).map_err(|error| io_error(path, error));
    }

    let mut missing = Vec::new();
    let mut current = path;
    loop {
        if current.exists() {
            break;
        }
        let Some(name) = current.file_name() else {
            return Err(AppError::new(ErrorCode::InvalidPath)
                .with("path", path.display().to_string())
                .with("reason", "path has no existing ancestor"));
        };
        missing.push(name.to_os_string());
        current = current.parent().ok_or_else(|| {
            AppError::new(ErrorCode::InvalidPath)
                .with("path", path.display().to_string())
                .with("reason", "path has no existing ancestor")
        })?;
    }

    let mut resolved = std::fs::canonicalize(current).map_err(|error| io_error(current, error))?;
    for name in missing.iter().rev() {
        resolved.push(name);
    }
    Ok(resolved)
}

fn stays_under(root: &Path, candidate: &Path) -> bool {
    let root = normalized_text(root);
    let candidate = normalized_text(candidate);

    candidate == root
        || candidate
            .strip_prefix(root.as_str())
            .is_some_and(|rest| rest.starts_with('\\') || rest.starts_with('/'))
}

fn is_under_lexical(root: &Path, candidate: &Path) -> bool {
    stays_under(root, candidate)
}

fn same_path(left: &Path, right: &Path) -> bool {
    normalized_text(left) == normalized_text(right)
}

fn normalized_text(path: &Path) -> String {
    let mut text = path.to_string_lossy().replace('/', "\\").to_lowercase();
    while text.len() > 3 && text.ends_with('\\') {
        text.pop();
    }
    text
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
