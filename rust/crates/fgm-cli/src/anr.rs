use std::path::{Path, PathBuf};

use fgm_domain::error::AppError;
use fgm_infra::anr::{AnrDeploymentService, AnrPackageService};
use fgm_infra::paths::AppPaths;

pub fn run_anr(args: &[String]) -> i32 {
    match args.first().map(String::as_str) {
        Some("import") => run_import(&args[1..]),
        Some("status") => run_status(&args[1..]),
        Some("deploy") => run_deploy(&args[1..]),
        Some("restore") => run_restore(&args[1..]),
        _ => {
            print_usage();
            2
        }
    }
}

fn run_import(args: &[String]) -> i32 {
    let mut zip = None;
    let mut data = None;
    let mut index = 0;
    while index < args.len() {
        match args[index].as_str() {
            "--data-dir" => match next(args, &mut index) {
                Some(value) => data = Some(value),
                None => return 2,
            },
            value if zip.is_none() => zip = Some(value.to_string()),
            value => {
                eprintln!("unexpected argument: {value}");
                return 2;
            }
        }
        index += 1;
    }
    let Some(zip) = zip else {
        print_usage();
        return 2;
    };
    let paths = match paths(data.as_deref()) {
        Ok(paths) => paths,
        Err(error) => return print_error(&error),
    };
    match AnrPackageService::import(Path::new(&zip), paths.root()) {
        Ok(info) => {
            println!(
                "OK {:?} {} {}",
                info.kind,
                info.version,
                info.payload_directory.display()
            );
            0
        }
        Err(error) => print_error(&error),
    }
}

fn run_status(args: &[String]) -> i32 {
    let data = match data_argument(args) {
        Ok(value) => value,
        Err(code) => return code,
    };
    let paths = match paths(data.as_deref()) {
        Ok(paths) => paths,
        Err(error) => return print_error(&error),
    };
    match AnrPackageService::load_current(paths.root()) {
        Some(info) => {
            println!(
                "{:?} {} {}",
                info.kind,
                info.version,
                info.payload_directory.display()
            );
            0
        }
        None => {
            println!("no ANR package imported");
            1
        }
    }
}

fn run_deploy(args: &[String]) -> i32 {
    let mut target = None;
    let mut exe = None;
    let mut data = None;
    let mut index = 0;
    while index < args.len() {
        match args[index].as_str() {
            "--exe" => match next(args, &mut index) {
                Some(value) => exe = Some(PathBuf::from(value)),
                None => return 2,
            },
            "--data-dir" => match next(args, &mut index) {
                Some(value) => data = Some(value),
                None => return 2,
            },
            value if target.is_none() => target = Some(PathBuf::from(value)),
            value => {
                eprintln!("unexpected argument: {value}");
                return 2;
            }
        }
        index += 1;
    }
    let Some(target) = target else {
        print_usage();
        return 2;
    };
    let paths = match paths(data.as_deref()) {
        Ok(paths) => paths,
        Err(error) => return print_error(&error),
    };
    let Some(package) = AnrPackageService::load_current(paths.root()) else {
        eprintln!("no ANR package imported");
        return 1;
    };
    match AnrDeploymentService::deploy(
        &package,
        &target,
        exe.as_deref(),
        &paths,
        &std::sync::atomic::AtomicBool::new(false),
        None,
        None,
    ) {
        Ok(report) => {
            println!(
                "OK {:?} files={} proxy={}",
                report.kind,
                report.files_written,
                report.proxy_module.as_deref().unwrap_or("-")
            );
            0
        }
        Err(error) => print_error(&error),
    }
}

fn run_restore(args: &[String]) -> i32 {
    let mut target = None;
    let mut data = None;
    let mut index = 0;
    while index < args.len() {
        match args[index].as_str() {
            "--data-dir" => match next(args, &mut index) {
                Some(value) => data = Some(value),
                None => return 2,
            },
            value if target.is_none() => target = Some(PathBuf::from(value)),
            value => {
                eprintln!("unexpected argument: {value}");
                return 2;
            }
        }
        index += 1;
    }
    let Some(target) = target else {
        print_usage();
        return 2;
    };
    let paths = match paths(data.as_deref()) {
        Ok(paths) => paths,
        Err(error) => return print_error(&error),
    };
    match AnrDeploymentService::restore(&target, &paths) {
        Ok(()) => {
            println!("OK restored");
            0
        }
        Err(error) => print_error(&error),
    }
}

fn data_argument(args: &[String]) -> Result<Option<String>, i32> {
    let mut data = None;
    let mut index = 0;
    while index < args.len() {
        match args[index].as_str() {
            "--data-dir" => match next(args, &mut index) {
                Some(value) => data = Some(value),
                None => return Err(2),
            },
            value => {
                eprintln!("unknown option: {value}");
                return Err(2);
            }
        }
        index += 1;
    }
    Ok(data)
}

fn paths(directory: Option<&str>) -> Result<AppPaths, AppError> {
    match directory {
        Some(directory) => AppPaths::new(Path::new(directory), false),
        None => AppPaths::create_default(None),
    }
}

fn next(args: &[String], index: &mut usize) -> Option<String> {
    *index += 1;
    args.get(*index).cloned()
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
    eprintln!("  fgm-next anr import <zip> [--data-dir <dir>]");
    eprintln!("  fgm-next anr status [--data-dir <dir>]");
    eprintln!("  fgm-next anr deploy <target-dir> [--exe <game.exe>] [--data-dir <dir>]");
    eprintln!("  fgm-next anr restore <target-dir> [--data-dir <dir>]");
}
