use std::collections::HashSet;
use std::path::{Path, PathBuf};

use fgm_domain::library::GameEntry;

use super::epic_game_scanner::EpicGameScanner;
use super::epic_locator::EpicLocator;
use super::loose_game_scanner::LooseGameScanner;
use super::steam_game_scanner::SteamGameScanner;
use super::steam_locator::SteamLocator;

/// Aggregates Steam, Epic and loose-folder results with install-directory dedup.
pub struct LibraryScanner;

impl LibraryScanner {
    pub fn scan(roots: &[PathBuf]) -> Vec<GameEntry> {
        let steam = SteamLocator::locate();
        let epic = EpicLocator::manifests_directory();
        Self::scan_with(steam.as_deref(), epic.as_deref(), roots)
    }

    pub fn scan_with(
        steam_root: Option<&Path>,
        epic_manifests: Option<&Path>,
        roots: &[PathBuf],
    ) -> Vec<GameEntry> {
        let mut games = Vec::new();
        let mut seen = HashSet::new();

        if let Some(steam_root) = steam_root {
            append(&mut games, &mut seen, SteamGameScanner::scan(steam_root));
        }
        if let Some(epic_manifests) = epic_manifests {
            append(&mut games, &mut seen, EpicGameScanner::scan(epic_manifests));
        }
        append(&mut games, &mut seen, LooseGameScanner::scan(roots));

        games
    }
}

fn append(games: &mut Vec<GameEntry>, seen: &mut HashSet<String>, entries: Vec<GameEntry>) {
    for entry in entries {
        let key = entry.install_directory.to_string_lossy().to_lowercase();
        if seen.insert(key) {
            games.push(entry);
        }
    }
}
