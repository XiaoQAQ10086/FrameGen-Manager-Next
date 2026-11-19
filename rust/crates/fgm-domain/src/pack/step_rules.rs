use std::collections::BTreeMap;

use super::asset_pack_step::AssetPackStep;
use super::pack_code::PackCode;
use super::pack_issue::PackIssue;
use super::pack_trust::PackTrust;
use super::pack_validator::{add, is_sha256, normalize_path};

pub(super) fn validate_steps(
    steps: &[Option<AssetPackStep>],
    trust: PackTrust,
    issues: &mut Vec<PackIssue>,
) {
    for (index, entry) in steps.iter().enumerate() {
        let field = format!("steps[{index}]");
        let Some(step) = entry else {
            add(
                issues,
                PackCode::NullEntry,
                &field,
                "Step entry must not be null.",
            );
            continue;
        };

        let arguments = normalized_arguments(step);

        if step.kind.eq_ignore_ascii_case("copyFiles") || step.kind.eq_ignore_ascii_case("writeIni")
        {
            if !arguments.is_empty() {
                add(
                    issues,
                    PackCode::StepArguments,
                    &field,
                    "This step does not accept arguments.",
                );
            }
            continue;
        }

        if step.kind.eq_ignore_ascii_case("removeStaleFiles") {
            validate_arguments(&arguments, &["target"], &field, issues);
            require_path(&arguments, "target", &field, issues);
            continue;
        }

        if step.kind.eq_ignore_ascii_case("runInstaller") {
            if trust != PackTrust::Bundled {
                add(
                    issues,
                    PackCode::InstallerTrust,
                    &field,
                    "runInstaller is only allowed for bundled packs.",
                );
            }

            validate_arguments(
                &arguments,
                &["source", "sha256", "arguments"],
                &field,
                issues,
            );
            require_path(&arguments, "source", &field, issues);

            if !arguments
                .get("sha256")
                .is_some_and(|value| is_sha256(value))
            {
                add(
                    issues,
                    PackCode::StepArguments,
                    format!("{field}.sha256"),
                    "runInstaller requires a 64-character SHA-256.",
                );
            }
            continue;
        }

        add(
            issues,
            PackCode::StepType,
            format!("{field}.type"),
            format!("Unknown step type '{}'.", step.kind),
        );
    }
}

fn normalized_arguments(step: &AssetPackStep) -> BTreeMap<String, String> {
    step.arguments
        .as_ref()
        .map(|arguments| {
            arguments
                .iter()
                .map(|(key, value)| (key.to_ascii_lowercase(), value.clone()))
                .collect()
        })
        .unwrap_or_default()
}

fn validate_arguments(
    arguments: &BTreeMap<String, String>,
    allowed: &[&str],
    field: &str,
    issues: &mut Vec<PackIssue>,
) {
    for key in arguments.keys() {
        if !allowed.contains(&key.as_str()) {
            add(
                issues,
                PackCode::StepArguments,
                format!("{field}.{key}"),
                format!("Unknown argument '{key}'."),
            );
        }
    }
}

fn require_path(
    arguments: &BTreeMap<String, String>,
    key: &str,
    field: &str,
    issues: &mut Vec<PackIssue>,
) {
    match arguments.get(key) {
        Some(value) if !value.trim().is_empty() => {
            normalize_path(value, &format!("{field}.{key}"), issues);
        }
        _ => add(
            issues,
            PackCode::StepArguments,
            format!("{field}.{key}"),
            format!("Argument '{key}' is required."),
        ),
    }
}
