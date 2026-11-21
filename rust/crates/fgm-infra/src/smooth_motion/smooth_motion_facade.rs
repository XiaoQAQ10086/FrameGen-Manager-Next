use std::path::Path;

use crate::paths::AppPaths;
use fgm_domain::error::{AppError, ErrorCode};
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
        driver_supported: bool,
        game: &str,
        executable: &Path,
        target_directory: &Path,
        app_version: &str,
        paths: &AppPaths,
    ) -> Result<SmoothMotionChange, AppError> {
        if !driver_supported {
            return Err(AppError::new(ErrorCode::GpuUnsupported).with("min", "616.64"));
        }
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
        remove_components(target_directory, paths)?;
        Ok(change)
    }

    pub fn restore(
        game: &str,
        executable: &Path,
        target_directory: &Path,
        paths: &AppPaths,
    ) -> Result<SmoothMotionChange, AppError> {
        let change = SmoothMotionService::restore(game, executable, paths.root())?;
        remove_components(target_directory, paths)?;
        Ok(change)
    }
}

/// Removes the deployed components, falling back to pack-hash matching.
///
/// Component files can have been deployed from a different data directory (a
/// portable build next to an installed one, or a wiped cache). The manifest
/// then lives elsewhere, so the files are matched against the pack instead. The
/// driver setting is global; refusing the whole toggle here would leave Smooth
/// Motion switched on with no way to switch it off.
fn remove_components(target_directory: &Path, paths: &AppPaths) -> Result<(), AppError> {
    let restored =
        tolerate_missing_manifest(SmoothMotionComponents::restore(target_directory, paths))?;
    if restored.is_none() {
        let _ = SmoothMotionComponents::restore_orphaned(target_directory);
    }
    Ok(())
}

/// Turns "this data directory has no component manifest" into "nothing to
/// remove".
///
/// Component files can have been deployed from a different data directory (a
/// portable build next to an installed one, or a wiped cache). The driver
/// setting is global, so a hard failure here would leave Smooth Motion switched
/// on with no way to switch it off.
fn tolerate_missing_manifest<T>(result: Result<T, AppError>) -> Result<Option<T>, AppError> {
    match result {
        Ok(value) => Ok(Some(value)),
        Err(error) if error.code == ErrorCode::ManifestMissing => Ok(None),
        Err(error) => Err(error),
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn a_missing_manifest_counts_as_nothing_to_restore() {
        let error = AppError::new(ErrorCode::ManifestMissing);
        assert!(
            tolerate_missing_manifest::<()>(Err(error))
                .unwrap()
                .is_none()
        );
    }

    #[test]
    fn other_failures_still_fail_the_toggle() {
        let error = AppError::new(ErrorCode::AccessDenied);
        assert_eq!(
            tolerate_missing_manifest::<()>(Err(error))
                .unwrap_err()
                .code,
            ErrorCode::AccessDenied
        );
    }
}
