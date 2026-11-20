use std::path::PathBuf;

use fgm_domain::error::AppError;
use fgm_domain::library::{GameEntry, LibrarySource};

use crate::net::Downloader;
use crate::paths::AppPaths;

/// Downloads and caches Steam library covers.
pub struct CoverService;

/// Serializes downloads/icon extraction; urlmon and GDI+ are happier when
/// cover requests do not run concurrently on many isolate threads.
static COVER_LOCK: std::sync::Mutex<()> = std::sync::Mutex::new(());

impl CoverService {
    pub fn cover(paths: &AppPaths, game: &GameEntry) -> Result<Option<PathBuf>, AppError> {
        let _guard = COVER_LOCK.lock().unwrap_or_else(|error| error.into_inner());
        log_cover(paths, "request", game);
        if let Some(path) = steam_cover(paths, game)? {
            return Ok(Some(path));
        }
        if let Some(path) = search_cover(paths, game) {
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
        if let Some(path) = appdetails_cover(paths, app_id) {
            return Ok(Some(path));
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
            if Downloader::download_wininet(&url, &path).is_ok()
                && path.metadata().is_ok_and(|metadata| metadata.len() > 1024)
            {
                return Ok(Some(path));
            }
            let _ = std::fs::remove_file(&path);
        }
        Ok(None)
    }
}

/// Store search by title, used when the launcher id has no usable artwork.
fn search_cover(paths: &AppPaths, game: &GameEntry) -> Option<PathBuf> {
    let mut names = vec![game.name.clone()];
    let trimmed = game
        .name
        .trim_end_matches(" Demo")
        .trim_end_matches(" demo")
        .trim()
        .to_string();
    if trimmed != game.name && trimmed.len() > 3 {
        names.push(trimmed);
    }
    if let Some(executable) = game.render_exe.as_deref()
        && let Some(product) = crate::platform::product_name(executable)
    {
        let product = product.trim().to_string();
        let lower = product.to_lowercase();
        if product.len() > 3
            && lower != "game"
            && lower != "launcher"
            && !product.contains('%')
            && !names.contains(&product)
        {
            names.push(product);
        }
    }
    for name in names {
        if let Some(path) = search_cover_name(paths, &name) {
            return Some(path);
        }
    }
    None
}

fn search_cover_name(paths: &AppPaths, name: &str) -> Option<PathBuf> {
    let name = name.trim();
    if name.len() < 3 {
        return None;
    }
    let directory = paths.cache().join("covers");
    std::fs::create_dir_all(&directory).ok()?;
    let key = text_hash(name);
    let destination = directory.join(format!("search-{key}.jpg"));
    if destination
        .metadata()
        .is_ok_and(|metadata| metadata.len() > 1024)
    {
        return Some(destination);
    }
    let query = percent_encode(name);
    let json_path = directory.join(format!("search-{key}.json"));
    let url =
        format!("https://store.steampowered.com/api/storesearch/?term={query}&l=english&cc=US");
    if Downloader::download_wininet(&url, &json_path).is_err() {
        let _ = std::fs::remove_file(&json_path);
        return None;
    }
    let search = std::fs::read_to_string(&json_path)
        .ok()
        .and_then(|text| serde_json::from_str::<serde_json::Value>(&text).ok())
        .and_then(|value| {
            value
                .get("items")
                .and_then(|items| items.as_array())
                .and_then(|items| items.first())
                .cloned()
        });
    let _ = std::fs::remove_file(&json_path);
    let search = search?;
    let app_id = search.get("id").and_then(|id| id.as_u64())?;
    let urls = [
        format!(
            "https://shared.steamstatic.com/store_item_assets/steam/apps/{app_id}/library_600x900.jpg"
        ),
        format!("https://cdn.cloudflare.steamstatic.com/steam/apps/{app_id}/header.jpg"),
        format!("https://shared.steamstatic.com/store_item_assets/steam/apps/{app_id}/header.jpg"),
    ];
    for url in urls {
        if Downloader::download_wininet(&url, &destination).is_ok()
            && destination
                .metadata()
                .is_ok_and(|metadata| metadata.len() > 1024)
        {
            return Some(destination);
        }
        let _ = std::fs::remove_file(&destination);
    }
    None
}

fn text_hash(value: &str) -> String {
    let mut hash: u64 = 0xcbf2_9ce4_8422_2325;
    for byte in value.trim().to_lowercase().bytes() {
        hash ^= u64::from(byte);
        hash = hash.wrapping_mul(0x100_0000_01b3);
    }
    format!("{hash:016x}")
}

fn percent_encode(value: &str) -> String {
    let mut output = String::new();
    for byte in value.bytes() {
        if byte.is_ascii_alphanumeric() || matches!(byte, b'-' | b'_' | b'.' | b'~') {
            output.push(char::from(byte));
        } else {
            output.push_str(&format!("%{byte:02X}"));
        }
    }
    output
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

/// Uses the public appdetails endpoint, which exposes hashed CDN paths that
/// still work for demos (their plain `header.jpg` URLs 404).
fn appdetails_cover(paths: &AppPaths, app_id: &str) -> Option<PathBuf> {
    let directory = paths.cache().join("covers");
    std::fs::create_dir_all(&directory).ok()?;
    let destination = directory.join(format!("{app_id}.jpg"));
    if destination
        .metadata()
        .is_ok_and(|metadata| metadata.len() > 1024)
    {
        return Some(destination);
    }
    let json_path = directory.join(format!("appdetails-{app_id}.json"));
    let url =
        format!("https://store.steampowered.com/api/appdetails?appids={app_id}&filters=basic");
    if Downloader::download_wininet(&url, &json_path).is_err() {
        let _ = std::fs::remove_file(&json_path);
        return None;
    }
    let data = std::fs::read_to_string(&json_path)
        .ok()
        .and_then(|text| serde_json::from_str::<serde_json::Value>(&text).ok())
        .and_then(|value| value.get(app_id).cloned())
        .and_then(|entry| entry.get("data").cloned());
    let _ = std::fs::remove_file(&json_path);
    let data = data?;
    for key in ["header_image", "capsule_image", "capsule_imagev5"] {
        if let Some(url) = data.get(key).and_then(|value| value.as_str())
            && url.starts_with("http")
            && Downloader::download_wininet(url, &destination).is_ok()
            && destination
                .metadata()
                .is_ok_and(|metadata| metadata.len() > 1024)
        {
            return Some(destination);
        }
        let _ = std::fs::remove_file(&destination);
    }
    None
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
