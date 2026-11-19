use std::collections::HashSet;

use crate::error::AppError;
use crate::safety::SafePath;

use super::asset_pack::AssetPack;
use super::file_rules::{validate_files, validate_owned_directories};
use super::ini_rules::validate_ini;
use super::pack_code::PackCode;
use super::pack_issue::PackIssue;
use super::pack_validation::PackValidation;
use super::step_rules::validate_steps;
use super::version_req;

/// Pure schema validation for `pack.json`. Payload hashes are checked by the
/// infrastructure validator because they need the file system.
pub struct PackValidator;

impl PackValidator {
    pub const SUPPORTED_SCHEMA_VERSION: u32 = 1;

    const ALLOWED_GPU_ROUTES: [&'static str; 8] = [
        "Any",
        "Sm86",
        "Sm75",
        "Gtx16",
        "Pascal",
        "NotNeeded",
        "Unsupported",
        "Unknown",
    ];

    pub fn validate(pack: &AssetPack, current_app_version: Option<&str>) -> PackValidation {
        let mut issues = Vec::new();

        if pack.schema_version != Self::SUPPORTED_SCHEMA_VERSION {
            add(
                &mut issues,
                PackCode::SchemaVersion,
                "schemaVersion",
                format!(
                    "Only schema version {} is supported.",
                    Self::SUPPORTED_SCHEMA_VERSION
                ),
            );
        }

        if !is_valid_id(&pack.id) {
            add(
                &mut issues,
                PackCode::InvalidId,
                "id",
                "Pack id must match ^[a-z0-9][a-z0-9._-]{1,63}$.",
            );
        }

        if version_req::parse(&pack.version).is_none() {
            add(
                &mut issues,
                PackCode::InvalidVersion,
                "version",
                "Pack version must be a semantic version.",
            );
        }

        if pack.display_name.trim().is_empty() || pack.display_name.chars().count() > 120 {
            add(
                &mut issues,
                PackCode::DisplayName,
                "displayName",
                "Display name is required and must not exceed 120 characters.",
            );
        }

        validate_min_app_version(&pack.min_app_version, current_app_version, &mut issues);
        validate_gpu_routes(&pack.gpu_routes, &mut issues);

        let targets = validate_files(&pack.files, &mut issues);
        validate_owned_directories(&pack.owned_directories, &targets, &mut issues);
        validate_ini(pack.ini.as_ref(), &targets, &mut issues);
        validate_steps(&pack.steps, pack.trust, &mut issues);

        PackValidation::new(issues)
    }
}

fn validate_min_app_version(
    min_app_version: &str,
    current_app_version: Option<&str>,
    issues: &mut Vec<PackIssue>,
) {
    if min_app_version.trim().is_empty() {
        return;
    }

    if version_req::parse(min_app_version).is_none() {
        add(
            issues,
            PackCode::MinAppVersion,
            "minAppVersion",
            "minAppVersion must be a semantic version.",
        );
        return;
    }

    let Some(current) = current_app_version.filter(|value| !value.trim().is_empty()) else {
        return;
    };
    let Some(ordering) = version_req::compare_core(min_app_version, current) else {
        return;
    };

    if ordering.is_gt() {
        add(
            issues,
            PackCode::MinAppTooNew,
            "minAppVersion",
            format!(
                "Pack requires app version {min_app_version} but the current version is {current}."
            ),
        );
    }
}

fn validate_gpu_routes(routes: &[String], issues: &mut Vec<PackIssue>) {
    if routes.is_empty() {
        add(
            issues,
            PackCode::GpuRoutes,
            "gpuRoutes",
            "At least one GPU route is required.",
        );
        return;
    }

    let mut seen = HashSet::new();
    for (index, route) in routes.iter().enumerate() {
        let field = format!("gpuRoutes[{index}]");

        if route.trim().is_empty() || !contains_ci(&PackValidator::ALLOWED_GPU_ROUTES, route) {
            add(
                issues,
                PackCode::GpuRoutes,
                field,
                format!("Unknown GPU route '{route}'."),
            );
            continue;
        }

        if !seen.insert(route.to_ascii_lowercase()) {
            add(
                issues,
                PackCode::DuplicateGpuRoute,
                field,
                format!("Duplicate GPU route '{route}'."),
            );
        }
    }
}

pub(super) fn normalize_path(
    value: &str,
    field: &str,
    issues: &mut Vec<PackIssue>,
) -> Option<String> {
    match SafePath::normalize_relative(value) {
        Ok(normalized) => Some(normalized),
        Err(error) => {
            add(issues, PackCode::Path, field, reason(&error).to_string());
            None
        }
    }
}

pub(super) fn is_sha256(value: &str) -> bool {
    value.len() == 64 && value.bytes().all(|byte| byte.is_ascii_hexdigit())
}

pub(super) fn add(
    issues: &mut Vec<PackIssue>,
    code: PackCode,
    field: impl Into<String>,
    diagnostic: impl Into<String>,
) {
    issues.push(PackIssue::new(code, field, diagnostic));
}

fn reason(error: &AppError) -> &str {
    error
        .context
        .iter()
        .find(|(key, _)| key == "reason")
        .map_or("Invalid path.", |(_, value)| value.as_str())
}

fn contains_ci(allowed: &[&str], value: &str) -> bool {
    allowed.iter().any(|item| item.eq_ignore_ascii_case(value))
}

fn is_valid_id(value: &str) -> bool {
    let bytes = value.as_bytes();
    if !(2..=64).contains(&bytes.len()) {
        return false;
    }

    let first = bytes[0];
    if !(first.is_ascii_lowercase() || first.is_ascii_digit()) {
        return false;
    }

    bytes[1..].iter().all(|byte| {
        byte.is_ascii_lowercase() || byte.is_ascii_digit() || matches!(byte, b'.' | b'_' | b'-')
    })
}
