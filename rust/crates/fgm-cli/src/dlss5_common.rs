use std::path::{Path, PathBuf};

use fgm_domain::error::{AppError, ErrorCode};
use fgm_domain::library::GraphicsApi;
use fgm_infra::packs::PackLocator;
use fgm_infra::paths::AppPaths;

pub(crate) const PACK_ID: &str = "dlss5.reshade-gitc";

pub(crate) fn parse_api(value: &str) -> Option<GraphicsApi> {
    match value.to_lowercase().as_str() {
        "dx12" | "d3d12" => Some(GraphicsApi::Dx12),
        "dx11" | "d3d11" => Some(GraphicsApi::Dx11),
        "dx10" | "d3d10" => Some(GraphicsApi::Dx10),
        "dx9" | "d3d9" => Some(GraphicsApi::Dx9),
        "vulkan" => Some(GraphicsApi::Vulkan),
        "opengl" => Some(GraphicsApi::OpenGl),
        _ => None,
    }
}

pub(crate) fn data_paths(directory: Option<&str>) -> Result<AppPaths, AppError> {
    match directory {
        Some(directory) => AppPaths::new(PathBuf::from(directory), false),
        None => AppPaths::create_default(None),
    }
}

pub(crate) fn absolute(path: &Path) -> Result<PathBuf, AppError> {
    std::path::absolute(path).map_err(|error| {
        AppError::new(ErrorCode::InvalidPath)
            .with("path", path.display().to_string())
            .with("reason", error.to_string())
    })
}

pub(crate) fn next_value(args: &[String], index: &mut usize) -> Option<String> {
    *index += 1;
    match args.get(*index) {
        Some(value) if !value.starts_with('-') => Some(value.clone()),
        _ => {
            eprintln!("option requires a value");
            None
        }
    }
}

pub(crate) fn print_error(error: &AppError) -> i32 {
    let detail = error
        .context
        .iter()
        .find(|(key, _)| key == "reason")
        .map_or("operation failed", |(_, value)| value.as_str());
    eprintln!("{:?}: {detail}", error.code);
    1
}

pub(crate) fn resolve_pack(directory: Option<&str>) -> Result<PathBuf, AppError> {
    if let Some(directory) = directory {
        return absolute(Path::new(directory));
    }
    PackLocator::find(PACK_ID).ok_or_else(|| {
        AppError::new(ErrorCode::PackNotFound).with("reason", format!("{PACK_ID} was not found"))
    })
}

pub(crate) fn print_dlss5_usage() {
    eprintln!("usage:");
    eprintln!(
        "  fgm-next dlss5 install <target-dir> <game-exe> [--api <api>] [--pack-dir <dir>] [--data-dir <dir>]"
    );
    eprintln!("  fgm-next dlss5 inspect <target-dir> [--pack-dir <dir>] [--data-dir <dir>]");
    eprintln!("  fgm-next dlss5 restore <target-dir> [--data-dir <dir>]");
    eprintln!("  fgm-next dlss5 uninstall <target-dir> [--data-dir <dir>]");
    eprintln!("  fgm-next dlss5 hotkey <target-dir> <virtual-key> [--data-dir <dir>]");
}
