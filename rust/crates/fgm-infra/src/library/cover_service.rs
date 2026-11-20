use std::path::PathBuf;
use std::sync::atomic::AtomicBool;

use fgm_domain::error::AppError;
use fgm_domain::library::{GameEntry, LibrarySource};

use crate::net::Downloader;
use crate::paths::AppPaths;

/// Downloads and caches Steam library covers.
pub struct CoverService;

impl CoverService {
    pub fn cover(paths: &AppPaths, game: &GameEntry) -> Result<Option<PathBuf>, AppError> {
        if let Some(path) = steam_cover(paths, game)? {
            return Ok(Some(path));
        }
        Ok(icon_cover(paths, game))
    }
}

/// Steam store/local artwork; `None` when the game has no usable Steam art.
fn steam_cover(paths: &AppPaths, game: &GameEntry) -> Result<Option<PathBuf>, AppError> {
    {
        if game.source != LibrarySource::Steam {
            return Ok(None);
        }
        let Some(app_id) = game
            .app_id
            .as_deref()
            .filter(|id| !id.is_empty() && id.chars().all(|character| character.is_ascii_digit()))
        else {
            return Ok(None);
        };

        let directory = paths.cache().join("covers");
        let path = directory.join(format!("{app_id}.jpg"));
        if path.metadata().is_ok_and(|metadata| metadata.len() > 1024) {
            return Ok(Some(path));
        }
        if let Some(local) = find_local_cover(game, app_id) {
            return Ok(Some(local));
        }

        std::fs::create_dir_all(&directory).map_err(|error| {
            AppError::new(fgm_domain::error::ErrorCode::Internal)
                .with("path", directory.display().to_string())
                .with("reason", error.to_string())
        })?;
        // Portrait library art first, then landscape store art for games that
        // never received a `library_600x900` asset (older titles and demos).
        let urls = [
            format!(
                "https://shared.steamstatic.com/store_item_assets/steam/apps/{app_id}/library_600x900.jpg"
            ),
            format!("https://cdn.cloudflare.steamstatic.com/steam/apps/{app_id}/header.jpg"),
            format!(
                "https://shared.steamstatic.com/store_item_assets/steam/apps/{app_id}/header.jpg"
            ),
            format!(
                "https://cdn.cloudflare.steamstatic.com/steam/apps/{app_id}/capsule_616x353.jpg"
            ),
        ];
        for url in urls {
            if Downloader::download(&url, &path, &AtomicBool::new(false)).is_ok()
                && path.metadata().is_ok_and(|metadata| metadata.len() > 1024)
            {
                return Ok(Some(path));
            }
            let _ = std::fs::remove_file(&path);
        }
        Ok(None)
    }
}

/// Last resort: the executable's own icon, cached as PNG.
fn icon_cover(paths: &AppPaths, game: &GameEntry) -> Option<PathBuf> {
    let Some(executable) = game.render_exe.as_deref() else {
        log_cover(paths, "missing render exe", game);
        return None;
    };
    if !executable.is_file() {
        log_cover(paths, "render exe not found", game);
        return None;
    }
    let directory = paths.cache().join("icons");
    std::fs::create_dir_all(&directory).ok()?;
    let hash = path_hash(executable);
    let destination = directory.join(format!("{hash}.png"));
    if destination
        .metadata()
        .is_ok_and(|metadata| metadata.len() > 0)
    {
        return Some(destination);
    }
    if let Err(error) = crate::platform::extract_exe_icon(executable, &destination) {
        log_cover(paths, &format!("icon extract failed: {error:?}"), game);
        return None;
    }
    destination
        .metadata()
        .is_ok_and(|metadata| metadata.len() > 0)
        .then_some(destination)
}

fn log_cover(paths: &AppPaths, reason: &str, game: &GameEntry) {
    let log = crate::logging::OperationLog::new(paths.logs().join("fgm.log"));
    let _ = log.append(
        0,
        "debug",
        "cover fallback",
        &[("game", &game.name), ("reason", reason)],
    );
}

fn path_hash(path: &std::path::Path) -> String {
    let mut hash: u64 = 0xcbf2_9ce4_8422_2325;
    for byte in path.to_string_lossy().to_lowercase().bytes() {
        hash ^= u64::from(byte);
        hash = hash.wrapping_mul(0x100_0000_01b3);
    }
    format!("{hash:016x}")
}

/// Looks for artwork Steam already downloaded for this app (new per-app folder
/// layout first, then the legacy flat cache and user grid files).
fn find_local_cover(game: &GameEntry, app_id: &str) -> Option<PathBuf> {
    let install = game.install_directory.as_path();
    let steam_root = find_steam_root(install)?;
    let cache_root = steam_root.join("appcache").join("librarycache");
    let folder = cache_root.join(app_id);
    if let Ok(entries) = std::fs::read_dir(&folder) {
        let mut best: Option<(u8, PathBuf)> = None;
        for entry in entries.flatten() {
            let path = entry.path();
            let name = path
                .file_name()
                .map(|name| name.to_string_lossy().to_lowercase())
                .unwrap_or_default();
            if !(name.ends_with(".jpg") || name.ends_with(".jpeg") || name.ends_with(".png")) {
                continue;
            }
            let rank = if name.starts_with("library_600x900") {
                0
            } else if name.starts_with("header") {
                1
            } else if name.starts_with("library_hero") {
                2
            } else {
                continue;
            };
            let usable = path
                .metadata()
                .is_ok_and(|metadata| metadata.len() > 8 * 1024);
            if usable && best.as_ref().is_none_or(|(best_rank, _)| rank < *best_rank) {
                best = Some((rank, path));
            }
        }
        if let Some((_, path)) = best {
            return Some(path);
        }
    }

    for suffix in [
        "_library_600x900_2x.jpg",
        "_library_600x900_2x.png",
        "_library_600x900.jpg",
        "_library_600x900.png",
        "_header.jpg",
        "_library_hero.jpg",
    ] {
        let flat = cache_root.join(format!("{app_id}{suffix}"));
        if flat
            .metadata()
            .is_ok_and(|metadata| metadata.len() > 8 * 1024)
        {
            return Some(flat);
        }
    }

    let userdata = steam_root.join("userdata");
    let Ok(users) = std::fs::read_dir(&userdata) else {
        return None;
    };
    for user in users.flatten() {
        let grid = user.path().join("config").join("grid");
        for suffix in [
            "_library_600x900_2x.jpg",
            "_library_600x900_2x.png",
            "_library_600x900.jpg",
            "_library_600x900.png",
            "_header.jpg",
            "_library_hero.jpg",
        ] {
            let candidate = grid.join(format!("{app_id}{suffix}"));
            if candidate
                .metadata()
                .is_ok_and(|metadata| metadata.len() > 8 * 1024)
            {
                return Some(candidate);
            }
        }
    }
    None
}

fn find_steam_root(install_directory: &std::path::Path) -> Option<PathBuf> {
    let mut directory = install_directory;
    for _ in 0..6 {
        if directory
            .file_name()
            .is_some_and(|name| name.eq_ignore_ascii_case("steamapps"))
        {
            return directory.parent().map(std::path::Path::to_path_buf);
        }
        directory = directory.parent()?;
    }
    None
}
