use std::collections::BTreeMap;
use std::path::Path;
use std::sync::atomic::AtomicBool;

use fgm_domain::error::{AppError, ErrorCode};
use fgm_domain::ini::IniDocument;
use fgm_domain::library::GraphicsApi;
use fgm_domain::pack::{AssetPack, AssetPackStep};

use crate::deployment::{
    DeploymentEngine, DeploymentFile, DeploymentInspector, DeploymentProgress,
    PackDeploymentPlanner,
};
use crate::hashing::{FileHasher, sha256_hex};
use crate::pack_json::PackJson;
use crate::paths::{AppPaths, TargetPath};
use crate::reframework::ReframeworkService;

use super::dlss5_config::Dlss5Config;
use super::dlss5_hotkey_outcome::HotkeyOutcome;
use super::dlss5_hotkey_queue::HotkeyQueue;
use super::dlss5_inspection::Dlss5Inspection;
use super::dlss5_report::Dlss5Report;
use super::reshade_installer::ReShadeInstaller;

const PROXY_CANDIDATES: [&str; 7] = [
    "dxgi.dll",
    "d3d9.dll",
    "d3d10.dll",
    "d3d11.dll",
    "d3d12.dll",
    "opengl32.dll",
    "vulkan-1.dll",
];

/// DLSS5 pack deployment plus offline ReShade proxy installation.
pub struct Dlss5Service;

impl Dlss5Service {
    pub const FAMILY: &'static str = "dlss5";
    pub const PACK_ID: &'static str = "dlss5.reshade-gitc";

