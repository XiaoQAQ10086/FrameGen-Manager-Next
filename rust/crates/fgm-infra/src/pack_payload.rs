use std::collections::BTreeMap;
use std::path::{Path, PathBuf};

use fgm_domain::error::{AppError, ErrorCode};
use fgm_domain::pack::{
    AssetPack, AssetPackIni, AssetPackStep, PackCode, PackIssue, PackValidation,
};
use fgm_domain::safety::SafePath;

use crate::hashing::FileHasher;

/// Verifies pack payload files against declared sizes and SHA-256 hashes.
pub struct PackPayloadValidator;

impl PackPayloadValidator {
    pub fn validate(pack: &AssetPack, directory: &Path, verify_hashes: bool) -> PackValidation {
        let mut issues = Vec::new();

        if pack.files.is_empty() {
            issues.push(PackIssue::new(
                PackCode::PayloadMissing,
                "files",
                "Pack has no files to validate.",
            ));
            return PackValidation::new(issues);
        }

        for (index, entry) in pack.files.iter().enumerate() {
            let field = format!("files[{index}]");
            let Some(file) = entry else {
                issues.push(PackIssue::new(
                    PackCode::NullEntry,
                    field,
                    "File entry must not be null.",
                ));
                continue;
            };

            let path = match combine_under_root(directory, &file.source) {
                Ok(path) => path,
                Err(error) => {
                    issues.push(issue(
                        PackCode::PayloadMissing,
                        format!("{field}.source"),
                        error_detail(&error),
                    ));
                    continue;
                }
            };

            let metadata = match std::fs::metadata(&path) {
                Ok(metadata) => metadata,
                Err(error) => {
                    let code = if error.kind() == std::io::ErrorKind::NotFound {
                        PackCode::PayloadMissing
                    } else {
                        PackCode::PayloadIo
                    };
                    issues.push(issue(
                        code,
                        format!("{field}.source"),
                        format!("File not readable: {}", path.display()),
                    ));
                    continue;
                }
            };

            if !metadata.is_file() {
                issues.push(issue(
                    PackCode::PayloadMissing,
                    format!("{field}.source"),
                    format!("Not a file: {}", path.display()),
                ));
                continue;
            }

            if let Some(expected) = file.size
                && expected != metadata.len() as i64
            {
                issues.push(issue(
                    PackCode::PayloadSize,
                    format!("{field}.size"),
                    format!("Expected {expected} bytes but found {}.", metadata.len()),
                ));
            }

            if !verify_hashes {
                continue;
            }

            let Some(expected) = file.sha256.as_deref() else {
                issues.push(issue(
                    PackCode::PayloadHash,
                    format!("{field}.sha256"),
                    "SHA-256 is missing.",
                ));
                continue;
            };

            match FileHasher::sha256_file(&path) {
                Ok(actual) if actual.eq_ignore_ascii_case(expected) => {}
                Ok(actual) => issues.push(issue(
                    PackCode::PayloadHash,
                    format!("{field}.sha256"),
                    format!("Expected {expected} but found {actual}."),
                )),
                Err(error) => issues.push(issue(
                    PackCode::PayloadIo,
                    format!("{field}.source"),
                    error_detail(&error),
                )),
            }
        }

        if let Some(ini) = pack.ini.as_ref() {
            validate_ini(ini, directory, verify_hashes, &mut issues);
        }

        validate_installers(pack, directory, verify_hashes, &mut issues);

        PackValidation::new(issues)
    }
}

