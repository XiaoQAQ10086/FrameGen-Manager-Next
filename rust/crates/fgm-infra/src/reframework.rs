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
use crate::updates::update_checker::is_newer;

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

    /// Replaces a REFramework deployment recorded by this app with the
    /// currently bundled nightly, preserving the original backup.
    pub fn upgrade(
        target_directory: &Path,
        app_version: &str,
        paths: &AppPaths,
    ) -> Result<bool, AppError> {
        let Some(pack_directory) = PackLocator::find(Self::PACK_ID) else {
            return Err(AppError::new(fgm_domain::error::ErrorCode::PackNotFound)
                .with("pack", Self::PACK_ID));
        };
        Self::upgrade_from(&pack_directory, target_directory, app_version, paths)
    }

    fn upgrade_from(
        pack_directory: &Path,
        target_directory: &Path,
        app_version: &str,
        paths: &AppPaths,
    ) -> Result<bool, AppError> {
        let store = BackupStore::new(paths.backups());
        let previous = store
            .load_manifest(Self::FAMILY, target_directory)?
            .ok_or_else(|| {
                AppError::new(fgm_domain::error::ErrorCode::ManifestMissing)
                    .with("family", Self::FAMILY)
            })?;
        let pack = PackJson::load(&pack_directory.join("pack.json"))?;
        if previous.pack_version.is_empty() || !is_newer(&pack.version, &previous.pack_version) {
            return Ok(false);
        }
        let options = BTreeMap::new();
        let plan = PackDeploymentPlanner::plan(
            &pack,
            pack_directory,
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
            None,
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

#[cfg(test)]
mod tests {
    use super::*;
    use sha2::{Digest, Sha256};
    use std::time::{SystemTime, UNIX_EPOCH};

    fn sha256(bytes: &[u8]) -> String {
        Sha256::digest(bytes)
            .iter()
            .map(|byte| format!("{byte:02x}"))
            .collect()
    }

    fn write_pack(directory: &Path, version: &str, payload: &[u8]) {
        std::fs::create_dir_all(directory).unwrap();
        std::fs::write(directory.join("dinput8.dll"), payload).unwrap();
        let manifest = format!(
            r#"{{
  "schemaVersion": 1,
  "id": "reframework",
  "version": "{version}",
  "displayName": "REFramework test",
  "minAppVersion": "2.0.0",
  "gpuRoutes": ["Any"],
  "files": [
    {{
      "source": "dinput8.dll",
      "target": "dinput8.dll",
      "role": "runtime",
      "required": true,
      "proxySlot": false,
      "sha256": "{}",
      "size": {}
    }}
  ],
  "trust": "bundled"
}}"#,
            sha256(payload),
            payload.len()
        );
        std::fs::write(directory.join("pack.json"), manifest).unwrap();
    }

    #[test]
    fn upgrade_replaces_files_and_keeps_the_original_backup() {
        let stamp = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .unwrap()
            .as_nanos();
        let root = std::env::temp_dir().join(format!("fgm-ref-upgrade-{stamp}"));
        let target = root.join("game");
        let pack = root.join("pack");
        let paths = AppPaths::new(root.join("data"), false).unwrap();
        std::fs::create_dir_all(&target).unwrap();
        let original = b"game-original";
        std::fs::write(target.join("dinput8.dll"), original).unwrap();

        write_pack(&pack, "1.0.0", b"ref-old");
        let old = PackJson::load(&pack.join("pack.json")).unwrap();
        let options = BTreeMap::new();
        let plan = PackDeploymentPlanner::plan(&old, &pack, &target, &options, &[], Some("2.3.6"))
            .unwrap();
        DeploymentEngine::apply(
            &plan.files,
            &target,
            ReframeworkService::FAMILY,
            &old.id,
            &old.version,
            &options,
            &old.owned_directories,
            None,
            &paths.backups(),
            &paths.state().join("locks"),
            &AtomicBool::new(false),
            None,
        )
        .unwrap();
        assert_eq!(
            std::fs::read(target.join("dinput8.dll")).unwrap(),
            b"ref-old"
        );

        write_pack(&pack, "1.1.0", b"ref-new");
        let upgraded = ReframeworkService::upgrade_from(&pack, &target, "2.3.6", &paths).unwrap();

        assert!(upgraded);
        assert_eq!(
            std::fs::read(target.join("dinput8.dll")).unwrap(),
            b"ref-new"
        );
        let store = BackupStore::new(paths.backups());
        let manifest = store
            .load_manifest(ReframeworkService::FAMILY, &target)
            .unwrap()
            .unwrap();
        assert_eq!(manifest.pack_version, "1.1.0");
        let record = &manifest.files[0];
        let backup = store
            .backup_file_path(
                &store
                    .directory(ReframeworkService::FAMILY, &target)
                    .unwrap(),
                record.backup_relative_path.as_deref().unwrap(),
            )
            .unwrap();
        assert_eq!(std::fs::read(backup).unwrap(), original);

        ReframeworkService::uninstall(&target, &paths).unwrap();
        assert_eq!(std::fs::read(target.join("dinput8.dll")).unwrap(), original);
        let _ = std::fs::remove_dir_all(root);
    }
}
