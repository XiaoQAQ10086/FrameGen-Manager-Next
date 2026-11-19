use fgm_domain::ini::{IniDocument, IniMerger};
use fgm_domain::pack::IniSetting;

#[test]
fn parses_comments_and_values_case_insensitively() {
    let document = IniDocument::parse("; comment\n[General]\nEnabled = 1\n");

    assert_eq!(document.get_value("general", "enabled"), Some("1"));
    assert!(document.to_text("\n").contains("; comment"));
    assert!(document.to_text("\n").contains("[General]"));
}

#[test]
fn preserves_malformed_lines_and_inline_semicolons() {
    let text = "not a pair\n[Section]\nKey=value;not-comment\n";
    let document = IniDocument::parse(text);

    assert_eq!(
        document.get_value("Section", "Key"),
        Some("value;not-comment")
    );
    assert!(document.to_text("\n").contains("not a pair"));
}

#[test]
fn set_value_updates_in_place_and_keeps_key_casing() {
    let mut document = IniDocument::parse("[General]\nEnabled = 1\nOther=2\n");

    document.set_value("general", "enabled", "0");

    let text = document.to_text("\n");
    assert!(text.contains("Enabled=0"));
    assert!(!text.contains("Enabled = 1"));
    assert!(text.contains("Other=2"));
}

#[test]
fn duplicate_keys_return_and_update_the_last_value() {
    let mut document = IniDocument::parse("[G]\nK=1\nK=2\n");

    assert_eq!(document.get_value("G", "K"), Some("2"));
    document.set_value("G", "K", "3");

    assert_eq!(document.to_text("\n"), "[G]\nK=1\nK=3\n");
}

#[test]
fn output_uses_the_requested_line_endings() {
    let document = IniDocument::parse("[G]\r\nK=1\r\n");

    assert_eq!(document.to_text("\n"), "[G]\nK=1\n");
    assert_eq!(document.to_text("\r\n"), "[G]\r\nK=1\r\n");
}

#[test]
fn supports_sectionless_keys() {
    let mut document = IniDocument::parse("Key=1\n");

    assert_eq!(document.get_value("", "key"), Some("1"));
    document.set_value("", "other", "2");

    assert!(document.to_text("\n").contains("other=2"));
}

#[test]
fn adds_a_missing_key_before_the_next_section() {
    let mut document = IniDocument::parse("[A]\nx=1\n[B]\ny=2\n");

    document.set_value("A", "z", "3");

    assert_eq!(document.to_text("\n"), "[A]\nx=1\nz=3\n[B]\ny=2\n");
}

#[test]
fn adds_a_missing_section_at_the_end() {
    let mut document = IniDocument::parse("[A]\nx=1\n");

    document.set_value("B", "y", "2");

    assert_eq!(document.to_text("\n"), "[A]\nx=1\n\n[B]\ny=2\n");
}

#[test]
fn removes_all_matching_keys() {
    let mut document = IniDocument::parse("[G]\nK=1\nKeep=2\nK=3\n");

    document.remove_value("g", "k");

    assert_eq!(document.to_text("\n"), "[G]\nKeep=2\n");
}

#[test]
fn strips_a_utf8_bom() {
    let document = IniDocument::parse("\u{feff}[G]\nK=1\n");
    assert_eq!(document.get_value("G", "K"), Some("1"));
}

#[test]
fn merger_skips_stock_value_when_template_lacks_the_key() {
    let mut target = IniDocument::parse("[G]\nOther=1\n");
    let template = IniDocument::parse("[G]\nOther=1\n");

    IniMerger::apply(
        &mut target,
        &template,
        &[setting("G", "K", "1")],
        &[setting("G", "K", "1")],
    );

    assert_eq!(target.get_value("G", "K"), None);
}

#[test]
fn merger_restores_stock_value_when_template_differs() {
    let mut target = IniDocument::parse("[G]\nK=0\n");
    let template = IniDocument::parse("[G]\nK=0\n");

    IniMerger::apply(
        &mut target,
        &template,
        &[setting("G", "K", "1")],
        &[setting("G", "K", "1")],
    );

    assert_eq!(target.get_value("G", "K"), Some("1"));
}

#[test]
fn merger_applies_a_different_override() {
    let mut target = IniDocument::parse("[G]\nK=1\n");
    let template = IniDocument::parse("[G]\nK=1\n");

    IniMerger::apply(
        &mut target,
        &template,
        &[setting("G", "K", "1")],
        &[setting("G", "K", "5")],
    );

    assert_eq!(target.get_value("G", "K"), Some("5"));
}

#[test]
fn merger_matches_stock_and_override_case_insensitively() {
    let mut target = IniDocument::parse("[general]\nenabled=0\n");
    let template = IniDocument::parse("[general]\nenabled=0\n");

    IniMerger::apply(
        &mut target,
        &template,
        &[setting("General", "Enabled", "1")],
        &[setting("general", "enabled", "2")],
    );

    assert_eq!(target.get_value("GENERAL", "ENABLED"), Some("2"));
    assert!(target.to_text("\n").contains("enabled=2"));
}

fn setting(section: &str, key: &str, value: &str) -> IniSetting {
    IniSetting {
        section: section.to_string(),
        key: key.to_string(),
        value: value.to_string(),
    }
}