fn validate_ini(
    ini: &AssetPackIni,
    directory: &Path,
    verify_hashes: bool,
    issues: &mut Vec<PackIssue>,
) {
    let path = match combine_under_root(directory, &ini.template) {
        Ok(path) => path,
        Err(error) => {
            issues.push(issue(
                PackCode::IniTemplateMissing,
                "ini.template",
                error_detail(&error),
            ));
            return;
        }
    };

    let metadata = match std::fs::metadata(&path) {
        Ok(metadata) => metadata,
        Err(error) => {
            let code = if error.kind() == std::io::ErrorKind::NotFound {
                PackCode::IniTemplateMissing
            } else {
                PackCode::PayloadIo
            };
            issues.push(issue(
                code,
                "ini.template",
                format!("INI template not readable: {}", path.display()),
            ));
            return;
        }
    };

    if !metadata.is_file() {
        issues.push(issue(
            PackCode::IniTemplateMissing,
            "ini.template",
            format!("Not a file: {}", path.display()),
        ));
        return;
    }

    if let Some(expected) = ini.template_size
        && expected != metadata.len() as i64
    {
        issues.push(issue(
            PackCode::IniTemplateMismatch,
            "ini.templateSize",
            format!("Expected {expected} bytes but found {}.", metadata.len()),
        ));
    }

    if !verify_hashes {
        return;
    }

    let Some(expected) = ini.template_sha256.as_deref() else {
        issues.push(issue(
            PackCode::IniTemplateMismatch,
            "ini.templateSha256",
            "SHA-256 is missing.",
        ));
        return;
    };

    match FileHasher::sha256_file(&path) {
        Ok(actual) if actual.eq_ignore_ascii_case(expected) => {}
        Ok(actual) => issues.push(issue(
            PackCode::IniTemplateMismatch,
            "ini.templateSha256",
            format!("Expected {expected} but found {actual}."),
        )),
        Err(error) => issues.push(issue(
            PackCode::PayloadIo,
            "ini.template",
            error_detail(&error),
        )),
    }
}

fn validate_installers(
    pack: &AssetPack,
    directory: &Path,
    verify_hashes: bool,
    issues: &mut Vec<PackIssue>,
) {
    for (index, entry) in pack.steps.iter().enumerate() {
        let Some(step) = entry else {
            continue;
        };
        if !step.kind.eq_ignore_ascii_case("runInstaller") {
            continue;
        }

        let field = format!("steps[{index}]");
        let Some(source) = argument(step, "source") else {
            continue;
        };

        let path = match combine_under_root(directory, source) {
            Ok(path) => path,
            Err(error) => {
                issues.push(issue(
                    PackCode::InstallerPayload,
                    format!("{field}.source"),
                    error_detail(&error),
                ));
                continue;
            }
        };

        if !std::fs::metadata(&path).is_ok_and(|metadata| metadata.is_file()) {
            issues.push(issue(
                PackCode::InstallerPayload,
                format!("{field}.source"),
                format!("Installer not found: {}", path.display()),
            ));
            continue;
        }

        if !verify_hashes {
            continue;
        }

        let Some(expected) = argument(step, "sha256") else {
            issues.push(issue(
                PackCode::InstallerPayload,
                format!("{field}.sha256"),
                "Installer SHA-256 is missing.",
            ));
            continue;
        };

        match FileHasher::sha256_file(&path) {
            Ok(actual) if actual.eq_ignore_ascii_case(expected) => {}
            Ok(actual) => issues.push(issue(
                PackCode::InstallerPayload,
                format!("{field}.sha256"),
                format!("Expected {expected} but found {actual}."),
            )),
            Err(error) => issues.push(issue(
                PackCode::InstallerPayload,
                field,
                error_detail(&error),
            )),
        }
    }
}

fn argument<'a>(step: &'a AssetPackStep, key: &str) -> Option<&'a str> {
    step.arguments
        .as_ref()
        .and_then(|arguments| get_ignore_case(arguments, key))
}

fn get_ignore_case<'a>(arguments: &'a BTreeMap<String, String>, key: &str) -> Option<&'a str> {
    arguments
        .iter()
        .find(|(candidate, _)| candidate.eq_ignore_ascii_case(key))
        .map(|(_, value)| value.as_str())
}

fn combine_under_root(root: &Path, relative: &str) -> Result<PathBuf, AppError> {
    if !root.is_absolute() {
        return Err(
            AppError::new(ErrorCode::InvalidPath).with("reason", "root directory is not absolute")
        );
    }

    let normalized = SafePath::normalize_relative(relative)?;
    let mut path = root.to_path_buf();
    for segment in normalized.split('/') {
        path.push(segment);
    }
    Ok(path)
}

fn issue(code: PackCode, field: impl Into<String>, diagnostic: impl Into<String>) -> PackIssue {
    PackIssue::new(code, field, diagnostic)
}

fn error_detail(error: &AppError) -> String {
    error
        .context
        .iter()
        .find(|(key, _)| key == "reason")
        .map_or_else(|| "invalid path".to_string(), |(_, value)| value.clone())
}
