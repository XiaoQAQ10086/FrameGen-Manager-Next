use std::path::Path;

use crate::deployment::{
    DeploymentEngine, DeploymentInspector, DeploymentReport, PackDeploymentPlanner, RestoreReport,
};
use crate::pack_json::PackJson;
use crate::packs::PackLocator;
use crate::paths::AppPaths;
use fgm_domain::error::{AppError, ErrorCode};
use fgm_domain::gpu::GpuRoute;

/// Deploys and restores the Smooth Motion component files through the shared engine.
pub struct SmoothMotionComponents;

impl SmoothMotionComponents {
    pub const FAMILY: &'static str = "smooth-motion";

    pub fn pack_id(route: GpuRoute) -> Option<&'static str> {
        match route {
            GpuRoute::Sm86 => Some("smooth-motion.rtx30"),
            GpuRoute::Sm75 => Some("smooth-motion.rtx20"),
            _ => None,
        }
    }

    pub fn deploy(
        route: GpuRoute,
        target_directory: &Path,
        game_exe: Option<&Path>,
        app_version: &str,
        paths: &AppPaths,
    ) -> Result<DeploymentReport, AppError> {
        let pack_id = Self::pack_id(route).ok_or_else(|| {
            AppError::new(ErrorCode::GpuUnsupported)
                .with("route", format!("{route:?}"))
                .with("reason", "Smooth Motion is not supported on this GPU route")
        })?;
        let pack_directory = PackLocator::find(pack_id)
            .ok_or_else(|| AppError::new(ErrorCode::PackNotFound).with("packId", pack_id))?;
        Self::deploy_from(
            &pack_directory,
            target_directory,
            game_exe,
            app_version,
            paths,
        )
    }

    pub fn deploy_from(
        pack_directory: &Path,
        target_directory: &Path,
        game_exe: Option<&Path>,
        app_version: &str,
        paths: &AppPaths,
    ) -> Result<DeploymentReport, AppError> {
        if let Ok(inspection) =
            DeploymentInspector::inspect("framegen", target_directory, &paths.backups(), None)
            && inspection.has_manifest
        {
            return Err(AppError::new(ErrorCode::Conflict).with(
                "reason",
                "frame generation is already deployed to this directory",
            ));
        }

        let pack = PackJson::load(&pack_directory.join("pack.json"))?;
        let plan = PackDeploymentPlanner::plan(
            &pack,
            pack_directory,
            target_directory,
            &std::collections::BTreeMap::new(),
            &[],
            Some(app_version),
        )?;

        DeploymentEngine::apply(
            &plan.files,
            target_directory,
            Self::FAMILY,
            &pack.id,
            &pack.version,
            &std::collections::BTreeMap::new(),
            &pack.owned_directories,
            game_exe,
            &paths.backups(),
            &paths.state().join("locks"),
            &std::sync::atomic::AtomicBool::new(false),
        )
    }

    pub fn restore(target_directory: &Path, paths: &AppPaths) -> Result<RestoreReport, AppError> {
        DeploymentEngine::restore(
            Self::FAMILY,
            target_directory,
            &paths.backups(),
            &paths.state().join("locks"),
        )
    }
}
