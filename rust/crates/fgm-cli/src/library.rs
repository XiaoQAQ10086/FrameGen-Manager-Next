use std::path::{Path, PathBuf};

use fgm_domain::error::AppError;
use fgm_infra::library::{EpicLocator, LibraryScanner, LibraryStore, SteamLocator};
use fgm_infra::paths::AppPaths;
use fgm_infra::platform::DriveRoots;

pub fn run_library(args: &[String]) -> i32 {
    match args.first().map(String::as_str) {
        Some("scan") => run_scan(&args[1..]),
        Some("list") => run_list(&args[1..]),
        _ => {
            print_usage();
            2
        }
    }
}

fn run_scan(args: &[String]) -> i32 {
    let mut data_directory = None;
    let mut roots = Vec::new();
    let mut steam_directory = None;
    let mut epic_manifests = None;
    let mut index = 0;

    while index < args.len() {
        match args[index].as_str() {
            "--data-dir" => match next_value(args, &mut index) {
                Some(value) => data_directory = Some(value),
                None => return 2,
            },
            "--root" => match next_value(args, &mut index) {
                Some(value) => roots.push(PathBuf::from(value)),
                None => return 2,
            },
            "--steam-dir" => match next_value(args, &mut index) {
                Some(value) => steam_directory = Some(PathBuf::from(value)),
                None => return 2,
            },
            "--epic-manifests" => match next_value(args, &mut index) {
                Some(value) => epic_manifests = Some(PathBuf::from(value)),
                None => return 2,
            },
            "--help" | "-h" => {
                print_usage();
                return 0;
            }
            value => {
                eprintln!("unknown option: {value}");
                return 2;
            }
        }
        index += 1;
    }

    if roots.is_empty() {
        roots = DriveRoots::roots();
    }

    let steam = steam_directory.or_else(SteamLocator::locate);
    let epic = epic_manifests.or_else(EpicLocator::manifests_directory);
    let games = LibraryScanner::scan_with(steam.as_deref(), epic.as_deref(), &roots);

    let paths = match data_paths(data_directory.as_deref()) {
        Ok(paths) => paths,
        Err(error) => return print_error(&error),
    };
    if let Err(error) = paths.ensure_created() {
        return print_error(&error);
    }

    let store = LibraryStore::new(paths.library().join("library.json"));
    let mut cache = match store.load() {
        Ok(cache) => cache,
        Err(error) => return print_error(&error),
    };
    cache.schema_version = 1;
    cache.games = games;
    if let Err(error) = store.save(&cache) {
        return print_error(&error);
    }

    println!("OK {} games", cache.games.len());
    for game in &cache.games {
        println!(
            "GAME {} | {:?} | {:?} | {}",
            game.name,
            game.source,
            game.graphics_api,
            game.install_directory.display()
        );
    }
    0
}

fn run_list(args: &[String]) -> i32 {
    let mut data_directory = None;
    let mut index = 0;
    while index < args.len() {
        match args[index].as_str() {
            "--data-dir" => match next_value(args, &mut index) {
                Some(value) => data_directory = Some(value),
                None => return 2,
            },
            "--help" | "-h" => {
                print_usage();
                return 0;
            }
            value => {
                eprintln!("unknown option: {value}");
                return 2;
            }
        }
        index += 1;
    }

    let paths = match data_paths(data_directory.as_deref()) {
        Ok(paths) => paths,
        Err(error) => return print_error(&error),
    };
    let store = LibraryStore::new(paths.library().join("library.json"));
    let cache = match store.load() {
        Ok(cache) => cache,
        Err(error) => return print_error(&error),
    };

    println!("{} games", cache.games.len());
    for game in &cache.games {
        println!(
            "{} | {:?} | {:?} | {}",
            game.id, game.source, game.graphics_api, game.name
        );
    }
    0
}

fn data_paths(directory: Option<&str>) -> Result<AppPaths, AppError> {
    match directory {
        Some(directory) => AppPaths::new(Path::new(directory), false),
        None => AppPaths::create_default(None),
    }
}

fn next_value(args: &[String], index: &mut usize) -> Option<String> {
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
    eprintln!(
        "  fgm-next library scan [--root <dir>]... [--steam-dir <dir>] [--epic-manifests <dir>] [--data-dir <dir>]"
    );
    eprintln!("  fgm-next library list [--data-dir <dir>]");
}
