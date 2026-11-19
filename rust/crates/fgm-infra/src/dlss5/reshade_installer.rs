use std::path::Path;

use fgm_domain::error::{AppError, ErrorCode};

use crate::fs::AtomicFile;
use crate::paths::TargetPath;
use crate::pe::PeImports;
use crate::platform::file_version::product_name;

use super::reshade_zip::extract_entry;

const DEFAULT_INI: &str = "[GENERAL]\r\n\
EffectSearchPaths=.\\reshade-shaders\\Shaders\\**\r\n\
TextureSearchPaths=.\\reshade-shaders\\Textures\\**\r\n\
PreprocessorDefinitions=RESHADE_DEPTH_LINEARIZATION_FAR_PLANE=1000.0,RESHADE_DEPTH_INPUT_IS_UPSIDE_DOWN=0,RESHADE_DEPTH_INPUT_IS_REVERSED=1,RESHADE_DEPTH_INPUT_IS_LOGARITHMIC=0\r\n\
\r\n\
[INPUT]\r\n\
GamepadNavigation=0\r\n\
KeyOverlay=36,0,0,0\r\n";

/// Extracts and installs the ReShade module without running the setup EXE.
pub struct ReShadeInstaller;

impl ReShadeInstaller {
    pub fn extract_module(setup_path: &Path, game_exe: &Path) -> Result<Vec<u8>, AppError> {
        let data = std::fs::read(setup_path).map_err(|error| {
            AppError::new(ErrorCode::FileNotFound)
                .with("path", setup_path.display().to_string())
                .with("reason", error.to_string())
        })?;
        let entry = if PeImports::is_64_bit(game_exe) {
            "ReShade64.dll"
        } else {
            "ReShade32.dll"
        };
        extract_entry(&data, entry).ok_or_else(|| {
            AppError::new(ErrorCode::PackNotFound)
                .with("path", setup_path.display().to_string())
                .with(
                    "reason",
                    format!("{entry} was not found in the setup executable"),
                )
        })
    }

    pub fn is_reshade_module(path: &Path) -> bool {
        path.is_file()
            && product_name(path).is_some_and(|name| name.to_lowercase().contains("reshade"))
    }

    pub fn default_ini() -> &'static str {
        DEFAULT_INI
    }

    pub fn install_module(
        target_directory: &Path,
        proxy_module: &str,
        module: &[u8],
    ) -> Result<(), AppError> {
        if proxy_module.trim().is_empty() {
            return Err(AppError::new(ErrorCode::InvalidArgument)
                .with("reason", "an offline ReShade module name is required"));
        }

        let target = TargetPath::combine(target_directory, proxy_module)?;
        if target.is_file() && !Self::is_reshade_module(&target) {
            return Err(AppError::new(ErrorCode::Conflict)
                .with("path", target.display().to_string())
                .with("reason", "the target module belongs to another program"));
        }

        AtomicFile::write(&target, module)?;
        let ini = TargetPath::combine(target_directory, "ReShade.ini")?;
        if !ini.is_file() {
            AtomicFile::write_text(&ini, DEFAULT_INI)?;
        }
        Ok(())
    }
}
