use std::collections::BTreeMap;
use std::path::Path;

use fgm_domain::error::{AppError, ErrorCode};
use fgm_domain::ini::IniDocument;
use fgm_domain::library::GraphicsApi;
use fgm_domain::pack::{AssetPack, AssetPackStep};

use crate::deployment::{
    BackupStore, DeploymentEngine, DeploymentFile, DeploymentInspector, PackDeploymentPlanner,
    RestoreReport,
};
use crate::hashing::{FileHasher, sha256_hex};
use crate::pack_json::PackJson;
use crate::paths::{AppPaths, TargetPath};

use super::dlss5_config::Dlss5Config;
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

    pub fn proxy_for(api: GraphicsApi) -> Result<&'static str, AppError> {
        match api {
            GraphicsApi::Dx12 | GraphicsApi::Dx11 | GraphicsApi::Dx10 => Ok("dxgi.dll"),
            GraphicsApi::Dx9 => Ok("d3d9.dll"),
            GraphicsApi::Vulkan => Ok("vulkan-1.dll"),
            GraphicsApi::OpenGl => Ok("opengl32.dll"),
            GraphicsApi::Unknown => Err(AppError::new(ErrorCode::InvalidArgument)
                .with("reason", "the graphics API could not be determined")),
        }
    }

    pub fn install(
        pack_directory: &Path,
        game_exe: &Path,
        target_directory: &Path,
        api: GraphicsApi,
        app_version: &str,
        paths: &AppPaths,
    ) -> Result<Dlss5Report, AppError> {
        let pack = PackJson::load(&pack_directory.join("pack.json"))?;
        let proxy_module = Self::proxy_for(api)?;
        let setup_source = installer_source(&pack.steps).ok_or_else(|| {
            AppError::new(ErrorCode::PackInvalid)
                .with("reason", "the DLSS5 pack has no runInstaller step")
        })?;
        let setup_path = TargetPath::combine(pack_directory, &setup_source)?;
        let module = ReShadeInstaller::extract_module(&setup_path, game_exe)?;

        let plan = PackDeploymentPlanner::plan(
            &pack,
            pack_directory,
            target_directory,
            &BTreeMap::new(),
            &[],
            Some(app_version),
        )?;

        let proxy_path = TargetPath::combine(target_directory, proxy_module)?;
        if proxy_path.is_file() && !ReShadeInstaller::is_reshade_module(&proxy_path) {
            return Err(AppError::new(ErrorCode::Conflict)
                .with("path", proxy_path.display().to_string())
                .with("reason", "the target module belongs to another program"));
        }

        let mut files = plan.files;
        files.push(DeploymentFile {
            relative_path: proxy_module.to_string(),
            source: None,
            content: Some(module.clone()),
            sha256: sha256_hex(&module),
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

        let mut options = BTreeMap::new();
        options.insert("proxySlot".to_string(), proxy_module.to_string());

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
            &std::sync::atomic::AtomicBool::new(false),
        )?;

        Ok(Dlss5Report {
            files_written: report.files_written,
            proxy_module: proxy_module.to_string(),
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
        let re_shade_present = tracked_proxy_present
            || PROXY_CANDIDATES.iter().any(|name| {
                let path = target_directory.join(name);
                path.is_file() && ReShadeInstaller::is_reshade_module(&path)
            });

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

    /// Restores everything the install wrote, including the ReShade proxy and
    /// the default `ReShade.ini`. Files that were present before the install
    /// are restored from backup; files changed by the user are reported as
    /// conflicts and left untouched, so a third-party module that reused the
    /// proxy slot is never deleted.
    pub fn uninstall(target_directory: &Path, paths: &AppPaths) -> Result<RestoreReport, AppError> {
        DeploymentEngine::restore(
            Self::FAMILY,
            target_directory,
            &paths.backups(),
            &paths.state().join("locks"),
        )
    }

    /// Writes `[INPUT] KeyOverlay` for an installed game and re-syncs the
    /// manifest hash so the edit is not reported as a conflict.
    pub fn set_hotkey(
        target_directory: &Path,
        virtual_key: u32,
        paths: &AppPaths,
    ) -> Result<(), AppError> {
        let store = BackupStore::new(paths.backups());
        let Some(mut manifest) = store.load_manifest(Self::FAMILY, target_directory)? else {
            return Err(AppError::new(ErrorCode::ManifestMissing)
                .with("path", target_directory.display().to_string())
                .with("family", Self::FAMILY));
        };
        let ini_path = TargetPath::combine(target_directory, "ReShade.ini")?;
        Dlss5Config::set_hotkey(&ini_path, virtual_key)?;

        let size = std::fs::metadata(&ini_path)
            .map_err(|error| {
                AppError::new(ErrorCode::FileNotFound)
                    .with("path", ini_path.display().to_string())
                    .with("reason", error.to_string())
            })?
            .len() as i64;
        let hash = FileHasher::sha256_file(&ini_path)?;
        for record in &mut manifest.files {
            if record.relative_path.eq_ignore_ascii_case("ReShade.ini") {
                record.sha256 = hash.clone();
                record.size = size;
            }
        }
        store.save_manifest(&manifest)
    }
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
        assert_eq!(
            Dlss5Service::proxy_for(GraphicsApi::Vulkan).unwrap(),
            "vulkan-1.dll"
        );
        assert!(Dlss5Service::proxy_for(GraphicsApi::Unknown).is_err());
    }
}
