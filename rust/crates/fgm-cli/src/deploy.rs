use std::collections::BTreeMap;
use std::path::{Path, PathBuf};
use std::sync::atomic::AtomicBool;

use fgm_domain::error::AppError;
use fgm_domain::version::VERSION;
use fgm_infra::deployment::{DeploymentEngine, PackDeploymentPlanner};
use fgm_infra::pack_json::PackJson;
use fgm_infra::paths::AppPaths;

pub fn run_deploy(args: &[String]) -> i32 {
    let mut directories = Vec::new();
    let mut family = None;
    let mut proxy_slot = None;
    let mut data_directory = None;
    let mut game_exe = None;
    let mut app_version = VERSION.to_string();
    let mut dry_run = false;
    let mut index = 0;

    while index < args.len() {
        let argument = args[index].as_str();
        match argument {
            "--family" => match next_value(args, &mut index) {
                Some(value) => family = Some(value),
                None => return 2,
            },
            "--proxy-slot" => match next_value(args, &mut index) {
                Some(value) => proxy_slot = Some(value),
                None => return 2,
            },
            "--data-dir" => match next_value(args, &mut index) {
                Some(value) => data_directory = Some(value),
                None => return 2,
            },
            "--game-exe" => match next_value(args, &mut index) {
                Some(value) => game_exe = Some(value),
                None => return 2,
            },
            "--app-version" => match next_value(args, &mut index) {
                Some(value) => app_version = value,
                None => return 2,
            },
            "--dry-run" => dry_run = true,
            "--help" | "-h" => {
                print_deploy_usage();
                return 0;
            }
            value if value.starts_with('-') => {
                eprintln!("unknown option: {value}");
                return 2;
            }
            value => directories.push(value.to_string()),
        }
        index += 1;
    }

    if directories.len() != 2 {
        print_deploy_usage();
        return 2;
    }

    let pack_directory = match absolute(Path::new(&directories[0])) {
        Ok(path) => path,
        Err(error) => return print_error(&error),
    };
    let target_directory = match absolute(Path::new(&directories[1])) {
        Ok(path) => path,
        Err(error) => return print_error(&error),
    };

    if !pack_directory.is_dir() {
        eprintln!("pack directory not found: {}", pack_directory.display());
        return 2;
    }

    let manifest = pack_directory.join("pack.json");
    let pack = match PackJson::load(&manifest) {
        Ok(pack) => pack,
        Err(error) => return print_error(&error),
    };

    let mut options = BTreeMap::new();
    if let Some(slot) = &proxy_slot {
        options.insert("proxySlot".to_string(), slot.clone());
    }

    let plan = match PackDeploymentPlanner::plan(
        &pack,
        &pack_directory,
        &target_directory,
        &options,
        &[],
        Some(&app_version),
    ) {
        Ok(plan) => plan,
        Err(error) => return print_error(&error),
    };

    if dry_run {
        for file in &plan.files {
            println!("PLAN {}", file.relative_path);
        }
        println!(
            "OK {} {} ({} files, proxySlot={})",
            pack.id,
            pack.version,
            plan.files.len(),
            plan.proxy_slot.as_deref().unwrap_or("-")
        );
        return 0;
    }

    let paths = match data_paths(data_directory.as_deref()) {
        Ok(paths) => paths,
        Err(error) => return print_error(&error),
    };
    if let Err(error) = paths.ensure_created() {
        return print_error(&error);
    }

    let family = family.unwrap_or_else(|| pack.id.clone());
    let lock_directory = paths.state().join("locks");
    let game_executable = match game_exe {
        Some(path) => match absolute(Path::new(&path)) {
            Ok(path) => Some(path),
            Err(error) => return print_error(&error),
        },
        None => None,
    };
    let report = match DeploymentEngine::apply(
        &plan.files,
        &target_directory,
        &family,
        &pack.id,
        &pack.version,
        &options,
        &pack.owned_directories,
        game_executable.as_deref(),
        &paths.backups(),
        &lock_directory,
        &AtomicBool::new(false),
        None,
    ) {
        Ok(report) => report,
        Err(error) => return print_error(&error),
    };

    println!(
        "OK {} {} state={:?} files={} backedUp={}",
        pack.id, pack.version, report.state, report.files_written, report.files_backed_up
    );
    0
}

