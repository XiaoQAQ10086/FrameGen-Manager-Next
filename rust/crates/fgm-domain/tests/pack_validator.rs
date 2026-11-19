use std::collections::BTreeMap;

use fgm_domain::pack::{
    AssetPack, AssetPackFile, AssetPackIni, AssetPackStep, IniSetting, PackCode, PackTrust,
    PackValidation, PackValidator,
};

#[test]
fn valid_pack_passes() {
    let validation = PackValidator::validate(&valid_pack(), Some("2.3.4"));
    assert!(validation.is_valid(), "{:?}", validation.issues());
}

#[test]
fn schema_version_must_be_one() {
    let mut pack = valid_pack();
    pack.schema_version = 2;
    assert_has(&validate(&pack), PackCode::SchemaVersion);
}

#[test]
fn invalid_id_is_rejected() {
    for id in ["Bad Pack", "a", "UPPER", "-leading"] {
        let mut pack = valid_pack();
        pack.id = id.to_string();
        assert_has(&validate(&pack), PackCode::InvalidId);
    }
}

#[test]
fn invalid_version_is_rejected() {
    for version in ["latest", "1.2", "1.2.3-"] {
        let mut pack = valid_pack();
        pack.version = version.to_string();
        assert_has(&validate(&pack), PackCode::InvalidVersion);
    }
}

#[test]
fn display_name_is_required_and_bounded() {
    let mut pack = valid_pack();
    pack.display_name = " ".to_string();
    assert_has(&validate(&pack), PackCode::DisplayName);

    pack.display_name = "x".repeat(121);
    assert_has(&validate(&pack), PackCode::DisplayName);
}

#[test]
fn min_app_version_must_be_semver() {
    let mut pack = valid_pack();
    pack.min_app_version = "two".to_string();
    assert_has(&validate(&pack), PackCode::MinAppVersion);
}

#[test]
fn newer_min_app_version_is_rejected() {
    let mut pack = valid_pack();
    pack.min_app_version = "2.1.0".to_string();
    assert_has(
        &PackValidator::validate(&pack, Some("2.0.0")),
        PackCode::MinAppTooNew,
    );
}

#[test]
fn gpu_routes_must_be_known_and_unique() {
    let mut pack = valid_pack();
    pack.gpu_routes.clear();
    assert_has(&validate(&pack), PackCode::GpuRoutes);

    pack.gpu_routes = vec!["RTX5090".to_string()];
    assert_has(&validate(&pack), PackCode::GpuRoutes);

    pack.gpu_routes = vec!["Sm86".to_string(), "sm86".to_string()];
    assert_has(&validate(&pack), PackCode::DuplicateGpuRoute);
}

#[test]
fn files_must_not_be_empty() {
    let mut pack = valid_pack();
    pack.files.clear();
    assert_has(&validate(&pack), PackCode::Files);
}

#[test]
fn unsafe_paths_are_rejected() {
    let mut pack = valid_pack();
    pack.files = vec![Some(valid_file_with_target("../version.dll"))];
    assert_has(&validate(&pack), PackCode::Path);

    pack.files = vec![Some(valid_file_with_source("C:\\version.dll"))];
    assert_has(&validate(&pack), PackCode::Path);

    pack.files = vec![Some(valid_file())];
    pack.owned_directories = vec!["../outside".to_string()];
    assert_has(&validate(&pack), PackCode::Path);
}

#[test]
fn duplicate_targets_are_case_insensitive() {
    let mut pack = valid_pack();
    pack.files = vec![
        Some(valid_file()),
        Some(valid_file_with_target("VERSION.DLL")),
    ];
    assert_has(&validate(&pack), PackCode::DuplicateTarget);
}

#[test]
fn file_role_and_proxy_slot_are_checked() {
    let mut pack = valid_pack();
    pack.files = vec![Some(AssetPackFile {
        role: "magic".to_string(),
        ..valid_file()
    })];
    assert_has(&validate(&pack), PackCode::Role);

    pack.files = vec![Some(AssetPackFile {
        role: "runtime".to_string(),
        proxy_slot: true,
        ..valid_file()
    })];
    assert_has(&validate(&pack), PackCode::ProxySlot);
}

#[test]
fn file_hash_and_size_are_required() {
    let mut pack = valid_pack();
    pack.files = vec![Some(AssetPackFile {
        sha256: Some("abc".to_string()),
        ..valid_file()
    })];
    assert_has(&validate(&pack), PackCode::Sha256);

    pack.files = vec![Some(AssetPackFile {
        size: Some(-1),
        ..valid_file()
    })];
    assert_has(&validate(&pack), PackCode::Size);

    pack.files = vec![Some(AssetPackFile {
        size: None,
        ..valid_file()
    })];
    assert_has(&validate(&pack), PackCode::Size);
}

#[test]
fn ini_target_cannot_conflict_with_a_file() {
    let mut pack = valid_pack();
    pack.ini = Some(valid_ini("version.dll"));
    assert_has(&validate(&pack), PackCode::IniTargetConflict);
}

#[test]
fn ini_mode_and_template_metadata_are_checked() {
    let mut pack = valid_pack();
    pack.ini = Some(valid_ini("dlssg_sm86.ini"));
    pack.ini.as_mut().unwrap().mode = "append".to_string();
    assert_has(&validate(&pack), PackCode::IniMode);

    pack.ini = Some(AssetPackIni {
        target: "dlssg_sm86.ini".to_string(),
        template: "template.ini".to_string(),
        mode: "merge".to_string(),
        ..AssetPackIni::default()
    });
    let validation = validate(&pack);
    assert_has(&validation, PackCode::IniTemplateSha256);
    assert_has(&validation, PackCode::IniTemplateSize);
}

