use std::collections::HashSet;
use std::path::{Path, PathBuf};

use fgm_domain::library::{GameEntry, LibrarySource};

use crate::fs::DeploymentKey;

use super::graphics_api_detector::GraphicsApiDetector;
use super::render_exe_selector::RenderExeSelector;

const MAX_GAMES: usize = 300;

/// Finds Microsoft Store games under each drive's `XboxGames` directory.
pub struct XboxGameScanner;

impl XboxGameScanner {
    pub fn scan(roots: &[PathBuf]) -> Vec<GameEntry> {
        let mut games = Vec::new();
        let mut seen = HashSet::new();
        for root in roots {
            let base = root.join("XboxGames");
            if !base.is_dir() {
                continue;
            }
            let Ok(entries) = std::fs::read_dir(&base) else {
                continue;
            };
            let mut count = 0;
            for entry in entries.flatten() {
                if count >= MAX_GAMES {
                    break;
                }
                let directory = entry.path();
                if !directory.is_dir() || !looks_like_store_game(&directory) {
                    continue;
                }
                let Some((install, render)) = render_executable(&directory) else {
                    continue;
                };
                let Some(name) = directory
                    .file_name()
                    .map(|name| name.to_string_lossy().into_owned())
                else {
                    continue;
                };
                let Ok(key) = DeploymentKey::for_path(&directory) else {
                    continue;
                };
                if !seen.insert(key.clone()) {
                    continue;
                }
                let target = render
                    .path
                    .parent()
                    .map(Path::to_path_buf)
                    .unwrap_or_else(|| install.clone());
                let graphics_api = GraphicsApiDetector::detect(&render.path, &target);
                games.push(GameEntry {
                    id: format!("xbox:{key}"),
                    name,
                    source: LibrarySource::Xbox,
                    install_directory: install,
                    render_exe: Some(render.path),
                    graphics_api,
                    target_directory: target,
                    app_id: None,
                    is_manual: false,
                });
                count += 1;
            }
        }
        games
    }
}

fn looks_like_store_game(directory: &Path) -> bool {
    let content = directory.join("Content");
    content.is_dir()
        || directory.join("MicrosoftGame.config").is_file()
        || directory.join("AppxManifest.xml").is_file()
        || content.join("MicrosoftGame.config").is_file()
}

fn render_executable(directory: &Path) -> Option<(PathBuf, super::render_exe::RenderExe)> {
    let content = directory.join("Content");
    if content.is_dir()
        && let Some(render) = RenderExeSelector::select(&content)
    {
        return Some((content, render));
    }
    RenderExeSelector::select(directory).map(|render| (directory.to_path_buf(), render))
}
