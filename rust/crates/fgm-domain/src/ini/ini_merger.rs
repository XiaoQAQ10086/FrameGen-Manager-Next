use crate::pack::IniSetting;

use super::ini_document::IniDocument;

/// Applies pack stock values and user overrides while preserving line structure.
pub struct IniMerger;

impl IniMerger {
    pub fn apply(
        target: &mut IniDocument,
        template: &IniDocument,
        stock: &[IniSetting],
        overrides: &[IniSetting],
    ) {
        for item in stock {
            let desired =
                override_value(overrides, &item.section, &item.key).unwrap_or(item.value.as_str());
            let template_has_key = template.get_value(&item.section, &item.key).is_some();
            let current = target.get_value(&item.section, &item.key);

            if desired == item.value {
                if template_has_key && current != Some(desired) {
                    target.set_value(&item.section, &item.key, desired);
                }
            } else if current != Some(desired) {
                target.set_value(&item.section, &item.key, desired);
            }
        }
    }
}

fn override_value<'a>(overrides: &'a [IniSetting], section: &str, key: &str) -> Option<&'a str> {
    overrides.iter().rev().find_map(|item| {
        (item.section.eq_ignore_ascii_case(section) && item.key.eq_ignore_ascii_case(key))
            .then_some(item.value.as_str())
    })
}
