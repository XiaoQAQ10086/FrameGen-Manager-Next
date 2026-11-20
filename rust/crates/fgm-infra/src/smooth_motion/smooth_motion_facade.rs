use std::path::Path;

use crate::paths::AppPaths;
use fgm_domain::error::AppError;
use fgm_domain::gpu::GpuRoute;

use super::smooth_motion_change::SmoothMotionChange;
use super::smooth_motion_components::SmoothMotionComponents;
use super::smooth_motion_service::SmoothMotionService;

/// Combines component deployment with the NVAPI driver toggle.
pub struct SmoothMotionFacade;

impl SmoothMotionFacade {
    #[allow(clippy::too_many_arguments)]
    pub fn enable(
        route: GpuRoute,
        _driver_supported: bool,
        game: &str,
        executable: &Path,
        target_directory: &Path,
        app_version: &str,
        paths: &AppPaths,
    ) -> Result<SmoothMotionChange, AppError> {
        SmoothMotionComponents::deploy(
            route,
            target_directory,
            Some(executable),
            app_version,
            paths,
        )?;

        match SmoothMotionService::set(game, executable, true, paths.root()) {
            Ok(change) => Ok(change),
            Err(error) => {
                let _ = SmoothMotionComponents::restore(target_directory, paths);
                Err(error.with("rollback", "components"))
            }
        }
    }

    pub fn disable(
        game: &str,
        executable: &Path,
        target_directory: &Path,
        paths: &AppPaths,
    ) -> Result<SmoothMotionChange, AppError> {
        let change = SmoothMotionService::set(game, executable, false, paths.root())?;
        SmoothMotionComponents::restore(target_directory, paths)?;
        Ok(change)
    }

    pub fn restore(
        game: &str,
        executable: &Path,
        target_directory: &Path,
        paths: &AppPaths,
    ) -> Result<SmoothMotionChange, AppError> {
        let change = SmoothMotionService::restore(game, executable, paths.root())?;
        SmoothMotionComponents::restore(target_directory, paths)?;
        Ok(change)
    }
}
