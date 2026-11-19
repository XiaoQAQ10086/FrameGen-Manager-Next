use std::collections::HashSet;

use super::asset_pack_ini::AssetPackIni;
use super::pack_code::PackCode;
use super::pack_issue::PackIssue;
use super::pack_validator::{add, is_sha256, normalize_path};

pub(super) fn validate_ini(
    ini: Option<&AssetPackIni>,
    file_targets: &HashSet<String>,
    issues: &mut Vec<PackIssue>,
) {
    let Some(ini) = ini else {
        return;
    };

    let target = normalize_path(&ini.target, "ini.target", issues);
    normalize_path(&ini.template, "ini.template", issues);

    if let Some(target) = &target
        && file_targets.contains(&target.to_ascii_lowercase())
    {
        add(
            issues,
            PackCode::IniTargetConflict,
            "ini.target",
            format!("INI target '{target}' conflicts with a file target."),
        );
    }

    if ini.mode.trim().is_empty()
        || !(ini.mode.eq_ignore_ascii_case("merge") || ini.mode.eq_ignore_ascii_case("replace"))
    {
        add(
            issues,
            PackCode::IniMode,
            "ini.mode",
            format!("Unknown INI mode '{}'.", ini.mode),
        );
    }

    if !ini.template_sha256.as_deref().is_some_and(is_sha256) {
        add(
            issues,
            PackCode::IniTemplateSha256,
            "ini.templateSha256",
            "INI template SHA-256 must be 64 hexadecimal characters.",
        );
    }

    if !ini.template_size.is_some_and(|size| size >= 0) {
        add(
            issues,
            PackCode::IniTemplateSize,
            "ini.templateSize",
            "INI template size must be a non-negative number.",
        );
    }

    let mut seen = HashSet::new();
    for (index, entry) in ini.stock_values.iter().enumerate() {
        let field = format!("ini.stockValues[{index}]");
        let Some(setting) = entry else {
            add(
                issues,
                PackCode::StockValue,
                field,
                "Stock value requires a section, key and value.",
            );
            continue;
        };

        if setting.section.trim().is_empty() || setting.key.trim().is_empty() {
            add(
                issues,
                PackCode::StockValue,
                field,
                "Stock value requires a section, key and value.",
            );
            continue;
        }

        let key = format!(
            "{}\u{1f}{}",
            setting.section.to_ascii_lowercase(),
            setting.key.to_ascii_lowercase()
        );
        if !seen.insert(key) {
            add(
                issues,
                PackCode::DuplicateStockValue,
                field,
                format!(
                    "Duplicate stock value '{}/{}'.",
                    setting.section, setting.key
                ),
            );
        }
    }
}
