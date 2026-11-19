use std::path::{Path, PathBuf};

use fgm_domain::error::{AppError, ErrorCode};
use fgm_domain::library::GraphicsApi;
use fgm_domain::version::VERSION;
use fgm_infra::dlss5::Dlss5Service;
use fgm_infra::library::GraphicsApiDetector;
use fgm_infra::pack_json::PackJson;
use fgm_infra::packs::PackLocator;
use fgm_infra::paths::AppPaths;

const PACK_ID: &str = "dlss5.reshade-gitc";

pub fn run_dlss5(args: &[String]) -> i32 {
    match args.split_first() {
        Some((command, rest)) if command == "install" => run_install(rest),
        Some((command, rest)) if command == "uninstall" => run_uninstall(rest),
        Some((command, rest)) if command == "inspect" => run_inspect(rest),
        _ => {
            print_usage();
            2
        }
    }
}

fn run_install(args: &[String]) -> i32 {
    let mut data_directory = None;
    let mut pack_directory = None;
    let mut api_override = None;
    let mut app_version = VERSION.to_string();
    let mut positionals = Vec::new();
    let mut index = 0;

    while index < args.len() {
        let argument = args[index].as_str();
        match argument {
            "--data-dir" => match next_value(args, &mut index) {
                Some(value) => data_directory = Some(value),
                None => return 2,
            },
            "--pack-dir" => match next_value(args, &mut index) {
                Some(value) => pack_directory = Some(value),
                None => return 2,
            },
            "--api" => match next_value(args, &mut index) {
                Some(value) => api_override = Some(value),
                None => return 2,
            },
            "--app-version" => match next_value(args, &mut index) {
                Some(value) => app_version = value,
                None => return 2,
            },
            "--help" | "-h" => {
                print_usage();
                return 0;
            }
            value if value.starts_with('-') => {
                eprintln!("unknown option: {value}");
                return 2;
            }
            value => positionals.push(value.to_string()),
        }
        index += 1;
    }

    if positionals.len() != 2 {
        print_usage();
        return 2;
    }

    let target_directory = match absolute(Path::new(&positionals[0])) {
        Ok(path) => path,
        Err(error) => return print_error(&error),
    };
    let game_exe = match absolute(Path::new(&positionals[1])) {
        Ok(path) => path,
        Err(error) => return print_error(&error),
    };
    let pack_directory = match resolve_pack(pack_directory.as_deref()) {
        Ok(path) => path,
        Err(error) => return print_error(&error),
    };
    let api = match api_override {
        Some(value) => match parse_api(&value) {
            Some(api) => api,
            None => {
                eprintln!("unknown graphics API: {value}");
                return 2;
            }
        },
        None => GraphicsApiDetector::detect(&game_exe, &target_directory),
    };

    let paths = match data_paths(data_directory.as_deref()) {
        Ok(paths) => paths,
        Err(error) => return print_error(&error),
    };
    if let Err(error) = paths.ensure_created() {
        return print_error(&error);
    }

    match Dlss5Service::install(
        &pack_directory,
        &game_exe,
        &target_directory,
        api,
        &app_version,
        &paths,
    ) {
        Ok(report) => {
            println!(
                "OK {} api={:?} proxy={} files={}",
                PACK_ID, api, report.proxy_module, report.files_written
            );
            0
        }
        Err(error) => print_error(&error),
    }
}

fn run_uninstall(args: &[String]) -> i32 {
    let mut target_directory = None;
    let mut data_directory = None;
    let mut index = 0;

    while index < args.len() {
        let argument = args[index].as_str();
        match argument {
            "--data-dir" => match next_value(args, &mut index) {
                Some(value) => data_directory = Some(value),
                None => return 2,
            },
            "--help" | "-h" => {
                print_usage();
                return 0;
            }
            value if value.starts_with('-') => {
                eprintln!("unknown option: {value}");
                return 2;
            }
            value if target_directory.is_none() => {
                target_directory = Some(value.to_string());
            }
            value => {
                eprintln!("unexpected argument: {value}");
                return 2;
            }
        }
        index += 1;
    }

    let Some(target_directory) = target_directory else {
        print_usage();
        return 2;
    };
    let target_directory = match absolute(Path::new(&target_directory)) {
        Ok(path) => path,
        Err(error) => return print_error(&error),
    };
    let paths = match data_paths(data_directory.as_deref()) {
        Ok(paths) => paths,
        Err(error) => return print_error(&error),
    };
    if let Err(error) = paths.ensure_created() {
        return print_error(&error);
    }

    match Dlss5Service::uninstall(&target_directory, &paths) {
        Ok(report) => {
            for warning in &report.warnings {
                eprintln!("WARN {warning}");
            }
            for conflict in &report.conflicts {
                eprintln!("CONFLICT {conflict}");
            }
            println!(
                "restored={} removed={} conflicts={}",
                report.restored_files,
                report.removed_files,
                report.conflicts.len()
            );
            if report.conflicts.is_empty() { 0 } else { 1 }
        }
        Err(error) => print_error(&error),
    }
}

