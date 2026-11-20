use std::path::Path;

use crate::deployment::{
    DeploymentEngine, DeploymentInspector, DeploymentReport, PackDeploymentPlanner, RestoreReport,
};
use crate::hashing::FileHasher;
use crate::pack_json::PackJson;
use crate::packs::PackLocator;
use crate::paths::{AppPaths, TargetPath};
use fgm_domain::deployment::DeploymentState;
use fgm_domain::error::{AppError, ErrorCode};
use fgm_domain::gpu::GpuRoute;

/// Deployment families that own a frame-generation install.
const FRAMEGEN_FAMILIES: [&str; 1] = ["framegen.sm86"];

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
        // Frame generation records its manifest under its pack id, so the guard
        // has to look there instead of the bare `framegen` name.
        for family in FRAMEGEN_FAMILIES {
            let Ok(inspection) =
                DeploymentInspector::inspect(family, target_directory, &paths.backups(), None)
            else {
                continue;
            };
            if inspection.has_manifest && inspection.state != DeploymentState::NotDeployed {
                return Err(AppError::new(ErrorCode::Conflict).with(
                    "reason",
                    "frame generation is already deployed to this directory",
                ));
            }
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
            None,
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

    /// Removes component files that are byte-identical to a Smooth Motion pack.
    ///
    /// Used when the manifest lives in another data directory (a portable build
    /// next to an installed one, or a wiped cache). Only files whose hash still
    /// matches the pack are deleted, so a DLL the user replaced is never
    /// touched.
    pub fn restore_orphaned(target_directory: &Path) -> Result<usize, AppError> {
        let mut removed = 0;
        for route in [GpuRoute::Sm86, GpuRoute::Sm75] {
            let Some(pack_id) = Self::pack_id(route) else {
                continue;
            };
            let Some(pack_directory) = PackLocator::find(pack_id) else {
                continue;
            };
            removed += Self::restore_orphaned_from(&pack_directory, target_directory)?;
        }
        Ok(removed)
    }

    pub fn restore_orphaned_from(
        pack_directory: &Path,
        target_directory: &Path,
    ) -> Result<usize, AppError> {
        let pack = PackJson::load(&pack_directory.join("pack.json"))?;
        let mut removed = 0;
        for file in pack.files.iter().flatten() {
            let Some(expected) = file.sha256.as_deref() else {
                continue;
            };
            let Ok(target) = TargetPath::combine(target_directory, &file.target) else {
                continue;
            };
            if !target.is_file() {
                continue;
            }
            let Ok(actual) = FileHasher::sha256_file(&target) else {
                continue;
            };
            if !actual.eq_ignore_ascii_case(expected) {
                continue;
            }
            if std::fs::remove_file(&target).is_ok() {
                removed += 1;
            }
        }
        Ok(removed)
    }
}
