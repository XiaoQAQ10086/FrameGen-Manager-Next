use std::path::Path;

use fgm_domain::error::{AppError, ErrorCode};
use fgm_domain::library::GraphicsApi;
use fgm_domain::pack::AssetPackStep;

use crate::deployment::{DeploymentEngine, PackDeploymentPlanner};
use crate::pack_json::PackJson;
use crate::paths::{AppPaths, TargetPath};

use super::dlss5_report::Dlss5Report;
use super::reshade_installer::ReShadeInstaller;

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
            &std::collections::BTreeMap::new(),
            &[],
            Some(app_version),
        )?;
        let report = DeploymentEngine::apply(
            &plan.files,
            target_directory,
            Self::FAMILY,
            &pack.id,
            &pack.version,
            &std::collections::BTreeMap::new(),
            &pack.owned_directories,
            Some(game_exe),
            &paths.backups(),
            &paths.state().join("locks"),
            &std::sync::atomic::AtomicBool::new(false),
        )?;

        if let Err(error) =
            ReShadeInstaller::install_module(target_directory, proxy_module, &module)
        {
            let _ = DeploymentEngine::restore(
                Self::FAMILY,
                target_directory,
                &paths.backups(),
                &paths.state().join("locks"),
            );
            return Err(error.with("rollback", "components"));
        }

        Ok(Dlss5Report {
            files_written: report.files_written,
            proxy_module: proxy_module.to_string(),
        })
    }
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