    pub fn proxy_for(api: GraphicsApi) -> Result<&'static str, AppError> {
        match api {
            GraphicsApi::Dx12 | GraphicsApi::Dx11 | GraphicsApi::Dx10 => Ok("dxgi.dll"),
            GraphicsApi::Dx9 => Ok("d3d9.dll"),
            // Vulkan uses a global ReShade layer instead of a proxy DLL.
            GraphicsApi::Vulkan => Err(AppError::new(ErrorCode::InvalidArgument).with(
                "reason",
                "the Vulkan ReShade layer is installed by the official setup",
            )),
            GraphicsApi::OpenGl => Ok("opengl32.dll"),
            GraphicsApi::Unknown => Err(AppError::new(ErrorCode::InvalidArgument)
                .with("reason", "the graphics API could not be determined")),
        }
    }

    #[allow(clippy::too_many_arguments)]
    pub fn install(
        pack_directory: &Path,
        game_exe: &Path,
        target_directory: &Path,
        api: GraphicsApi,
        app_version: &str,
        paths: &AppPaths,
        cancelled: &AtomicBool,
        progress: Option<&dyn DeploymentProgress>,
    ) -> Result<Dlss5Report, AppError> {
        let pack = PackJson::load(&pack_directory.join("pack.json"))?;
        let legacy = legacy_residue(target_directory);
        if !legacy.is_empty() {
            return Err(AppError::new(ErrorCode::Conflict)
                .with("reason", "legacy DLSS5/RenoDX files must be cleaned first")
                .with("files", legacy.join(",")));
        }
        ReframeworkService::install_before_deploy(game_exe, target_directory, app_version, paths)?;
        let proxy_module = if api == GraphicsApi::Vulkan {
            None
        } else {
            Some(Self::proxy_for(api)?)
        };
        let setup_source = installer_source(&pack.steps).ok_or_else(|| {
            AppError::new(ErrorCode::PackInvalid)
                .with("reason", "the DLSS5 pack has no runInstaller step")
        })?;
        let setup_path = TargetPath::combine(pack_directory, &setup_source)?;
        let module = match proxy_module {
            Some(_) => Some(ReShadeInstaller::extract_module(&setup_path, game_exe)?),
            None => {
                super::dlss5_vulkan::install_vulkan_layer(&setup_path, game_exe, target_directory)?;
                None
            }
        };

        let plan = PackDeploymentPlanner::plan(
            &pack,
            pack_directory,
            target_directory,
            &BTreeMap::new(),
            &[],
            Some(app_version),
        )?;

        let mut files = plan.files;
        if let Some(proxy_module) = proxy_module {
            let module = module.as_deref().unwrap_or_default();
            let proxy_path = TargetPath::combine(target_directory, proxy_module)?;
            if proxy_path.is_file() && !ReShadeInstaller::is_reshade_module(&proxy_path) {
                let module_hash = sha256_hex(module);
                let same_module = FileHasher::sha256_file(&proxy_path)
                    .is_ok_and(|hash| hash.eq_ignore_ascii_case(&module_hash));
                if !same_module {
                    return Err(AppError::new(ErrorCode::Conflict)
                        .with("path", proxy_path.display().to_string())
                        .with("reason", "the target module belongs to another program"));
                }
            }
            files.push(DeploymentFile {
                relative_path: proxy_module.to_string(),
                source: None,
                content: Some(module.to_vec()),
                sha256: sha256_hex(module),
                size: module.len() as i64,
                role: "proxy".to_string(),
            });

            let ini_path = TargetPath::combine(target_directory, "ReShade.ini")?;
            let ini_text = if ini_path.is_file() {
                std::fs::read_to_string(&ini_path).map_err(|error| {
                    AppError::new(ErrorCode::FileLocked)
                        .with("path", ini_path.display().to_string())
                        .with("reason", error.to_string())
                })?
            } else {
                ReShadeInstaller::default_ini().to_string()
            };
            let content = Dlss5Config::merge(&ini_text).into_bytes();
            files.push(DeploymentFile {
                relative_path: "ReShade.ini".to_string(),
                source: None,
                content: Some(content.clone()),
                sha256: sha256_hex(&content),
                size: content.len() as i64,
                role: "config".to_string(),
            });
        }

        let mut options = BTreeMap::new();
        if let Some(proxy_module) = proxy_module {
            options.insert("proxySlot".to_string(), proxy_module.to_string());
        }
        options.insert("api".to_string(), api_key(api).to_string());
        options.insert("packDir".to_string(), pack_directory.display().to_string());
        options.insert("gameExe".to_string(), game_exe.display().to_string());

        let report = DeploymentEngine::apply(
            &files,
            target_directory,
            Self::FAMILY,
            &pack.id,
            &pack.version,
            &options,
            &pack.owned_directories,
            Some(game_exe),
            &paths.backups(),
            &paths.state().join("locks"),
            cancelled,
            progress,
        )?;

        Ok(Dlss5Report {
            files_written: report.files_written,
            proxy_module: proxy_module.unwrap_or("vulkan").to_string(),
        })
    }

    /// Reports pack state plus ReShade presence and legacy RenoDX residue.
    pub fn inspect(
        target_directory: &Path,
        pack: Option<&AssetPack>,
        paths: &AppPaths,
    ) -> Result<Dlss5Inspection, AppError> {
        let inspection =
            DeploymentInspector::inspect(Self::FAMILY, target_directory, &paths.backups(), pack)?;
        let proxy_module = inspection
            .options
            .as_ref()
            .and_then(|options| options.get("proxySlot").cloned());
        let tracked_proxy_present = proxy_module.as_deref().is_some_and(|name| {
            TargetPath::combine(target_directory, name).is_ok_and(|path| path.is_file())
        });
        let vulkan = inspection
            .options
            .as_ref()
            .and_then(|options| options.get("api"))
            .is_some_and(|api| api == "vulkan");
        let re_shade_present = tracked_proxy_present
            || PROXY_CANDIDATES.iter().any(|name| {
                let path = target_directory.join(name);
                path.is_file() && ReShadeInstaller::is_reshade_module(&path)
            })
            || (vulkan
                && (target_directory.join("ReShade.ini").is_file()
                    || super::dlss5_vulkan::vulkan_layer_present()));

        Ok(Dlss5Inspection {
            state: inspection.state,
            has_manifest: inspection.has_manifest,
            pack_version: inspection.pack_version,
            proxy_module,
            re_shade_present,
            hotkey_vk: read_hotkey(target_directory),
            legacy_files: legacy_residue(target_directory),
            details: inspection.details,
        })
    }

    /// Deletes legacy RenoDX/DLSS5 files that this app never deployed.
    pub fn clean_legacy(target_directory: &Path) -> Result<usize, AppError> {
        let mut removed = 0;
        for name in legacy_residue(target_directory) {
            let path = TargetPath::combine(target_directory, &name)?;
            if path.is_file() {
                std::fs::remove_file(&path).map_err(|error| {
                    AppError::new(ErrorCode::AccessDenied)
                        .with("path", path.display().to_string())
                        .with("reason", error.to_string())
                })?;
                removed += 1;
            }
        }
        Ok(removed)
    }

    /// Writes `[INPUT] KeyOverlay` for an installed game. When the game is
    /// running or the INI is locked the request is queued instead of failing.
    pub fn set_hotkey(
        target_directory: &Path,
        virtual_key: u32,
        game_exe: Option<&Path>,
        paths: &AppPaths,
    ) -> Result<HotkeyOutcome, AppError> {
        if game_exe.is_some_and(crate::process::GameProcessProbe::is_running) {
            HotkeyQueue::enqueue(paths, target_directory, game_exe, virtual_key)?;
            return Ok(HotkeyOutcome::Queued);
        }
        match Self::apply_hotkey_now(target_directory, virtual_key, paths) {
            Ok(()) => Ok(HotkeyOutcome::Applied),
            Err(error) if super::dlss5_lifecycle::is_locked(&error) => {
                HotkeyQueue::enqueue(paths, target_directory, game_exe, virtual_key)?;
                Ok(HotkeyOutcome::Queued)
            }
            Err(error) => Err(error),
        }
    }
}

