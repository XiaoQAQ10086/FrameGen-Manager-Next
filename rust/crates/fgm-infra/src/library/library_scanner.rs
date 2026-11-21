use std::collections::HashSet;
use std::path::{Path, PathBuf};
use std::sync::atomic::AtomicBool;

use fgm_domain::library::GameEntry;

use super::epic_game_scanner::EpicGameScanner;
use super::epic_locator::EpicLocator;
use super::loose_game_scanner::LooseGameScanner;
use super::steam_game_scanner::SteamGameScanner;
use super::steam_locator::SteamLocator;
use super::xbox_game_scanner::XboxGameScanner;

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
        Self::scan_with_progress(steam_root, epic_manifests, roots, |_, _, _| {})
    }

    /// Scans every launcher and reports `(phase, completed_sources, found)` as
    /// each source starts, so the UI can show progress instead of a spinner.
    pub fn scan_with_progress(
        steam_root: Option<&Path>,
        epic_manifests: Option<&Path>,
        roots: &[PathBuf],
        report: impl FnMut(&'static str, usize, usize),
    ) -> Vec<GameEntry> {
        Self::scan_with_progress_cancelled(steam_root, epic_manifests, roots, None, report)
    }

    pub fn scan_with_progress_cancelled(
        steam_root: Option<&Path>,
        epic_manifests: Option<&Path>,
        roots: &[PathBuf],
        cancelled: Option<&AtomicBool>,
        mut report: impl FnMut(&'static str, usize, usize),
    ) -> Vec<GameEntry> {
        let mut games = Vec::new();
        let mut seen = HashSet::new();
        let mut completed = 0;

        report("steam", completed, games.len());
        if let Some(steam_root) = steam_root {
            append(&mut games, &mut seen, SteamGameScanner::scan(steam_root));
        }
        completed += 1;
        if is_cancelled(cancelled) {
            report("finalizing", completed, games.len());
            return games;
        }

        report("epic", completed, games.len());
        if let Some(epic_manifests) = epic_manifests {
            append(&mut games, &mut seen, EpicGameScanner::scan(epic_manifests));
        }
        completed += 1;
        if is_cancelled(cancelled) {
            report("finalizing", completed, games.len());
            return games;
        }

        report("xbox", completed, games.len());
        append(&mut games, &mut seen, XboxGameScanner::scan(roots));
        completed += 1;
        if is_cancelled(cancelled) {
            report("finalizing", completed, games.len());
            return games;
        }

        report("loose", completed, games.len());
        append(
            &mut games,
            &mut seen,
            LooseGameScanner::scan_cancelled(roots, cancelled),
        );
        completed += 1;

        report("finalizing", completed, games.len());
        games
    }
}

fn is_cancelled(cancelled: Option<&AtomicBool>) -> bool {
    cancelled.is_some_and(|flag| flag.load(std::sync::atomic::Ordering::Relaxed))
}

fn append(games: &mut Vec<GameEntry>, seen: &mut HashSet<String>, entries: Vec<GameEntry>) {
    for entry in entries {
        let key = super::path_key(&entry.install_directory);
        if seen.insert(key) {
            games.push(entry);
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use fgm_domain::library::{GraphicsApi, LibrarySource};
    use std::path::PathBuf;

    #[test]
    fn deduplicates_mixed_separators_and_case() {
        let entry = |path: &str| GameEntry {
            id: path.to_string(),
            name: "Game".to_string(),
            source: LibrarySource::Steam,
            install_directory: PathBuf::from(path),
            render_exe: None,
            target_directory: PathBuf::from(path),
            graphics_api: GraphicsApi::Unknown,
            app_id: None,
            is_manual: false,
        };
        let mut games = Vec::new();
        let mut seen = HashSet::new();

        append(
            &mut games,
            &mut seen,
            vec![
                entry("d:/steam/steamapps/common/Game"),
                entry("D:\\Steam\\steamapps\\common\\Game"),
            ],
        );

        assert_eq!(games.len(), 1);
    }
}