#[test]
fn stock_values_must_be_present_and_unique() {
    let mut pack = valid_pack();
    pack.ini = Some(valid_ini("dlssg_sm86.ini"));
    pack.ini.as_mut().unwrap().stock_values = vec![
        Some(IniSetting {
            section: "DLSSG".to_string(),
            key: "Preset".to_string(),
            value: "A".to_string(),
        }),
        Some(IniSetting {
            section: "dlssg".to_string(),
            key: "preset".to_string(),
            value: "B".to_string(),
        }),
    ];
    assert_has(&validate(&pack), PackCode::DuplicateStockValue);

    pack.ini.as_mut().unwrap().stock_values = vec![Some(IniSetting::default())];
    assert_has(&validate(&pack), PackCode::StockValue);
}

#[test]
fn step_types_and_arguments_are_checked() {
    let mut pack = valid_pack();

    pack.steps = vec![Some(AssetPackStep {
        kind: "deleteEverything".to_string(),
        arguments: None,
    })];
    assert_has(&validate(&pack), PackCode::StepType);

    pack.steps = vec![Some(step_with("copyFiles", &[("source", "x.dll")]))];
    assert_has(&validate(&pack), PackCode::StepArguments);

    pack.steps = vec![Some(step_with("removeStaleFiles", &[]))];
    assert_has(&validate(&pack), PackCode::StepArguments);

    pack.steps = vec![Some(step_with("removeStaleFiles", &[("target", "../x")]))];
    assert_has(&validate(&pack), PackCode::Path);

    pack.steps = vec![Some(step_with("runInstaller", &[("source", "setup.exe")]))];
    assert_has(&validate(&pack), PackCode::StepArguments);

    pack.steps = vec![Some(step_with(
        "runInstaller",
        &[
            ("source", "setup.exe"),
            ("sha256", &"b".repeat(64)),
            ("shell", "true"),
        ],
    ))];
    assert_has(&validate(&pack), PackCode::StepArguments);
}

#[test]
fn run_installer_requires_bundled_trust() {
    let mut pack = valid_pack();
    pack.trust = PackTrust::Verified;
    pack.steps = vec![Some(step_with(
        "runInstaller",
        &[("source", "setup.exe"), ("sha256", &"b".repeat(64))],
    ))];
    assert_has(&validate(&pack), PackCode::InstallerTrust);
}

#[test]
fn null_entries_are_reported() {
    let mut pack = valid_pack();
    pack.files = vec![None];
    assert_has(&validate(&pack), PackCode::NullEntry);

    pack.files = vec![Some(valid_file())];
    pack.steps = vec![None];
    assert_has(&validate(&pack), PackCode::NullEntry);
}

#[test]
fn owned_directories_must_be_unique_and_not_files() {
    let mut pack = valid_pack();
    pack.owned_directories = vec!["runtime".to_string(), "RUNTIME".to_string()];
    assert_has(&validate(&pack), PackCode::DuplicateOwnedDirectory);

    pack.owned_directories = vec!["version.dll".to_string()];
    assert_has(&validate(&pack), PackCode::OwnedDirectoryConflict);
}

fn validate(pack: &AssetPack) -> PackValidation {
    PackValidator::validate(pack, Some("2.3.4"))
}

fn assert_has(validation: &PackValidation, code: PackCode) {
    assert!(
        validation.issues().iter().any(|issue| issue.code == code),
        "missing {:?} in {:?}",
        code,
        validation.issues()
    );
}

fn valid_pack() -> AssetPack {
    AssetPack {
        schema_version: 1,
        id: "framegen.sm86".to_string(),
        version: "0.4.2".to_string(),
        display_name: "FrameGen SM86".to_string(),
        min_app_version: "2.0.0".to_string(),
        gpu_routes: vec!["Sm86".to_string()],
        files: vec![Some(valid_file())],
        owned_directories: Vec::new(),
        ini: None,
        steps: Vec::new(),
        trust: PackTrust::Bundled,
    }
}

fn valid_file() -> AssetPackFile {
    AssetPackFile {
        source: "version.dll".to_string(),
        target: "version.dll".to_string(),
        role: "proxy".to_string(),
        required: true,
        proxy_slot: true,
        sha256: Some("a".repeat(64)),
        size: Some(123),
    }
}

fn valid_file_with_target(target: &str) -> AssetPackFile {
    AssetPackFile {
        target: target.to_string(),
        ..valid_file()
    }
}

fn valid_file_with_source(source: &str) -> AssetPackFile {
    AssetPackFile {
        source: source.to_string(),
        ..valid_file()
    }
}

fn valid_ini(target: &str) -> AssetPackIni {
    AssetPackIni {
        target: target.to_string(),
        template: "template.ini".to_string(),
        mode: "merge".to_string(),
        template_sha256: Some("c".repeat(64)),
        template_size: Some(10),
        stock_values: Vec::new(),
    }
}

fn step_with(kind: &str, arguments: &[(&str, &str)]) -> AssetPackStep {
    AssetPackStep {
        kind: kind.to_string(),
        arguments: Some(
            arguments
                .iter()
                .map(|(key, value)| (key.to_string(), value.to_string()))
                .collect::<BTreeMap<_, _>>(),
        ),
    }
}
