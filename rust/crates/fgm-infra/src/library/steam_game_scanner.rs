use std::collections::HashSet;
use std::path::{Path, PathBuf};

use fgm_domain::library::{GameEntry, GraphicsApi, LibrarySource};

use super::graphics_api_detector::GraphicsApiDetector;
use super::render_exe_selector::RenderExeSelector;
use super::valve_key_values::{ValveDocument, ValveValue};

const JUNK_NAMES: [&str; 7] = [
    "steamworks common redistributables",
    "steamvr",
    "steamvr beta",
    "proton experimental",
    "proton hotfix",
    "proton - experimental",
    "steam linux runtime",
];

/// Reads Steam library folders and `appmanifest_*.acf` files.
pub struct SteamGameScanner;

impl SteamGameScanner {
    pub fn scan(steam_root: &Path) -> Vec<GameEntry> {
        let mut libraries = vec![steam_root.to_path_buf()];
        let library_file = steam_root.join("steamapps").join("libraryfolders.vdf");
        if let Ok(text) = std::fs::read_to_string(&library_file)
            && let Some(document) = ValveDocument::parse(&text)
        {
            for path in document.all_strings("path") {
                let path = PathBuf::from(path);
                if path.is_dir() {
                    libraries.push(path);
                }
            }
        }

        let mut games = Vec::new();
        let mut seen = HashSet::new();
        for library in libraries {
            let steamapps = library.join("steamapps");
            let Ok(entries) = std::fs::read_dir(&steamapps) else {
                continue;
            };
            for entry in entries.flatten() {
                let path = entry.path();
                let file_name = path
                    .file_name()
                    .map_or_else(String::new, |name| name.to_string_lossy().to_lowercase());
                if !(file_name.starts_with("appmanifest_") && file_name.ends_with(".acf")) {
                    continue;
                }
                let Some(game) = read_manifest(&path, &steamapps) else {
                    continue;
                };
                let key = game.install_directory.to_string_lossy().to_lowercase();
                if seen.insert(key) {
                    games.push(game);
                }
            }
        }

        games
    }
}

fn read_manifest(manifest_path: &Path, steamapps: &Path) -> Option<GameEntry> {
    let text = std::fs::read_to_string(manifest_path).ok()?;
    let document = ValveDocument::parse(&text)?;
    let app_state = document.object("AppState")?;

    let app_id = object_string(app_state, "appid")?;
    let name = object_string(app_state, "name")?;
    let install_dir = object_string(app_state, "installdir")?;
    if is_junk(&name) {
        return None;
    }

    let install_directory = steamapps.join("common").join(install_dir);
    if !install_directory.is_dir() {
        return None;
    }

    let render = RenderExeSelector::select(&install_directory);
    let target_directory = render
        .as_ref()
        .and_then(|candidate| candidate.path.parent())
        .map_or_else(|| install_directory.clone(), Path::to_path_buf);
    let graphics_api = render.as_ref().map_or(GraphicsApi::Unknown, |candidate| {
        GraphicsApiDetector::detect(&candidate.path, &install_directory)
    });

    Some(GameEntry {
        id: format!("steam:{app_id}"),
        name,
        source: LibrarySource::Steam,
        install_directory,
        render_exe: render.map(|candidate| candidate.path),
        target_directory,
        graphics_api,
        app_id: Some(app_id),
        is_manual: false,
    })
}

fn object_string(entries: &[(String, ValveValue)], key: &str) -> Option<String> {
    entries.iter().find_map(|(name, value)| {
        if !name.eq_ignore_ascii_case(key) {
            return None;
        }
        match value {
            ValveValue::String(text) => Some(text.clone()),
            ValveValue::Object(_) => None,
        }
    })
}

fn is_junk(name: &str) -> bool {
    let name = name.to_lowercase();
    JUNK_NAMES.contains(&name.as_str())
        || name.starts_with("steam linux runtime ")
        || (name.starts_with("proton ")
            && name
                .chars()
                .nth("proton ".len())
                .is_some_and(|character| character.is_ascii_digit()))
}
