use std::path::PathBuf;

use fgm_domain::error::AppError;
use fgm_infra::gpu_spoof::{GpuRestoreMode, GpuSpoofService};
use fgm_infra::paths::AppPaths;

pub fn run_gpu(args: &[String]) -> i32 {
    match args.split_first() {
        Some((command, rest)) if command == "spoof" => run_spoof(rest),
        _ => {
            print_usage();
            2
        }
    }
}

fn run_spoof(args: &[String]) -> i32 {
    match args.split_first() {
        Some((command, rest)) if command == "inspect" => run_inspect(rest),
        Some((command, rest)) if command == "apply" => run_apply(rest),
        Some((command, rest)) if command == "restore" => run_restore(rest),
        _ => {
            print_usage();
            2
        }
    }
}

fn run_inspect(args: &[String]) -> i32 {
    let (data_directory, rest) = match parse_data_dir(args) {
        Ok(value) => value,
        Err(code) => return code,
    };
    if !rest.is_empty() {
        print_usage();
        return 2;
    }
    let paths = match data_paths(data_directory.as_deref()) {
        Ok(paths) => paths,
        Err(error) => return print_error(&error),
    };
    let inspection = match GpuSpoofService::inspect(&paths) {
        Ok(inspection) => inspection,
        Err(error) => return print_error(&error),
    };
    println!("elevated={}", inspection.elevated);
    for (index, adapter) in inspection.adapters.iter().enumerate() {
        println!(
            "ADAPTER {index} instance={} driver={} name={} desc={}",
            adapter.instance_path, adapter.driver_index, adapter.display_name, adapter.device_desc
        );
    }
    for preset in &inspection.presets {
        println!("PRESET {preset}");
    }
    if let Some(applied) = &inspection.applied {
        println!(
            "APPLIED {} original={}",
            applied.applied_name, applied.original_device_desc
        );
    }
    0
}

fn run_apply(args: &[String]) -> i32 {
    let (data_directory, rest) = match parse_data_dir(args) {
        Ok(value) => value,
        Err(code) => return code,
    };
    let (Some(index), Some(preset)) = (rest.first(), rest.get(1)) else {
        print_usage();
        return 2;
    };
    let Ok(index) = index.parse::<usize>() else {
        eprintln!("adapter index must be a number");
        return 2;
    };
    let paths = match data_paths(data_directory.as_deref()) {
        Ok(paths) => paths,
        Err(error) => return print_error(&error),
    };
    match GpuSpoofService::apply(&paths, index, preset) {
        Ok(adapter) => {
            println!(
                "OK name={} desc={}",
                adapter.display_name, adapter.device_desc
            );
            0
        }
        Err(error) => print_error(&error),
    }
}

fn run_restore(args: &[String]) -> i32 {
    let (data_directory, rest) = match parse_data_dir(args) {
        Ok(value) => value,
        Err(code) => return code,
    };
    let (Some(index), Some(mode)) = (rest.first(), rest.get(1)) else {
        print_usage();
        return 2;
    };
    let Ok(index) = index.parse::<usize>() else {
        eprintln!("adapter index must be a number");
        return 2;
    };
    let mode = match mode.as_str() {
        "driver" => GpuRestoreMode::Driver,
        "backup" => GpuRestoreMode::Backup,
        value => {
            eprintln!("unknown restore mode: {value}");
            return 2;
        }
    };
    let paths = match data_paths(data_directory.as_deref()) {
        Ok(paths) => paths,
        Err(error) => return print_error(&error),
    };
    match GpuSpoofService::restore(&paths, index, mode) {
        Ok(adapter) => {
            println!(
                "OK name={} desc={}",
                adapter.display_name, adapter.device_desc
            );
            0
        }
        Err(error) => print_error(&error),
    }
}

fn parse_data_dir(args: &[String]) -> Result<(Option<String>, Vec<String>), i32> {
    let mut data_directory = None;
    let mut positionals = Vec::new();
    let mut index = 0;
    while index < args.len() {
        match args[index].as_str() {
            "--data-dir" => {
                index += 1;
                let Some(value) = args.get(index) else {
                    eprintln!("--data-dir requires a value");
                    return Err(2);
                };
                data_directory = Some(value.clone());
            }
            value if value.starts_with('-') => {
                eprintln!("unknown option: {value}");
                return Err(2);
            }
            value => positionals.push(value.to_string()),
        }
        index += 1;
    }
    Ok((data_directory, positionals))
}

fn data_paths(directory: Option<&str>) -> Result<AppPaths, AppError> {
    match directory {
        Some(directory) => AppPaths::new(PathBuf::from(directory), false),
        None => AppPaths::create_default(None),
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
    eprintln!("  fgm-next gpu spoof inspect [--data-dir <dir>]");
    eprintln!("  fgm-next gpu spoof apply <adapter-index> \"<preset>\" [--data-dir <dir>]");
    eprintln!("  fgm-next gpu spoof restore <adapter-index> driver|backup [--data-dir <dir>]");
}
