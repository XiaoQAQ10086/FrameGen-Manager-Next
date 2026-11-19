use std::path::PathBuf;

use crate::platform::registry::{HKEY_CURRENT_USER, HKEY_LOCAL_MACHINE, read_string};

/// Locates the Steam installation directory from the registry and common paths.
pub struct SteamLocator;

impl SteamLocator {
    pub fn locate() -> Option<PathBuf> {
        let mut candidates = Vec::new();

        if let Some(path) = read_string(HKEY_CURRENT_USER, "Software\\Valve\\Steam", "SteamPath") {
            candidates.push(PathBuf::from(path));
        }
        if let Some(path) = read_string(
            HKEY_LOCAL_MACHINE,
            "SOFTWARE\\WOW6432Node\\Valve\\Steam",
            "InstallPath",
        ) {
            candidates.push(PathBuf::from(path));
        }
        if let Some(path) = read_string(HKEY_LOCAL_MACHINE, "SOFTWARE\\Valve\\Steam", "InstallPath")
        {
            candidates.push(PathBuf::from(path));
        }

        for variable in ["ProgramFiles(x86)", "ProgramFiles"] {
            if let Some(base) = std::env::var_os(variable) {
                candidates.push(PathBuf::from(base).join("Steam"));
            }
        }

        candidates
            .into_iter()
            .find(|path| path.join("steamapps").is_dir() || path.join("steam.exe").is_file())
    }
}