pub fn run_restore(args: &[String]) -> i32 {
    let mut target_directory = None;
    let mut family = None;
    let mut data_directory = None;
    let mut index = 0;

    while index < args.len() {
        let argument = args[index].as_str();
        match argument {
            "--family" => match next_value(args, &mut index) {
                Some(value) => family = Some(value),
                None => return 2,
            },
            "--data-dir" => match next_value(args, &mut index) {
                Some(value) => data_directory = Some(value),
                None => return 2,
            },
            "--help" | "-h" => {
                print_restore_usage();
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
        print_restore_usage();
        return 2;
    };
    let Some(family) = family else {
        eprintln!("--family is required for restore");
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

    let report = match DeploymentEngine::restore(
        &family,
        &target_directory,
        &paths.backups(),
        &paths.state().join("locks"),
    ) {
        Ok(report) => report,
        Err(error) => return print_error(&error),
    };

    for warning in &report.warnings {
        eprintln!("WARN {warning}");
    }
    println!(
        "restored={} removed={} conflicts={}",
        report.restored_files,
        report.removed_files,
        report.conflicts.len()
    );
    if report.conflicts.is_empty() { 0 } else { 1 }
}

pub fn run_inspect(args: &[String]) -> i32 {
    let mut target_directory = None;
    let mut family = None;
    let mut data_directory = None;
    let mut pack_directory = None;
    let mut index = 0;

    while index < args.len() {
        let argument = args[index].as_str();
        match argument {
            "--family" => match next_value(args, &mut index) {
                Some(value) => family = Some(value),
                None => return 2,
            },
            "--data-dir" => match next_value(args, &mut index) {
                Some(value) => data_directory = Some(value),
                None => return 2,
            },
            "--pack-dir" => match next_value(args, &mut index) {
                Some(value) => pack_directory = Some(value),
                None => return 2,
            },
            "--help" | "-h" => {
                print_inspect_usage();
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
        print_inspect_usage();
        return 2;
    };
    let Some(family) = family else {
        eprintln!("--family is required for inspect");
        return 2;
    };

    let target_directory = match absolute(Path::new(&target_directory)) {
        Ok(path) => path,
        Err(error) => return print_error(&error),
    };
    let pack = match pack_directory {
        Some(directory) => {
            let directory = match absolute(Path::new(&directory)) {
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

    let inspection = match fgm_infra::deployment::DeploymentInspector::inspect(
        &family,
        &target_directory,
        &paths.backups(),
        pack.as_ref(),
    ) {
        Ok(inspection) => inspection,
        Err(error) => return print_error(&error),
    };

    println!(
        "state={:?} manifest={} version={}",
        inspection.state,
        inspection.has_manifest,
        inspection.pack_version.as_deref().unwrap_or("-")
    );
    for detail in &inspection.details {
        println!("DETAIL {detail}");
    }
    0
}

fn data_paths(directory: Option<&str>) -> Result<AppPaths, AppError> {
    match directory {
        Some(directory) => AppPaths::new(PathBuf::from(directory), false),
        None => AppPaths::create_default(None),
    }
}

fn absolute(path: &Path) -> Result<PathBuf, AppError> {
    std::path::absolute(path).map_err(|error| {
        AppError::new(fgm_domain::error::ErrorCode::InvalidPath)
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

fn print_deploy_usage() {
    eprintln!(
        "usage: fgm-next deploy <pack-dir> <target-dir> [--family <id>] [--proxy-slot <target>]"
    );
    eprintln!(
        "       [--app-version <version>] [--data-dir <dir>] [--game-exe <path>] [--dry-run]"
    );
}

fn print_restore_usage() {
    eprintln!("usage: fgm-next restore <target-dir> --family <id> [--data-dir <dir>]");
}

fn print_inspect_usage() {
    eprintln!(
        "usage: fgm-next inspect <target-dir> --family <id> [--pack-dir <dir>] [--data-dir <dir>]"
    );
}
