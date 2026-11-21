use std::path::Path;

use fgm_domain::error::{AppError, ErrorCode};

use crate::deployment::{BackupStore, DeploymentEngine, RestoreReport};
use crate::hashing::FileHasher;
use crate::paths::{AppPaths, TargetPath};
use crate::reframework::ReframeworkService;

use super::dlss5_config::Dlss5Config;
use super::dlss5_hotkey_queue::HotkeyQueue;
use super::dlss5_service::Dlss5Service;

impl Dlss5Service {
    /// Removes the DLSS5 pack while keeping the ReShade proxy and INI, so a
    /// user-installed ReShade stays usable.
    pub fn restore_keeping_reshade(
        target_directory: &Path,
        paths: &AppPaths,
    ) -> Result<RestoreReport, AppError> {
        DeploymentEngine::restore_keeping(
            Self::FAMILY,
            target_directory,
            &paths.backups(),
            &paths.state().join("locks"),
            &["proxy", "config"],
        )
    }

    /// Restores everything the install wrote, including the ReShade proxy and
    /// the default `ReShade.ini`. Files that were present before the install
    /// are restored from backup; files changed by the user are reported as
    /// conflicts and left untouched, so a third-party module that reused the
    /// proxy slot is never deleted.
    pub fn uninstall(target_directory: &Path, paths: &AppPaths) -> Result<RestoreReport, AppError> {
        let store = BackupStore::new(paths.backups());
        let previous = store.load_manifest(Self::FAMILY, target_directory)?;
        let vulkan = previous.as_ref().is_some_and(|manifest| {
            manifest
                .options
                .get("api")
                .is_some_and(|api| api == "vulkan")
        });
        let pack_directory = previous
            .as_ref()
            .and_then(|manifest| manifest.options.get("packDir"))
            .map(std::path::PathBuf::from)
            .filter(|path| path.is_dir())
            .or_else(|| crate::packs::PackLocator::find(Self::PACK_ID));
        let game_exe = previous
            .as_ref()
            .and_then(|manifest| manifest.options.get("gameExe"))
            .map(std::path::PathBuf::from);
        let mut report = DeploymentEngine::restore(
            Self::FAMILY,
            target_directory,
            &paths.backups(),
            &paths.state().join("locks"),
        )?;
        let owned_ini = previous.as_ref().is_some_and(|manifest| {
            manifest.files.iter().any(|file| {
                file.relative_path.eq_ignore_ascii_case("ReShade.ini") && !file.existed_before
            })
        });
        if owned_ini {
            for name in ["ReShade.ini", "ReShade.log", "ReShadePreset.ini"] {
                let _ = std::fs::remove_file(target_directory.join(name));
            }
        }
        if vulkan {
            match (pack_directory, game_exe) {
                (Some(pack_directory), Some(game_exe)) => {
                    if let Err(error) =
                        super::dlss5_vulkan::uninstall_vulkan_layer(&pack_directory, &game_exe)
                    {
                        report.warnings.push(format!(
                            "vulkan layer: {}",
                            super::dlss5_service::first_reason(&error)
                        ));
                    }
                }
                _ => report.warnings.push(
                    "vulkan layer: the original pack or game executable is unknown".to_string(),
                ),
            }
            for name in ["ReShade.ini", "ReShade.log", "ReShadePreset.ini"] {
                let _ = std::fs::remove_file(target_directory.join(name));
            }
        }
        ReframeworkService::uninstall(target_directory, paths)?;
        Ok(report)
    }

    /// Retries queued hotkey writes; returns how many were applied.
    pub fn process_hotkey_queue(paths: &AppPaths) -> Result<usize, AppError> {
        let entries = HotkeyQueue::load(paths)?;
        let mut remaining = Vec::new();
        let mut applied = 0;
        for entry in entries {
            let exe = entry.exe_path.as_deref().map(Path::new);
            if exe.is_some_and(crate::process::GameProcessProbe::is_running) {
                remaining.push(entry);
                continue;
            }
            match Self::apply_hotkey_now(Path::new(&entry.target_dir), entry.virtual_key, paths) {
                Ok(()) => applied += 1,
                Err(error) if is_locked(&error) => remaining.push(entry),
                Err(_) => {}
            }
        }
        HotkeyQueue::save(paths, &remaining)?;
        Ok(applied)
    }

    pub(super) fn apply_hotkey_now(
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

pub(super) fn is_locked(error: &AppError) -> bool {
    matches!(error.code, ErrorCode::FileLocked | ErrorCode::AccessDenied)
}
