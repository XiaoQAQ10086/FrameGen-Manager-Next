use std::path::{Path, PathBuf};
use std::sync::atomic::AtomicBool;
use std::time::Duration;

use fgm_domain::error::{AppError, ErrorCode};

use crate::process::ProcessRunner;

pub(super) fn vulkan_layer_present() -> bool {
    std::env::var_os("ProgramData")
        .map(PathBuf::from)
        .is_some_and(|root| root.join("ReShade").join("ReShade64.json").is_file())
}

pub(super) fn install_vulkan_layer(
    setup_path: &Path,
    game_exe: &Path,
    target_directory: &Path,
) -> Result<(), AppError> {
    if !crate::platform::is_elevated() {
        return Err(AppError::new(ErrorCode::AccessDenied).with(
            "reason",
            "administrator rights are required for the Vulkan layer",
        ));
    }
    let arguments = vec![
        game_exe.display().to_string(),
        "--headless".to_string(),
        "--api".to_string(),
        "vulkan".to_string(),
    ];
    let result = ProcessRunner::run(
        setup_path,
        &arguments,
        Some(target_directory),
        Duration::from_secs(240),
        &AtomicBool::new(false),
    )?;
    if result.timed_out {
        return Err(AppError::new(ErrorCode::OperationTimedOut)
            .with("reason", "the ReShade setup did not finish"));
    }
    if result.exit_code != 0 {
        return Err(AppError::new(ErrorCode::Internal)
            .with("exitCode", result.exit_code.to_string())
            .with("reason", "the ReShade setup failed"));
    }
    Ok(())
}

pub(super) fn uninstall_vulkan_layer(
    pack_directory: &Path,
    game_exe: &Path,
) -> Result<(), AppError> {
    if !crate::platform::is_elevated() {
        return Err(AppError::new(ErrorCode::AccessDenied).with(
            "reason",
            "administrator rights are required for the Vulkan layer",
        ));
    }
    let setup_path = pack_setup_executable(pack_directory)?;
    let arguments = [
        game_exe.display().to_string(),
        "--headless".to_string(),
        "--elevated".to_string(),
        "--api".to_string(),
        "vulkan".to_string(),
        "--state".to_string(),
        "uninstall".to_string(),
    ];
    let result = ProcessRunner::run(
        &setup_path,
        &arguments,
        Some(pack_directory),
        Duration::from_secs(120),
        &AtomicBool::new(false),
    )?;
    if result.timed_out {
        return Err(AppError::new(ErrorCode::OperationTimedOut)
            .with("reason", "the ReShade uninstall did not finish"));
    }
    if result.exit_code != 0 {
        return Err(AppError::new(ErrorCode::Internal)
            .with("exitCode", result.exit_code.to_string())
            .with("reason", "the ReShade uninstall failed"));
    }
    Ok(())
}

pub(super) fn pack_setup_executable(pack_directory: &Path) -> Result<std::path::PathBuf, AppError> {
    let entries = std::fs::read_dir(pack_directory).map_err(|error| {
        AppError::new(ErrorCode::FileNotFound)
            .with("path", pack_directory.display().to_string())
            .with("reason", error.to_string())
    })?;
    entries
        .flatten()
        .map(|entry| entry.path())
        .find(|path| {
            path.file_name().is_some_and(|name| {
                let lower = name.to_string_lossy().to_lowercase();
                lower.starts_with("reshade_setup") && lower.ends_with(".exe")
            })
        })
        .ok_or_else(|| {
            AppError::new(ErrorCode::PackNotFound)
                .with("path", pack_directory.display().to_string())
        })
}
