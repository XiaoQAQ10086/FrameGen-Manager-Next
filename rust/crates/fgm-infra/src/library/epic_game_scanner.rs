use std::collections::HashSet;
use std::path::{Path, PathBuf};

use fgm_domain::library::{GameEntry, GraphicsApi, LibrarySource};
use serde_json::Value;

use crate::fs::DeploymentKey;

use super::graphics_api_detector::GraphicsApiDetector;
use super::render_exe_selector::RenderExeSelector;

/// Reads Epic Games Launcher `.item` manifests.
pub struct EpicGameScanner;

impl EpicGameScanner {
    pub fn scan(manifest_directory: &Path) -> Vec<GameEntry> {
        let Ok(entries) = std::fs::read_dir(manifest_directory) else {
            return Vec::new();
        };

        let mut games = Vec::new();
        let mut seen = HashSet::new();

        for entry in entries.flatten() {
            let path = entry.path();
            if path
                .extension()
                .is_none_or(|extension| !extension.eq_ignore_ascii_case("item"))
            {
                continue;
            }
            let Ok(text) = std::fs::read_to_string(&path) else {
                continue;
            };
            let Ok(manifest) = serde_json::from_str::<Value>(&text) else {
                continue;
            };
            if manifest
                .get("bIsApplication")
                .and_then(Value::as_bool)
                .is_some_and(|value| !value)
            {
                continue;
            }

            let Some(install_directory) = manifest
                .get("InstallLocation")
                .and_then(Value::as_str)
                .map(PathBuf::from)
            else {
                continue;
            };
            if !install_directory.is_dir() {
                continue;
            }
            let key = super::path_key(&install_directory);
            if !seen.insert(key) {
                continue;
            }

            let name = manifest
                .get("DisplayName")
                .and_then(Value::as_str)
                .unwrap_or("Epic Game")
                .to_string();
            let app_id = manifest
                .get("AppName")
                .and_then(Value::as_str)
                .map(ToString::to_string);
            let render = RenderExeSelector::select(&install_directory);
            let target_directory = render
                .as_ref()
                .and_then(|candidate| candidate.path.parent())
                .map_or_else(|| install_directory.clone(), Path::to_path_buf);
            let graphics_api = render.as_ref().map_or(GraphicsApi::Unknown, |candidate| {
                GraphicsApiDetector::detect(&candidate.path, &install_directory)
            });
            let Ok(key) = DeploymentKey::for_path(&install_directory) else {
                continue;
            };
            let id = app_id
                .as_ref()
                .map_or_else(|| format!("epic:{key}"), |app| format!("epic:{app}"));

            games.push(GameEntry {
                id,
                name,
                source: LibrarySource::Epic,
                install_directory,
                render_exe: render.map(|candidate| candidate.path),
                target_directory,
                graphics_api,
                app_id,
                is_manual: false,
            });
        }

        games
    }
}
