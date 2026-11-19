use std::path::Path;

use fgm_domain::error::AppError;
use fgm_domain::pack::{PackValidation, PackValidator};
use fgm_domain::version::VERSION;
use fgm_infra::pack_json::PackJson;
use fgm_infra::pack_payload::PackPayloadValidator;

mod deploy;

fn main() {
    std::process::exit(run(std::env::args().skip(1).collect()));
}

fn run(args: Vec<String>) -> i32 {
    match args.as_slice() {
        [] => {
            print_usage();
            2
        }
        [flag] if flag == "--version" || flag == "-V" => {
            println!("fgm-next {VERSION}");
            0
        }
        [flag] if flag == "--help" || flag == "-h" => {
            print_usage();
            0
        }
        [pack, validate, rest @ ..] if pack == "pack" && validate == "validate" => {
            run_pack_validate(rest)
        }
        [deploy, rest @ ..] if deploy == "deploy" => self::deploy::run_deploy(rest),
        [restore, rest @ ..] if restore == "restore" => self::deploy::run_restore(rest),
        _ => {
            eprintln!("unknown command");
            print_usage();
            2
        }
    }
}

fn run_pack_validate(args: &[String]) -> i32 {
    let mut directory = None;
    let mut verify_hashes = true;
    let mut schema_only = false;
    let mut app_version = VERSION.to_string();
    let mut index = 0;

    while index < args.len() {
        let argument = args[index].as_str();
        match argument {
            "--no-hash" => verify_hashes = false,
            "--schema-only" => schema_only = true,
            "--help" | "-h" => {
                print_pack_usage();
                return 0;
            }
            "--app-version" => {
                index += 1;
                let Some(value) = args.get(index) else {
                    eprintln!("--app-version requires a value");
                    return 2;
                };
                app_version = value.clone();
            }
            _ if argument.starts_with("--app-version=") => {
                app_version = argument["--app-version=".len()..].to_string();
            }
            _ if argument.starts_with('-') => {
                eprintln!("unknown option: {argument}");
                return 2;
            }
            _ if directory.is_none() => directory = Some(argument),
            _ => {
                eprintln!("unexpected argument: {argument}");
                return 2;
            }
        }
        index += 1;
    }

    let Some(directory) = directory else {
        print_pack_usage();
        return 2;
    };

    let directory = Path::new(directory);
    let directory = match std::path::absolute(directory) {
        Ok(path) => path,
        Err(error) => {
            eprintln!("invalid pack directory: {error}");
            return 2;
        }
    };

    if !directory.is_dir() {
        eprintln!("pack directory not found: {}", directory.display());
        return 2;
    }

    let manifest = directory.join("pack.json");
    let pack = match PackJson::load(&manifest) {
        Ok(pack) => pack,
        Err(error) => {
            print_error(&error);
            return 1;
        }
    };

    let schema = PackValidator::validate(&pack, Some(&app_version));
    if !schema.is_valid() {
        print_issues(&schema);
        return 1;
    }

    if schema_only {
        println!("OK {} {} (schema only)", pack.id, pack.version);
        return 0;
    }

    let payload = PackPayloadValidator::validate(&pack, &directory, verify_hashes);
    if !payload.is_valid() {
        print_issues(&payload);
        return 1;
    }

    println!(
        "OK {} {} ({} files, trust={:?})",
        pack.id,
        pack.version,
        pack.files.len(),
        pack.trust
    );
    0
}

fn print_error(error: &AppError) {
    let detail = error
        .context
        .iter()
        .find(|(key, _)| key == "reason")
        .map_or("operation failed", |(_, value)| value.as_str());
    eprintln!("{:?}: {detail}", error.code);
}

fn print_issues(validation: &PackValidation) {
    for issue in validation.issues() {
        eprintln!(
            "{} {}: {}",
            issue.code.as_str(),
            issue.field,
            issue.diagnostic
        );
    }
}

fn print_usage() {
    eprintln!("fgm-next {VERSION}");
    eprintln!("usage:");
    eprintln!("  fgm-next --version");
    eprintln!(
        "  fgm-next pack validate <pack-dir> [--no-hash] [--schema-only] [--app-version <version>]"
    );
    eprintln!(
        "  fgm-next deploy <pack-dir> <target-dir> [--family <id>] [--proxy-slot <target>] [--data-dir <dir>] [--game-exe <path>] [--dry-run]"
    );
    eprintln!("  fgm-next restore <target-dir> --family <id> [--data-dir <dir>]");
}

fn print_pack_usage() {
    eprintln!(
        "usage: fgm-next pack validate <pack-dir> [--no-hash] [--schema-only] [--app-version <version>]"
    );
    eprintln!("  --no-hash       verify file existence and size only");
    eprintln!("  --schema-only   validate pack.json without touching the payload");
    eprintln!("  --app-version   app version used for minAppVersion checks");
}
