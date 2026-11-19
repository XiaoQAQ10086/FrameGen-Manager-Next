use std::path::PathBuf;
use std::sync::atomic::AtomicBool;

use fgm_domain::error::{AppError, ErrorCode};

use crate::net::Downloader;

use super::update_info::UpdateInfo;
use super::update_manifest::UpdateManifest;

/// Fetches and compares `update.json` manifests.
pub struct UpdateChecker;

impl UpdateChecker {
    pub fn check(
        urls: &[String],
        current_version: &str,
        cancelled: &AtomicBool,
    ) -> Result<UpdateInfo, AppError> {
        let mut last_error = None;
        for url in urls {
            let path = manifest_path();
            match Downloader::download(url, &path, cancelled).and_then(|_| {
                std::fs::read(&path).map_err(|error| {
                    AppError::new(ErrorCode::FileNotFound)
                        .with("path", path.display().to_string())
                        .with("reason", error.to_string())
                })
            }) {
                Ok(bytes) => {
                    let _ = std::fs::remove_file(&path);
                    let manifest: UpdateManifest =
                        serde_json::from_slice(&bytes).map_err(|error| {
                            AppError::new(ErrorCode::PackInvalid).with("reason", error.to_string())
                        })?;
                    let available = is_newer(&manifest.version, current_version);
                    return Ok(UpdateInfo {
                        available,
                        current_version: current_version.to_string(),
                        manifest,
                    });
                }
                Err(error) => {
                    let _ = std::fs::remove_file(&path);
                    last_error = Some(error);
                }
            }
        }
        Err(last_error.unwrap_or_else(|| {
            AppError::new(ErrorCode::NetworkError).with("reason", "no update source")
        }))
    }
}

fn manifest_path() -> PathBuf {
    std::env::temp_dir()
        .join("FrameGen-Manager-Next")
        .join("updates")
        .join(format!("update-{}.json", std::process::id()))
}

fn is_newer(candidate: &str, current: &str) -> bool {
    let Some(candidate) = parse_version(candidate) else {
        return false;
    };
    let Some(current) = parse_version(current) else {
        return false;
    };
    candidate > current
}

fn parse_version(value: &str) -> Option<(u64, u64, u64)> {
    let value = value.trim().trim_start_matches(['v', 'V']);
    let core = value
        .split(['-', '+'])
        .next()?
        .split('.')
        .map(|part| part.parse::<u64>().ok())
        .collect::<Option<Vec<_>>>()?;
    if core.is_empty() {
        return None;
    }
    Some((
        *core.first()?,
        *core.get(1).unwrap_or(&0),
        *core.get(2).unwrap_or(&0),
    ))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn compares_versions_ignoring_prefix_and_build() {
        assert!(is_newer("v2.3.4", "2.3.3"));
        assert!(is_newer("2.4.0+meta", "2.3.9"));
        assert!(!is_newer("2.3.3", "2.3.3"));
        assert!(!is_newer("2.3.2", "2.3.3"));
    }
}