fn run_inspect(args: &[String]) -> i32 {
    let mut target_directory = None;
    let mut data_directory = None;
    let mut pack_directory = None;
    let mut index = 0;

    while index < args.len() {
        let argument = args[index].as_str();
        match argument {
            "--data-dir" => match next_value(args, &mut index) {
                Some(value) => data_directory = Some(value),
                None => return 2,
            },
            "--pack-dir" => match next_value(args, &mut index) {
                Some(value) => pack_directory = Some(value),
                None => return 2,
            },
            "--help" | "-h" => {
                print_usage();
                return 0;
            }
            value if value.starts_with('-') => {
                eprintln!("unknown option: {value}");
                return 2;
            }
            value if target_directory.is_none() => {
                target_directory = Some(value.to_string());
            }
            value => {
                eprintln!("unexpected argument: {value}");
                return 2;
            }
        }
        index += 1;
    }

    let Some(target_directory) = target_directory else {
        print_usage();
        return 2;
    };
    let target_directory = match absolute(Path::new(&target_directory)) {
        Ok(path) => path,
        Err(error) => return print_error(&error),
    };
    let pack = match pack_directory.as_deref() {
        Some(directory) => {
            let directory = match resolve_pack(Some(directory)) {
                Ok(path) => path,
                Err(error) => return print_error(&error),
            };
            match PackJson::load(&directory.join("pack.json")) {
                Ok(pack) => Some(pack),
                Err(error) => return print_error(&error),
            }
        }
        None => None,
    };
    let paths = match data_paths(data_directory.as_deref()) {
        Ok(paths) => paths,
        Err(error) => return print_error(&error),
    };
    let inspection = match Dlss5Service::inspect(&target_directory, pack.as_ref(), &paths) {
        Ok(inspection) => inspection,
        Err(error) => return print_error(&error),
    };

    println!(
        "state={:?} manifest={} version={}",
        inspection.state,
        inspection.has_manifest,
        inspection.pack_version.as_deref().unwrap_or("-")
    );
    println!(
        "reshade={} proxy={}",
        inspection.re_shade_present,
        inspection.proxy_module.as_deref().unwrap_or("-")
    );
    for legacy in &inspection.legacy_files {
        println!("LEGACY {legacy}");
    }
    for detail in &inspection.details {
        println!("DETAIL {detail}");
    }
    0
}

fn resolve_pack(directory: Option<&str>) -> Result<PathBuf, AppError> {
    if let Some(directory) = directory {
        return absolute(Path::new(directory));
    }
    PackLocator::find(PACK_ID).ok_or_else(|| {
        AppError::new(ErrorCode::PackNotFound).with("reason", format!("{PACK_ID} was not found"))
    })
}

fn parse_api(value: &str) -> Option<GraphicsApi> {
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

fn data_paths(directory: Option<&str>) -> Result<AppPaths, AppError> {
    match directory {
        Some(directory) => AppPaths::new(PathBuf::from(directory), false),
        None => AppPaths::create_default(None),
    }
}

fn absolute(path: &Path) -> Result<PathBuf, AppError> {
    std::path::absolute(path).map_err(|error| {
        AppError::new(ErrorCode::InvalidPath)
            .with("path", path.display().to_string())
            .with("reason", error.to_string())
    })
}

fn next_value(args: &[String], index: &mut usize) -> Option<String> {
    *index += 1;
    match args.get(*index) {
        Some(value) if !value.starts_with('-') => Some(value.clone()),
        _ => {
            eprintln!("option requires a value");
            None
        }
    }
}

fn print_error(error: &AppError) -> i32 {
    let detail = error
        .context
        .iter()
        .find(|(key, _)| key == "reason")
        .map_or("operation failed", |(_, value)| value.as_str());
    eprintln!("{:?}: {detail}", error.code);
    1
}

fn print_usage() {
    eprintln!("usage:");
    eprintln!(
        "  fgm-next dlss5 install <target-dir> <game-exe> [--api <api>] [--pack-dir <dir>] [--data-dir <dir>]"
    );
    eprintln!("  fgm-next dlss5 inspect <target-dir> [--pack-dir <dir>] [--data-dir <dir>]");
    eprintln!("  fgm-next dlss5 uninstall <target-dir> [--data-dir <dir>]");
}
