use std::path::Path;

use fgm_domain::error::{AppError, ErrorCode};
use fgm_domain::ini::IniDocument;

use crate::fs::AtomicFile;

use super::reshade_installer::ReShadeInstaller;

const EFFECT_SEARCH_PATH: &str = r".\reshade-shaders\Shaders\**";
const TEXTURE_SEARCH_PATH: &str = r".\reshade-shaders\Textures\**";
const LINE_ENDING: &str = "\r\n";

/// ReShade.ini editing that preserves unrelated settings.
pub struct Dlss5Config;

impl Dlss5Config {
    pub const DEFAULT_HOTKEY_VK: u32 = 36;

    /// Merges the pack shader and texture search paths into an existing INI.
    pub fn merge(text: &str) -> String {
        let mut document = IniDocument::parse(text);
        merge_path(&mut document, "EffectSearchPaths", EFFECT_SEARCH_PATH);
        merge_path(&mut document, "TextureSearchPaths", TEXTURE_SEARCH_PATH);
        document.to_text(LINE_ENDING)
    }

    /// Sets `[INPUT] KeyOverlay` to the recorded virtual key.
    pub fn with_hotkey(text: &str, virtual_key: u32) -> String {
        let mut document = IniDocument::parse(text);
        document.set_value("INPUT", "KeyOverlay", &format!("{virtual_key},0,0,0"));
        document.to_text(LINE_ENDING)
    }

    /// Reads the INI, applies the search paths and writes it atomically.
    pub fn apply_search_paths(ini_path: &Path) -> Result<(), AppError> {
        let text = read_or_default(ini_path)?;
        AtomicFile::write_text(ini_path, &Self::merge(&text))
    }

    pub fn set_hotkey(ini_path: &Path, virtual_key: u32) -> Result<(), AppError> {
        if virtual_key == 0 || virtual_key > 0xFF {
            return Err(AppError::new(ErrorCode::InvalidArgument)
                .with("reason", "a virtual key between 1 and 255 is required"));
        }
        let text = read_or_default(ini_path)?;
        AtomicFile::write_text(ini_path, &Self::with_hotkey(&text, virtual_key))
    }
}

fn merge_path(document: &mut IniDocument, key: &str, path: &str) {
    let current = document
        .get_value("GENERAL", key)
        .unwrap_or_default()
        .trim()
        .to_string();
    if current
        .split(',')
        .any(|entry| entry.trim().eq_ignore_ascii_case(path))
    {
        return;
    }
    let merged = if current.is_empty() {
        path.to_string()
    } else {
        format!("{current},{path}")
    };
    document.set_value("GENERAL", key, &merged);
}

fn read_or_default(ini_path: &Path) -> Result<String, AppError> {
    match std::fs::read_to_string(ini_path) {
        Ok(text) => Ok(text),
        Err(error) if error.kind() == std::io::ErrorKind::NotFound => {
            Ok(ReShadeInstaller::default_ini().to_string())
        }
        Err(error) => Err(AppError::new(ErrorCode::FileLocked)
            .with("path", ini_path.display().to_string())
            .with("reason", error.to_string())),
    }
}
