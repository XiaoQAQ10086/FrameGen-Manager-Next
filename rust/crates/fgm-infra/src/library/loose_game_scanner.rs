use std::collections::HashSet;
use std::path::{Path, PathBuf};
use std::sync::atomic::{AtomicBool, Ordering};

use fgm_domain::library::{GameEntry, LibrarySource};

use crate::fs::DeploymentKey;

use super::graphics_api_detector::GraphicsApiDetector;
use super::render_exe_selector::RenderExeSelector;

const KNOWN_DIRECTORIES: [&str; 11] = [
    "Games",
    "Game",
    "My Games",
    "SteamLibrary",
    "GOG Games",
    "Epic Games",
    "Origin Games",
    "Repacks",
    "游戏",
    "单机游戏",
    "游戏库",
];
const MAX_SUBDIRECTORIES: usize = 300;
const MAX_GAMES_PER_ROOT: usize = 80;

/// Finds games in well-known folders on fixed/removable drives.
pub struct LooseGameScanner;

impl LooseGameScanner {
    pub fn scan(roots: &[PathBuf]) -> Vec<GameEntry> {
        Self::scan_cancelled(roots, None)
    }

    pub fn scan_cancelled(roots: &[PathBuf], cancelled: Option<&AtomicBool>) -> Vec<GameEntry> {
        let mut games = Vec::new();
        let mut seen = HashSet::new();

        for root in roots {
            if is_cancelled(cancelled) {
                break;
            }
            let mut found_for_root = 0;
            for known in KNOWN_DIRECTORIES {
                if found_for_root >= MAX_GAMES_PER_ROOT || is_cancelled(cancelled) {
                    break;
                }
                let base = root.join(known);
                if !base.is_dir() {
                    continue;
                }
                let Ok(entries) = std::fs::read_dir(&base) else {
                    continue;
                };

                for entry in entries.flatten().take(MAX_SUBDIRECTORIES) {
                    if found_for_root >= MAX_GAMES_PER_ROOT || is_cancelled(cancelled) {
                        break;
                    }
                    let directory = entry.path();
                    let Ok(metadata) = std::fs::symlink_metadata(&directory) else {
                        continue;
                    };
                    if !metadata.is_dir() || is_reparse_point(&metadata) {
                        continue;
                    }
                    let directory_name = directory.file_name().and_then(|name| name.to_str());
                    if directory_name.is_some_and(|name| {
                        name.eq_ignore_ascii_case("steamapps")
                            || name.eq_ignore_ascii_case("common")
                    }) {
                        continue;
                    }
                    let render = RenderExeSelector::select(&directory);
                    let Some(render) = render else {
                        continue;
                    };
                    let depth = render
                        .path
                        .strip_prefix(&directory)
                        .map_or(usize::MAX, |relative| relative.components().count());
                    if depth > 4 {
                        continue;
                    }

                    let key = super::path_key(&directory);
                    if !seen.insert(key) {
                        continue;
                    }

                    let name = directory.file_name().map_or_else(
                        || "Unknown".to_string(),
                        |name| name.to_string_lossy().into_owned(),
                    );
                    let target_directory = render
                        .path
                        .parent()
                        .map_or_else(|| directory.clone(), Path::to_path_buf);
                    let graphics_api = GraphicsApiDetector::detect(&render.path, &directory);
                    let Ok(key) = DeploymentKey::for_path(&directory) else {
                        continue;
                    };

                    games.push(GameEntry {
                        id: format!("loose:{key}"),
                        name,
                        source: LibrarySource::Loose,
                        install_directory: directory,
                        render_exe: Some(render.path),
                        target_directory,
                        graphics_api,
                        app_id: None,
                        is_manual: false,
                    });
                    found_for_root += 1;
                }
            }
        }

        games
    }
}

fn is_cancelled(cancelled: Option<&AtomicBool>) -> bool {
    cancelled.is_some_and(|flag| flag.load(Ordering::Relaxed))
}

fn is_reparse_point(metadata: &std::fs::Metadata) -> bool {
    use std::os::windows::fs::MetadataExt;
    metadata.file_attributes() & 0x400 != 0
}

#[cfg(not(windows))]
fn is_reparse_point(metadata: &std::fs::Metadata) -> bool {
    metadata.file_type().is_symlink()
}
