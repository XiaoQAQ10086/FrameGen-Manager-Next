use std::path::{Path, PathBuf};

use fgm_domain::error::{AppError, ErrorCode};
use fgm_domain::library::{GameEntry, LibrarySource};

use crate::hashing::sha256_hex;
use crate::paths::AppPaths;
use crate::platform::DriveRoots;
use crate::platform::file_version::product_name;
use crate::process::ProcessLauncher;

use super::epic_locator::EpicLocator;
use super::graphics_api_detector::GraphicsApiDetector;
use super::library_cache::LibraryCache;
use super::library_scanner::LibraryScanner;
use super::library_store::LibraryStore;
use super::path_key;
use super::steam_locator::SteamLocator;

/// Library cache operations: scanning with manual entries, manual add, launch.
pub struct LibraryService;

impl LibraryService {
    pub fn scan(paths: &AppPaths) -> Result<Vec<GameEntry>, AppError> {
        Self::scan_with_progress(paths, |_, _, _| {})
    }

    /// Same as `scan`, forwarding launcher phases and game counts to the UI.
    pub fn scan_with_progress(
        paths: &AppPaths,
        report: impl FnMut(&'static str, usize, usize),
    ) -> Result<Vec<GameEntry>, AppError> {
        let store = Self::store(paths);
        let mut cache = store.load()?;
        let manual: Vec<GameEntry> = cache
            .games
            .iter()
            .filter(|game| game.is_manual)
            .cloned()
            .collect();

        let scanned = LibraryScanner::scan_with_progress(
            SteamLocator::locate().as_deref(),
            EpicLocator::manifests_directory().as_deref(),
            &DriveRoots::roots(),
            report,
        );
        cache.schema_version = 1;
        let mut games = merge(manual, scanned);
        apply_overrides(&mut games, &cache.target_overrides);
        cache.games = games;
        store.save(&cache)?;
        Ok(visible(&cache))
    }

    pub fn list(paths: &AppPaths) -> Vec<GameEntry> {
        let store = Self::store(paths);
        visible(&store.load().unwrap_or_else(|_| LibraryCache::default()))
    }

    /// Games the user removed from the library but whose records are kept.
    pub fn removed(paths: &AppPaths) -> Vec<GameEntry> {
        let store = Self::store(paths);
        let cache = store.load().unwrap_or_else(|_| LibraryCache::default());
        cache
            .games
            .into_iter()
            .filter(|game| cache.ignored_ids.contains(&game.id))
            .collect()
    }

    pub fn remove(paths: &AppPaths, game_id: &str) -> Result<(), AppError> {
        let store = Self::store(paths);
        let mut cache = store.load()?;
        if !cache.games.iter().any(|game| game.id == game_id) {
            return Err(AppError::new(ErrorCode::FileNotFound).with("gameId", game_id.to_string()));
        }
        if !cache.ignored_ids.iter().any(|id| id == game_id) {
            cache.ignored_ids.push(game_id.to_string());
        }
        store.save(&cache)
    }

    pub fn restore_removed(paths: &AppPaths, game_id: &str) -> Result<(), AppError> {
        let store = Self::store(paths);
        let mut cache = store.load()?;
        cache.ignored_ids.retain(|id| id != game_id);
        store.save(&cache)
    }

    pub fn forget(paths: &AppPaths, game_id: &str) -> Result<(), AppError> {
        let store = Self::store(paths);
        let mut cache = store.load()?;
        cache.games.retain(|game| game.id != game_id);
        cache.ignored_ids.retain(|id| id != game_id);
        cache.target_overrides.remove(game_id);
        store.save(&cache)
    }

    pub fn add_manual(paths: &AppPaths, executable: &Path) -> Result<GameEntry, AppError> {
        let executable = std::path::absolute(executable).map_err(|error| {
            AppError::new(ErrorCode::InvalidPath)
                .with("path", executable.display().to_string())
                .with("reason", error.to_string())
        })?;
        if !executable.is_file() {
            return Err(AppError::new(ErrorCode::FileNotFound)
                .with("path", executable.display().to_string())
                .with("reason", "the selected executable does not exist"));
        }
        let is_exe = executable
            .extension()
            .is_some_and(|extension| extension.eq_ignore_ascii_case("exe"));
        if !is_exe {
            return Err(AppError::new(ErrorCode::InvalidArgument)
                .with("path", executable.display().to_string())
                .with("reason", "select a Windows executable"));
        }
        let Some(directory) = executable.parent() else {
            return Err(AppError::new(ErrorCode::InvalidPath)
                .with("path", executable.display().to_string()));
        };
        let directory = directory.to_path_buf();

        let name = product_name(&executable)
            .filter(|name| !name.trim().is_empty())
            .or_else(|| {
                executable
                    .file_stem()
                    .map(|stem| stem.to_string_lossy().into_owned())
            })
            .unwrap_or_else(|| "Game".to_string());
        let graphics_api = GraphicsApiDetector::detect(&executable, &directory);
        let entry = GameEntry {
            id: manual_id(&executable),
            name,
            source: LibrarySource::Manual,
            install_directory: directory.clone(),
            render_exe: Some(executable),
            target_directory: directory,
            graphics_api,
            app_id: None,
            is_manual: true,
        };

        let store = Self::store(paths);
        let mut cache = store.load()?;
        let key = path_key(&entry.install_directory);
        cache
            .games
            .retain(|game| game.id != entry.id && path_key(&game.install_directory) != key);
        cache.ignored_ids.retain(|id| id != &entry.id);
        cache.games.insert(0, entry.clone());
        store.save(&cache)?;
        Ok(entry)
    }

    /// Remembers the executable the user picked for a game and applies it to
    /// the cached entry, so rescans keep using it.
    pub fn set_target_override(
        paths: &AppPaths,
        game_id: &str,
        executable: &Path,
    ) -> Result<GameEntry, AppError> {
        let executable = std::path::absolute(executable).map_err(|error| {
            AppError::new(ErrorCode::InvalidPath)
                .with("path", executable.display().to_string())
                .with("reason", error.to_string())
        })?;
        let Some(directory) = executable.parent().map(Path::to_path_buf) else {
            return Err(AppError::new(ErrorCode::InvalidPath)
                .with("path", executable.display().to_string()));
        };
        let store = Self::store(paths);
        let mut cache = store.load()?;
        let Some(game) = cache.games.iter_mut().find(|game| game.id == game_id) else {
            return Err(AppError::new(ErrorCode::FileNotFound).with("gameId", game_id.to_string()));
        };
        game.render_exe = Some(executable.clone());
        game.target_directory = directory;
        let updated = game.clone();
        cache
            .target_overrides
            .insert(game_id.to_string(), executable.display().to_string());
        store.save(&cache)?;
        Ok(updated)
    }

    /// Launches through the store's protocol handler; the UI never supplies paths.
    pub fn launch(paths: &AppPaths, game_id: &str) -> Result<(), AppError> {
        let game = Self::list(paths)
            .into_iter()
            .find(|game| game.id == game_id)
            .ok_or_else(|| {
                AppError::new(ErrorCode::FileNotFound).with("gameId", game_id.to_string())
            })?;

        match game.source {
            LibrarySource::Steam => {
                let app_id = game.app_id.as_deref().ok_or_else(|| {
                    AppError::new(ErrorCode::InvalidArgument)
                        .with("gameId", game_id.to_string())
                        .with("reason", "the Steam app id is missing")
                })?;
                if !app_id.chars().all(|character| character.is_ascii_digit()) {
                    return Err(AppError::new(ErrorCode::InvalidArgument)
                        .with("gameId", game_id.to_string()));
                }
                ProcessLauncher::open_protocol(&format!("steam://rungameid/{app_id}"))
            }
            LibrarySource::Epic => {
                let app_name = game.app_id.as_deref().ok_or_else(|| {
                    AppError::new(ErrorCode::InvalidArgument)
                        .with("gameId", game_id.to_string())
                        .with("reason", "the Epic app name is missing")
                })?;
                if !app_name
                    .chars()
                    .all(|character| character.is_ascii_alphanumeric() || "._-".contains(character))
                {
                    return Err(AppError::new(ErrorCode::InvalidArgument)
                        .with("gameId", game_id.to_string()));
                }
                ProcessLauncher::open_protocol(&format!(
                    "com.epicgames.launcher://apps/{app_name}?action=launch&silent=true"
                ))
            }
            LibrarySource::Xbox => Err(AppError::new(ErrorCode::InvalidArgument)
                .with("gameId", game_id.to_string())
                .with("reason", "Xbox games must be started from the Xbox app")),
            LibrarySource::Manual | LibrarySource::WeGame | LibrarySource::Loose => {
                let executable: PathBuf = game.render_exe.ok_or_else(|| {
                    AppError::new(ErrorCode::FileNotFound)
                        .with("gameId", game_id.to_string())
                        .with("reason", "no render executable is known")
                })?;
                let directory = executable.parent().map(Path::to_path_buf);
                ProcessLauncher::launch_detached(&executable, directory.as_deref())
            }
        }
    }

    fn store(paths: &AppPaths) -> LibraryStore {
        LibraryStore::new(paths.library().join("library.json"))
    }
}

fn apply_overrides(
    games: &mut [GameEntry],
    overrides: &std::collections::BTreeMap<String, String>,
) {
    for game in games {
        let Some(path) = overrides.get(&game.id) else {
            continue;
        };
        let executable = PathBuf::from(path);
        let Some(directory) = executable.parent().map(Path::to_path_buf) else {
            continue;
        };
        game.render_exe = Some(executable);
        game.target_directory = directory;
    }
}

fn visible(cache: &LibraryCache) -> Vec<GameEntry> {
    cache
        .games
        .iter()
        .filter(|game| !cache.ignored_ids.contains(&game.id))
        .cloned()
        .collect()
}

fn merge(manual: Vec<GameEntry>, scanned: Vec<GameEntry>) -> Vec<GameEntry> {
    let mut games = manual;
    for game in scanned {
        let key = path_key(&game.install_directory);
        if games
            .iter()
            .any(|existing| path_key(&existing.install_directory) == key)
        {
            continue;
        }
        games.push(game);
    }
    games
}

fn manual_id(executable: &Path) -> String {
    let key = executable
        .to_string_lossy()
        .to_lowercase()
        .replace('/', "\\");
    format!("manual-{}", &sha256_hex(key.as_bytes())[..16])
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn merge_keeps_manual_entries_first_and_drops_duplicates() {
        let manual = GameEntry {
            id: "manual-1".into(),
            name: "Manual".into(),
            source: LibrarySource::Manual,
            install_directory: PathBuf::from("C:\\Games\\Same"),
            render_exe: None,
            target_directory: PathBuf::from("C:\\Games\\Same"),
            graphics_api: Default::default(),
            app_id: None,
            is_manual: true,
        };
        let scanned = GameEntry {
            id: "steam-1".into(),
            name: "Steam".into(),
            source: LibrarySource::Steam,
            install_directory: PathBuf::from("c:/games/same"),
            render_exe: None,
            target_directory: PathBuf::from("c:/games/same"),
            graphics_api: Default::default(),
            app_id: Some("42".into()),
            is_manual: false,
        };
        let ignored = GameEntry {
            id: "loose-ignored".into(),
            install_directory: PathBuf::from("D:\\Games\\Ignored"),
            ..scanned.clone()
        };

        let merged = merge(vec![manual.clone()], vec![scanned, ignored.clone()]);

        assert_eq!(merged.len(), 2);
        assert_eq!(merged[0], manual);
        assert_eq!(merged[1].id, "loose-ignored");
    }
}
