use std::collections::HashSet;

use super::asset_pack_file::AssetPackFile;
use super::pack_code::PackCode;
use super::pack_issue::PackIssue;
use super::pack_validator::{add, is_sha256, normalize_path};

const ALLOWED_ROLES: [&str; 12] = [
    "proxy",
    "runtime",
    "sr",
    "addon",
    "model",
    "shader",
    "effect",
    "texture",
    "preset",
    "installer",
    "config",
    "other",
];

pub(super) fn validate_files(
    files: &[Option<AssetPackFile>],
    issues: &mut Vec<PackIssue>,
) -> HashSet<String> {
    let mut targets = HashSet::new();

    if files.is_empty() {
        add(
            issues,
            PackCode::Files,
            "files",
            "At least one file is required.",
        );
        return targets;
    }

    for (index, entry) in files.iter().enumerate() {
        let field = format!("files[{index}]");
        let Some(file) = entry else {
            add(
                issues,
                PackCode::NullEntry,
                field,
                "File entry must not be null.",
            );
            continue;
        };

        let target = normalize_path(&file.target, &format!("{field}.target"), issues);
        normalize_path(&file.source, &format!("{field}.source"), issues);

        if let Some(target) = &target
            && !targets.insert(target.to_ascii_lowercase())
        {
            add(
                issues,
                PackCode::DuplicateTarget,
                format!("{field}.target"),
                format!("Duplicate target '{target}'."),
            );
        }

        if file.role.trim().is_empty() || !contains_ci(&ALLOWED_ROLES, &file.role) {
            add(
                issues,
                PackCode::Role,
                format!("{field}.role"),
                format!("Unknown file role '{}'.", file.role),
            );
        }

        if file.proxy_slot && !file.role.eq_ignore_ascii_case("proxy") {
            add(
                issues,
                PackCode::ProxySlot,
                format!("{field}.proxySlot"),
                "proxySlot is only valid for role 'proxy'.",
            );
        }

        if !file.sha256.as_deref().is_some_and(is_sha256) {
            add(
                issues,
                PackCode::Sha256,
                format!("{field}.sha256"),
                "SHA-256 must be 64 hexadecimal characters.",
            );
        }

        if !file.size.is_some_and(|size| size >= 0) {
            add(
                issues,
                PackCode::Size,
                format!("{field}.size"),
                "File size must be a non-negative number.",
            );
        }
    }

    targets
}

pub(super) fn validate_owned_directories(
    directories: &[String],
    file_targets: &HashSet<String>,
    issues: &mut Vec<PackIssue>,
) {
    let mut seen = HashSet::new();

    for (index, directory) in directories.iter().enumerate() {
        let field = format!("ownedDirectories[{index}]");
        let Some(normalized) = normalize_path(directory, &field, issues) else {
            continue;
        };
        let key = normalized.to_ascii_lowercase();

        if !seen.insert(key.clone()) {
            add(
                issues,
                PackCode::DuplicateOwnedDirectory,
                &field,
                format!("Duplicate owned directory '{normalized}'."),
            );
        }

        if file_targets.contains(&key) {
            add(
                issues,
                PackCode::OwnedDirectoryConflict,
                &field,
                format!("Owned directory '{normalized}' conflicts with a file target."),
            );
        }
    }
}

fn contains_ci(allowed: &[&str], value: &str) -> bool {
    allowed.iter().any(|item| item.eq_ignore_ascii_case(value))
}
