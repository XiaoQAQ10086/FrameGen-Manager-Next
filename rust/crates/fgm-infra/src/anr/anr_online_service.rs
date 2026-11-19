use std::path::{Path, PathBuf};
use std::sync::atomic::AtomicBool;

use crate::net::Downloader;
use crate::updates::{UpdateDownloader, UpdateInstaller};
use fgm_domain::error::{AppError, ErrorCode};
use serde_json::Value;

use super::anr_package_info::AnrPackageInfo;
use super::anr_package_service::AnrPackageService;

const DEFAULT_RELEASE_API: &str = "https://api.github.com/repos/apex-compute/ANR/releases/latest";
const MIRRORS: [&str; 3] = [
    "https://ghfast.top/",
    "https://gh-proxy.com/",
    "https://ghproxy.net/",
];

/// Downloads the latest ANR full package and imports it.
pub struct AnrOnlineService;

impl AnrOnlineService {
    pub fn download_latest(
        data_root: &Path,
        cancelled: &AtomicBool,
        progress: impl FnMut(u64),
    ) -> Result<AnrPackageInfo, AppError> {
        Self::download_from(DEFAULT_RELEASE_API, data_root, cancelled, progress)
    }

    pub fn download_from(
        api_url: &str,
        data_root: &Path,
        cancelled: &AtomicBool,
        progress: impl FnMut(u64),
    ) -> Result<AnrPackageInfo, AppError> {
        let release_path = release_json_path(data_root);
        if let Some(parent) = release_path.parent() {
            std::fs::create_dir_all(parent).map_err(|error| {
                AppError::new(ErrorCode::Internal)
                    .with("path", parent.display().to_string())
                    .with("reason", error.to_string())
            })?;
        }
        Downloader::download(api_url, &release_path, cancelled)?;
        let text = std::fs::read_to_string(&release_path).map_err(|error| {
            AppError::new(ErrorCode::Internal).with("reason", error.to_string())
        })?;
        let _ = std::fs::remove_file(&release_path);

        let release: Value = serde_json::from_str(&text).map_err(|error| {
            AppError::new(ErrorCode::NetworkError).with("reason", error.to_string())
        })?;
        let tag = release
            .get("tag_name")
            .and_then(Value::as_str)
            .unwrap_or("unknown")
            .trim_start_matches(['v', 'V'])
            .to_string();
        let installer = select_installer(&release)?;
        let zip = UpdateDownloader::download(&installer, &tag, cancelled, progress)?;
        let info = AnrPackageService::import(&zip, data_root)?;
        let _ = std::fs::remove_file(zip);
        Ok(info)
    }
}

fn select_installer(release: &Value) -> Result<UpdateInstaller, AppError> {
    let assets = release
        .get("assets")
        .and_then(Value::as_array)
        .ok_or_else(|| {
            AppError::new(ErrorCode::NetworkError).with("reason", "release has no assets")
        })?;

    for asset in assets {
        let name = asset
            .get("name")
            .and_then(Value::as_str)
            .unwrap_or_default();
        let lower = name.to_lowercase();
        if !lower.starts_with("anr-fg-")
            || !lower.ends_with("-full.zip")
            || lower.contains("addon")
            || lower.contains("optiscaler")
            || lower.contains("kit")
        {
            continue;
        }

        let url = asset
            .get("browser_download_url")
            .and_then(Value::as_str)
            .ok_or_else(|| {
                AppError::new(ErrorCode::NetworkError).with("reason", "asset URL is missing")
            })?;
        let size = asset.get("size").and_then(Value::as_u64).ok_or_else(|| {
            AppError::new(ErrorCode::NetworkError).with("reason", "asset size is missing")
        })?;
        let digest = asset.get("digest").and_then(Value::as_str).ok_or_else(|| {
            AppError::new(ErrorCode::PackHashMismatch).with("reason", "asset digest is missing")
        })?;
        let sha256 = digest.strip_prefix("sha256:").unwrap_or(digest).to_string();
        if sha256.len() != 64 || !sha256.bytes().all(|byte| byte.is_ascii_hexdigit()) {
            return Err(AppError::new(ErrorCode::PackHashMismatch)
                .with("reason", "asset digest is not a SHA-256"));
        }

        let mut urls = vec![url.to_string()];
        for mirror in MIRRORS {
            urls.push(format!("{mirror}{url}"));
        }

        return Ok(UpdateInstaller {
            name: name.to_string(),
            size,
            sha256,
            urls,
        });
    }

    Err(AppError::new(ErrorCode::PackNotFound).with("reason", "no anr-fg-*-full.zip asset found"))
}

fn release_json_path(data_root: &Path) -> PathBuf {
    data_root
        .join("cache")
        .join(format!("anr-release-{}.json", std::process::id()))
}
