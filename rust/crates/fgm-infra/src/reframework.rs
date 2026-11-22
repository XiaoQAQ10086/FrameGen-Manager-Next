use std::collections::BTreeMap;
use std::path::Path;
use std::sync::atomic::AtomicBool;

use fgm_domain::error::AppError;

use crate::deployment::{BackupStore, DeploymentEngine, DeploymentSummary, PackDeploymentPlanner};
use crate::library::{EngineDetector, GameEngine, LibraryService, path_key};
use crate::pack_json::PackJson;
use crate::packs::PackLocator;
use crate::paths::AppPaths;
use crate::settings::SettingsStore;

/// Optional REFramework deployment for RE Engine games.
pub struct ReframeworkService;

impl ReframeworkService {
    pub const FAMILY: &'static str = "reframework";
    pub const PACK_ID: &'static str = "reframework";

    /// Installs the bundled REFramework when the setting is on, the game is RE
    /// Engine and no `dinput8.dll` is already present. Returns whether files
    /// were written.
    pub fn install_before_deploy(
        game_exe: &Path,
        target_directory: &Path,
        app_version: &str,
        paths: &AppPaths,
        proxy_slot: Option<&str>,
    ) -> Result<bool, AppError> {
        let settings = SettingsStore::new(paths.config().join("app.json")).load()?;
        if !settings.auto_install_reframework {
            return Ok(false);
        }
        let install_directory = LibraryService::list(paths)
            .into_iter()
            .find(|game| path_key(&game.target_directory) == path_key(target_directory))
            .map(|game| game.install_directory);
        let roots = [
            target_directory,
            install_directory.as_deref().unwrap_or(target_directory),
        ];
        if EngineDetector::detect_with_roots(game_exe, &roots) != GameEngine::ReEngine {
            return Ok(false);
        }
        if proxy_slot.is_some_and(|slot| slot.eq_ignore_ascii_case("dinput8.dll")) {
            return Err(AppError::new(fgm_domain::error::ErrorCode::Conflict)
                .with("module", "dinput8.dll")
                .with(
                    "reason",
                    "REFramework will use dinput8.dll; choose another proxy",
                ));
        }
        if target_directory.join("reframework_revision.txt").is_file() {
            return Ok(false);
        }
        if DeploymentSummary::state("reframework", target_directory, &paths.backups(), None)?
            != fgm_domain::deployment::DeploymentState::NotDeployed
        {
            return Ok(false);
        }
        if target_directory.join("dinput8.dll").is_file() {
            return Ok(false);
        }
        let Some(pack_directory) = PackLocator::find(Self::PACK_ID) else {
            return Ok(false);
        };
        let pack = PackJson::load(&pack_directory.join("pack.json"))?;
        let options = BTreeMap::new();
        let plan = PackDeploymentPlanner::plan(
            &pack,
            &pack_directory,
            target_directory,
            &options,
            &[],
            Some(app_version),
        )?;
        DeploymentEngine::apply(
            &plan.files,
            target_directory,
            Self::FAMILY,
            &pack.id,
            &pack.version,
            &options,
            &pack.owned_directories,
            Some(game_exe),
            &paths.backups(),
            &paths.state().join("locks"),
            &AtomicBool::new(false),
            None,
        )?;
        Ok(true)
    }

    /// Removes only a REFramework deployment recorded by this app.
    pub fn uninstall(target_directory: &Path, paths: &AppPaths) -> Result<bool, AppError> {
        let store = BackupStore::new(paths.backups());
        if store
            .load_manifest(Self::FAMILY, target_directory)?
            .is_none()
        {
            return Ok(false);
        }
        DeploymentEngine::restore(
            Self::FAMILY,
            target_directory,
            &paths.backups(),
            &paths.state().join("locks"),
        )?;
        Ok(true)
    }
}
