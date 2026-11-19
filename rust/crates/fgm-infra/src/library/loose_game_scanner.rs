use std::collections::HashSet;
use std::path::{Path, PathBuf};

use fgm_domain::library::{GameEntry, GraphicsApi, LibrarySource};

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
const MAX_PROBE_DEPTH: usize = 3;
const MAX_PROBE_ENTRIES: usize = 5_000;
const MIN_EXE_SIZE: u64 = 64 * 1024;

/// Finds games in well-known folders on fixed/removable drives.
pub struct LooseGameScanner;

impl LooseGameScanner {
    pub fn scan(roots: &[PathBuf]) -> Vec<GameEntry> {
        let mut games = Vec::new();
        let mut seen = HashSet::new();

        for root in roots {
            let mut found_for_root = 0;
            for known in KNOWN_DIRECTORIES {
                if found_for_root >= MAX_GAMES_PER_ROOT {
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
                    if found_for_root >= MAX_GAMES_PER_ROOT {
                        break;
                    }
                    let directory = entry.path();
                    let Ok(metadata) = std::fs::symlink_metadata(&directory) else {
                        continue;
                    };
                    if !metadata.is_dir() || is_reparse_point(&metadata) {
                        continue;
                    }
                    if !contains_executable(&directory) {
                        continue;
                    }

                    let key = directory.to_string_lossy().to_lowercase();
                    if !seen.insert(key) {
                        continue;
                    }

                    let name = directory.file_name().map_or_else(
                        || "Unknown".to_string(),
                        |name| name.to_string_lossy().into_owned(),
                    );
                    let render = RenderExeSelector::select(&directory);
                    let target_directory = render
                        .as_ref()
                        .and_then(|candidate| candidate.path.parent())
                        .map_or_else(|| directory.clone(), Path::to_path_buf);
                    let graphics_api = render.as_ref().map_or(GraphicsApi::Unknown, |candidate| {
                        GraphicsApiDetector::detect(&candidate.path, &directory)
                    });
                    let Ok(key) = DeploymentKey::for_path(&directory) else {
                        continue;
                    };

                    games.push(GameEntry {
                        id: format!("loose:{key}"),
                        name,
                        source: LibrarySource::Loose,
                        install_directory: directory,
                        render_exe: render.map(|candidate| candidate.path),
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

fn contains_executable(directory: &Path) -> bool {
    let mut stack = vec![(directory.to_path_buf(), 0_usize)];
    let mut entries_seen = 0_usize;

    while let Some((current, depth)) = stack.pop() {
        let Ok(entries) = std::fs::read_dir(&current) else {
            continue;
        };
        for entry in entries.flatten() {
            entries_seen += 1;
            if entries_seen > MAX_PROBE_ENTRIES {
                return false;
            }
            let path = entry.path();
            let Ok(metadata) = std::fs::symlink_metadata(&path) else {
                continue;
            };
            if is_reparse_point(&metadata) {
                continue;
            }
            if metadata.is_dir() {
                if depth < MAX_PROBE_DEPTH {
                    stack.push((path, depth + 1));
                }
                continue;
            }
            if metadata.is_file()
                && metadata.len() >= MIN_EXE_SIZE
                && path
                    .extension()
                    .is_some_and(|extension| extension.eq_ignore_ascii_case("exe"))
            {
                return true;
            }
        }
    }
    false
}

#[cfg(windows)]
fn is_reparse_point(metadata: &std::fs::Metadata) -> bool {
    use std::os::windows::fs::MetadataExt;
    metadata.file_attributes() & 0x400 != 0
}

#[cfg(not(windows))]
fn is_reparse_point(metadata: &std::fs::Metadata) -> bool {
    metadata.file_type().is_symlink()
}
