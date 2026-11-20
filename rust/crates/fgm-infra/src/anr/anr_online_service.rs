use std::path::{Path, PathBuf};
use std::sync::atomic::{AtomicBool, Ordering};

use crate::net::{self, Downloader};
use crate::updates::{UpdateDownloader, UpdateInstaller};
use fgm_domain::error::{AppError, ErrorCode};
use serde_json::Value;

use super::anr_package_info::AnrPackageInfo;
use super::anr_package_service::AnrPackageService;

const DEFAULT_RELEASE_API: &str = "https://api.github.com/repos/apex-compute/ANR/releases/latest";
const RELEASES_FEED: &str = "https://github.com/apex-compute/ANR/releases.atom";
const RELEASES_BASE: &str = "https://github.com/apex-compute/ANR/releases";
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
        mut progress: impl FnMut(u64),
        mut on_total: impl FnMut(u64),
    ) -> Result<AnrPackageInfo, AppError> {
        match Self::download_from(
            DEFAULT_RELEASE_API,
            data_root,
            cancelled,
            &mut progress,
            &mut on_total,
        ) {
            Ok(info) => Ok(info),
            // The API allows 60 unauthenticated requests an hour per address;
            // once that runs out every download fails with 403 even though
            // GitHub itself is reachable. The release pages carry the same
            // information and have no such quota.
            Err(error) if !cancelled.load(Ordering::Relaxed) => {
                Self::download_from_pages(data_root, cancelled, &mut progress)
                    .map_err(|fallback| fallback.with("apiReason", first_reason(&error)))
            }
            Err(error) => Err(error),
        }
    }

    pub fn download_from(
        api_url: &str,
        data_root: &Path,
        cancelled: &AtomicBool,
        progress: &mut impl FnMut(u64),
        on_total: &mut impl FnMut(u64),
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
        // The UI needs a denominator to draw a determinate bar; the release
        // manifest already carries the package size.
        on_total(installer.size);
        let zip = UpdateDownloader::download(&installer, &tag, cancelled, progress)?;
        let info = AnrPackageService::import(&zip, data_root)?;
        let _ = std::fs::remove_file(zip);
        Ok(info)
    }

    /// Resolves and downloads the package through the release pages.
    ///
    /// No size or checksum is available this way, so the UI keeps the
    /// indeterminate bar it shows before the manifest arrives.
    fn download_from_pages(
        data_root: &Path,
        cancelled: &AtomicBool,
        progress: &mut impl FnMut(u64),
    ) -> Result<AnrPackageInfo, AppError> {
        let tag = latest_tag(cancelled)?;
        let url = package_url(&tag, cancelled)?;
        let name = url.rsplit('/').next().unwrap_or("anr-package.zip");
        let directory = std::env::temp_dir()
            .join("FrameGen-Manager-Next")
            .join("updates")
            .join(&tag);
        std::fs::create_dir_all(&directory).map_err(|error| {
            AppError::new(ErrorCode::Internal)
                .with("path", directory.display().to_string())
                .with("reason", error.to_string())
        })?;
        let zip = directory.join(name);
        Downloader::download_with_progress(&url, &zip, cancelled, progress)?;
        let info = AnrPackageService::import(&zip, data_root)?;
        let _ = std::fs::remove_file(&zip);
        Ok(info)
    }
}

/// Tag of the newest release, read from the releases feed.
fn latest_tag(cancelled: &AtomicBool) -> Result<String, AppError> {
    tag_from_feed(&net::fetch_text(RELEASES_FEED, cancelled)?)
}

fn tag_from_feed(feed: &str) -> Result<String, AppError> {
    let marker = "releases/tag/";
    let start = feed
        .find(marker)
        .ok_or_else(|| no_package("release feed has no entries"))?;
    let rest = &feed[start + marker.len()..];
    let end = rest.find(['"', '<', '&']).unwrap_or(rest.len());
    let tag = rest[..end].trim();
    if tag.is_empty() {
        return Err(no_package("release feed has no tag"));
    }
    Ok(tag.to_string())
}

/// Download URL of the full package for a tag, read from the asset list page.
fn package_url(tag: &str, cancelled: &AtomicBool) -> Result<String, AppError> {
    let html = net::fetch_text(&format!("{RELEASES_BASE}/expanded_assets/{tag}"), cancelled)?;
    package_from_assets(&html, tag)
}

fn package_from_assets(html: &str, tag: &str) -> Result<String, AppError> {
    let marker = format!("releases/download/{tag}/");
    let mut names: Vec<String> = Vec::new();
    let mut rest = html;
    while let Some(index) = rest.find(&marker) {
        let after = &rest[index + marker.len()..];
        let end = after.find(['"', '<', '&']).unwrap_or(after.len());
        let name = after[..end].trim();
        if name.ends_with(".zip") {
            names.push(name.to_string());
        }
        rest = after;
    }
    let name = names
        .iter()
        .find(|name| name.contains("-full") && !name.contains("source"))
        .or_else(|| names.iter().find(|name| name.contains("-addon")))
        .ok_or_else(|| no_package("release has no package asset"))?;
    Ok(format!("{RELEASES_BASE}/download/{tag}/{name}"))
}

fn no_package(reason: &str) -> AppError {
    AppError::new(ErrorCode::NetworkError).with("reason", reason)
}

fn first_reason(error: &AppError) -> &str {
    error
        .context
        .iter()
        .find(|(key, _)| key == "reason")
        .map_or("API request failed", |(_, value)| value.as_str())
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

#[cfg(test)]
mod tests {
    use super::*;

    const FEED: &str = r#"<feed>
        <link rel="alternate" href="https://github.com/apex-compute/ANR/releases"/>
        <entry><id>tag:github.com,2008:Repository/1/FG-V.B20</id>
          <link rel="alternate" href="https://github.com/apex-compute/ANR/releases/tag/FG-V.B20"/></entry>
        </feed>"#;

    const ASSETS: &str = r#"<div>
        <a href="/apex-compute/ANR/releases/download/FG-V.B20/anr-fg-V.B20-addon.zip">addon</a>
        <a href="/apex-compute/ANR/releases/download/FG-V.B20/anr-fg-V.B20-full-optiscaler-source.zip">src</a>
        <a href="/apex-compute/ANR/releases/download/FG-V.B20/anr-fg-V.B20-full.zip">full</a>
        </div>"#;

    #[test]
    fn reads_the_newest_tag_from_the_feed() {
        assert_eq!(tag_from_feed(FEED).unwrap(), "FG-V.B20");
    }

    #[test]
    fn prefers_the_full_package_over_the_addon_and_the_source_kit() {
        assert_eq!(
            package_from_assets(ASSETS, "FG-V.B20").unwrap(),
            "https://github.com/apex-compute/ANR/releases/download/FG-V.B20/anr-fg-V.B20-full.zip"
        );
    }

    #[test]
    fn reports_a_feed_without_entries() {
        assert!(tag_from_feed("<feed/>").is_err());
    }

    #[test]
    fn reports_a_release_without_a_package() {
        assert!(package_from_assets("<div/>", "FG-V.B20").is_err());
    }
}
