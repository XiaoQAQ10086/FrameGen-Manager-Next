use std::path::Path;

use fgm_domain::version::VERSION;
use fgm_infra::dlss5::Dlss5Service;
use fgm_infra::library::GraphicsApiDetector;
use fgm_infra::pack_json::PackJson;

use crate::dlss5_common::{
    PACK_ID, absolute, data_paths, next_value, parse_api, print_dlss5_usage, print_error,
    resolve_pack,
};

pub fn run_dlss5(args: &[String]) -> i32 {
    match args.split_first() {
        Some((command, rest)) if command == "install" => run_install(rest),
        Some((command, rest)) if command == "restore" => run_restore(rest),
        Some((command, rest)) if command == "uninstall" => run_uninstall(rest),
        Some((command, rest)) if command == "inspect" => run_inspect(rest),
        Some((command, rest)) if command == "hotkey" => run_hotkey(rest),
        _ => {
            print_dlss5_usage();
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
                print_dlss5_usage();
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
        print_dlss5_usage();
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

fn run_restore(args: &[String]) -> i32 {
    let mut target_directory = None;
    let mut data_directory = None;
    let mut index = 0;
    while index < args.len() {
        match args[index].as_str() {
            "--data-dir" => match next_value(args, &mut index) {
                Some(value) => data_directory = Some(value),
                None => return 2,
            },
            "--help" | "-h" => {
                print_dlss5_usage();
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
        print_dlss5_usage();
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
    match Dlss5Service::restore_keeping_reshade(&target_directory, &paths) {
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
                print_dlss5_usage();
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
        print_dlss5_usage();
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
                print_dlss5_usage();
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
        print_dlss5_usage();
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
        "reshade={} proxy={} hotkey={}",
        inspection.re_shade_present,
        inspection.proxy_module.as_deref().unwrap_or("-"),
        inspection
            .hotkey_vk
            .map_or_else(|| "-".to_string(), |vk| vk.to_string())
    );
    for legacy in &inspection.legacy_files {
        println!("LEGACY {legacy}");
    }
    for detail in &inspection.details {
        println!("DETAIL {detail}");
    }
    0
}

fn run_hotkey(args: &[String]) -> i32 {
    let mut data_directory = None;
    let mut positionals = Vec::new();
    let mut index = 0;

    while index < args.len() {
        let argument = args[index].as_str();
        match argument {
            "--data-dir" => match next_value(args, &mut index) {
                Some(value) => data_directory = Some(value),
                None => return 2,
            },
            "--help" | "-h" => {
                print_dlss5_usage();
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
        print_dlss5_usage();
        return 2;
    }
    let Ok(virtual_key) = positionals[1].parse::<u32>() else {
        eprintln!("virtual key must be a number");
        return 2;
    };
    let target_directory = match absolute(Path::new(&positionals[0])) {
        Ok(path) => path,
        Err(error) => return print_error(&error),
    };
    let paths = match data_paths(data_directory.as_deref()) {
        Ok(paths) => paths,
        Err(error) => return print_error(&error),
    };

    match Dlss5Service::set_hotkey(&target_directory, virtual_key, None, &paths) {
        Ok(outcome) => {
            println!(
                "OK hotkey={virtual_key} queued={}",
                outcome == fgm_infra::dlss5::HotkeyOutcome::Queued
            );
            0
        }
        Err(error) => print_error(&error),
    }
}