fn api_key(api: GraphicsApi) -> &'static str {
    match api {
        GraphicsApi::Dx12 => "dx12",
        GraphicsApi::Dx11 => "dx11",
        GraphicsApi::Dx10 => "dx10",
        GraphicsApi::Dx9 => "dx9",
        GraphicsApi::Vulkan => "vulkan",
        GraphicsApi::OpenGl => "opengl",
        GraphicsApi::Unknown => "unknown",
    }
}

pub(super) fn first_reason(error: &AppError) -> &str {
    error
        .context
        .iter()
        .find(|(key, _)| key == "reason")
        .map_or("operation failed", |(_, value)| value.as_str())
}

fn read_hotkey(target_directory: &Path) -> Option<u32> {
    let text = std::fs::read_to_string(target_directory.join("ReShade.ini")).ok()?;
    let document = IniDocument::parse(&text);
    document
        .get_value("INPUT", "KeyOverlay")?
        .split(',')
        .next()?
        .trim()
        .parse()
        .ok()
}

fn legacy_residue(target_directory: &Path) -> Vec<String> {
    let Ok(entries) = std::fs::read_dir(target_directory) else {
        return Vec::new();
    };
    let mut names: Vec<String> = entries
        .flatten()
        .filter(|entry| entry.path().is_file())
        .filter_map(|entry| entry.file_name().into_string().ok())
        .filter(|name| {
            let lower = name.to_lowercase();
            lower.starts_with("renodx-dlss") || lower == "nvngx_dlssnr.dll"
        })
        .collect();
    names.sort_by_key(|name| name.to_lowercase());
    names
}

fn installer_source(steps: &[Option<AssetPackStep>]) -> Option<String> {
    for step in steps.iter().flatten() {
        if !step.kind.eq_ignore_ascii_case("runInstaller") {
            continue;
        }
        let Some(arguments) = step.arguments.as_ref() else {
            continue;
        };
        if let Some((_, value)) = arguments
            .iter()
            .find(|(key, _)| key.eq_ignore_ascii_case("source"))
        {
            return Some(value.clone());
        }
    }
    None
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn maps_apis_to_proxy_modules() {
        assert_eq!(
            Dlss5Service::proxy_for(GraphicsApi::Dx12).unwrap(),
            "dxgi.dll"
        );
        assert_eq!(
            Dlss5Service::proxy_for(GraphicsApi::Dx11).unwrap(),
            "dxgi.dll"
        );
        assert_eq!(
            Dlss5Service::proxy_for(GraphicsApi::Dx9).unwrap(),
            "d3d9.dll"
        );
        assert!(Dlss5Service::proxy_for(GraphicsApi::Vulkan).is_err());
        assert_eq!(
            Dlss5Service::proxy_for(GraphicsApi::OpenGl).unwrap(),
            "opengl32.dll"
        );
        assert!(Dlss5Service::proxy_for(GraphicsApi::Unknown).is_err());
    }
}
