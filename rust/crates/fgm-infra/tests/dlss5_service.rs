use std::fs;

use fgm_domain::deployment::DeploymentState;
use fgm_domain::error::ErrorCode;
use fgm_infra::dlss5::{Dlss5Service, HotkeyOutcome, HotkeyQueue};
use fgm_infra::pack_json::PackJson;

mod support;
use support::Fixture;

#[test]
fn deploys_pack_and_installs_the_reshade_proxy() {
    let fixture = Fixture::new("dlss5-install");

    let report = fixture.install().unwrap();

    assert_eq!(report.proxy_module, "dxgi.dll");
    assert_eq!(
        fs::read_to_string(fixture.target.join("shader.fx")).unwrap(),
        "shader"
    );
    assert_eq!(
        fs::read(fixture.target.join("dxgi.dll")).unwrap(),
        b"module"
    );
    assert!(fixture.target.join("ReShade.ini").is_file());
}

#[test]
fn refuses_a_foreign_proxy_and_rolls_back_pack_files() {
    let fixture = Fixture::new("dlss5-conflict");
    fs::write(fixture.target.join("dxgi.dll"), b"foreign").unwrap();

    let error = fixture.install().unwrap_err();

    assert_eq!(error.code, ErrorCode::Conflict);
    assert_eq!(
        fs::read(fixture.target.join("dxgi.dll")).unwrap(),
        b"foreign"
    );
    assert!(!fixture.target.join("shader.fx").exists());
}

#[test]
fn uninstall_removes_the_pack_the_proxy_and_the_default_ini() {
    let fixture = Fixture::new("dlss5-uninstall");
    fixture.install().unwrap();

    let report = fixture.uninstall().unwrap();

    assert_eq!(report.conflicts, Vec::<String>::new());
    assert!(!fixture.target.join("shader.fx").exists());
    assert!(!fixture.target.join("nested").exists());
    assert!(!fixture.target.join("dxgi.dll").exists());
    assert!(!fixture.target.join("ReShade.ini").exists());
}

#[test]
fn restore_keeping_reshade_removes_pack_files_only() {
    let fixture = Fixture::new("dlss5-restore-keep-reshade");
    fixture.install().unwrap();
    let paths = fixture.paths();

    let report = Dlss5Service::restore_keeping_reshade(&fixture.target, &paths).unwrap();

    assert!(report.conflicts.is_empty());
    assert!(!fixture.target.join("shader.fx").exists());
    assert!(!fixture.target.join("nested").exists());
    assert!(fixture.target.join("dxgi.dll").is_file());
    assert!(fixture.target.join("ReShade.ini").is_file());
    let pack = PackJson::load(&fixture.pack.join("pack.json")).unwrap();
    let inspection = Dlss5Service::inspect(&fixture.target, Some(&pack), &paths).unwrap();
    assert_eq!(inspection.state, DeploymentState::NotDeployed);
    assert!(inspection.re_shade_present);

    fixture.install().unwrap();
    fixture.uninstall().unwrap();
    assert!(!fixture.target.join("dxgi.dll").exists());
    assert!(!fixture.target.join("ReShade.ini").exists());
}

#[test]
fn uninstall_keeps_a_pack_file_the_user_changed() {
    let fixture = Fixture::new("dlss5-uninstall-conflict");
    fixture.install().unwrap();
    fs::write(fixture.target.join("shader.fx"), b"edited").unwrap();

    let report = fixture.uninstall().unwrap();

    assert_eq!(report.conflicts, vec!["shader.fx".to_string()]);
    assert_eq!(
        fs::read_to_string(fixture.target.join("shader.fx")).unwrap(),
        "edited"
    );
    assert!(!fixture.target.join("dxgi.dll").exists());
}

#[test]
fn uninstall_keeps_a_reshade_ini_that_predates_the_install() {
    let fixture = Fixture::new("dlss5-uninstall-preexisting-ini");
    fs::write(fixture.target.join("ReShade.ini"), b"[GENERAL]\r\n").unwrap();

    fixture.install().unwrap();
    fixture.uninstall().unwrap();

    assert_eq!(
        fs::read_to_string(fixture.target.join("ReShade.ini")).unwrap(),
        "[GENERAL]\r\n"
    );
    assert!(!fixture.target.join("dxgi.dll").exists());
}

#[test]
fn install_refuses_while_legacy_files_exist() {
    let fixture = Fixture::new("dlss5-legacy-refuse");
    fs::write(fixture.target.join("renodx-dlss-SF-1.addon64"), b"legacy").unwrap();

    let error = fixture.install().unwrap_err();

    assert_eq!(error.code, ErrorCode::Conflict);
    assert!(!fixture.target.join("shader.fx").exists());
    assert!(fixture.target.join("renodx-dlss-SF-1.addon64").exists());
}

#[test]
fn clean_legacy_removes_only_known_residue() {
    let fixture = Fixture::new("dlss5-legacy-clean");
    fs::write(fixture.target.join("renodx-dlss-SF-1.addon64"), b"legacy").unwrap();
    fs::write(fixture.target.join("nvngx_dlssnr.dll"), b"legacy").unwrap();
    fs::write(fixture.target.join("keep.dll"), b"keep").unwrap();

    let removed = Dlss5Service::clean_legacy(&fixture.target).unwrap();

    assert_eq!(removed, 2);
    assert!(!fixture.target.join("renodx-dlss-SF-1.addon64").exists());
    assert!(!fixture.target.join("nvngx_dlssnr.dll").exists());
    assert!(fixture.target.join("keep.dll").exists());
}

#[test]
fn inspect_reports_state_proxy_and_legacy_residue() {
    let fixture = Fixture::new("dlss5-inspect");
    let paths = fixture.paths();
    let pack = PackJson::load(&fixture.pack.join("pack.json")).unwrap();

    let before = Dlss5Service::inspect(&fixture.target, Some(&pack), &paths).unwrap();
    assert_eq!(before.state, DeploymentState::NotDeployed);
    assert!(!before.re_shade_present);
    assert!(before.legacy_files.is_empty());

    fs::write(fixture.target.join("renodx-dlss-SF-1.addon64"), b"legacy").unwrap();
    let legacy = Dlss5Service::inspect(&fixture.target, Some(&pack), &paths).unwrap();
    assert_eq!(legacy.legacy_files, vec!["renodx-dlss-SF-1.addon64"]);
    assert_eq!(Dlss5Service::clean_legacy(&fixture.target).unwrap(), 1);

    fixture.install().unwrap();
    let after = Dlss5Service::inspect(&fixture.target, Some(&pack), &paths).unwrap();
    assert_eq!(after.state, DeploymentState::Installed);
    assert_eq!(after.proxy_module.as_deref(), Some("dxgi.dll"));
    assert!(after.re_shade_present);
    assert!(after.legacy_files.is_empty());

    fixture.uninstall().unwrap();
    let removed = Dlss5Service::inspect(&fixture.target, Some(&pack), &paths).unwrap();
    assert_eq!(removed.state, DeploymentState::NotDeployed);
    assert!(!removed.re_shade_present);
}

#[test]
fn install_merges_search_paths_and_preserves_user_settings() {
    let fixture = Fixture::new("dlss5-ini-merge");
    let ini = fixture.target.join("ReShade.ini");
    let original = "[GENERAL]\r\nEffectSearchPaths=.\\custom\\**\r\n\r\n[UPLIFT]\r\nStartEnabled=1\r\n\r\n[INPUT]\r\nKeyOverlay=20,0,0,0\r\n";
    fs::write(&ini, original).unwrap();

    fixture.install().unwrap();

    let text = fs::read_to_string(&ini).unwrap();
    assert!(text.contains("StartEnabled=1"));
    assert!(text.contains(".\\custom\\**"));
    assert!(text.contains(".\\reshade-shaders\\Shaders\\**"));
    assert!(text.contains(".\\reshade-shaders\\Textures\\**"));
    assert!(text.contains("KeyOverlay=20,0,0,0"));

    fixture.uninstall().unwrap();
    assert_eq!(fs::read_to_string(&ini).unwrap(), original);
}

#[test]
fn set_hotkey_keeps_the_deployment_installed() {
    let fixture = Fixture::new("dlss5-hotkey");
    fixture.install().unwrap();
    let paths = fixture.paths();

    let outcome = Dlss5Service::set_hotkey(&fixture.target, 118, None, &paths).unwrap();
    assert_eq!(outcome, fgm_infra::dlss5::HotkeyOutcome::Applied);

    let text = fs::read_to_string(fixture.target.join("ReShade.ini")).unwrap();
    assert!(text.contains("KeyOverlay=118,0,0,0"));
    let pack = PackJson::load(&fixture.pack.join("pack.json")).unwrap();
    let inspection = Dlss5Service::inspect(&fixture.target, Some(&pack), &paths).unwrap();
    assert_eq!(inspection.state, DeploymentState::Installed);
    assert!(Dlss5Service::set_hotkey(&fixture.target, 0, None, &paths).is_err());
}

#[test]
fn hotkey_queue_deduplicates_and_applies_later() {
    let fixture = Fixture::new("dlss5-hotkey-queue");
    fixture.install().unwrap();
    let paths = fixture.paths();
    HotkeyQueue::enqueue(&paths, &fixture.target, None, 117).unwrap();
    HotkeyQueue::enqueue(&paths, &fixture.target, None, 118).unwrap();

    let entries = HotkeyQueue::load(&paths).unwrap();
    assert_eq!(entries.len(), 1);
    assert_eq!(entries[0].virtual_key, 118);

    assert_eq!(Dlss5Service::process_hotkey_queue(&paths).unwrap(), 1);
    assert!(HotkeyQueue::load(&paths).unwrap().is_empty());
    let text = fs::read_to_string(fixture.target.join("ReShade.ini")).unwrap();
    assert!(text.contains("KeyOverlay=118,0,0,0"));
}

#[test]
fn hotkey_queue_drops_entries_without_a_deployment() {
    let fixture = Fixture::new("dlss5-hotkey-queue-missing");
    let paths = fixture.paths();
    HotkeyQueue::enqueue(&paths, &fixture.target, None, 118).unwrap();

    assert_eq!(Dlss5Service::process_hotkey_queue(&paths).unwrap(), 0);
    assert!(HotkeyQueue::load(&paths).unwrap().is_empty());
}

#[cfg(windows)]
#[test]
fn locked_ini_is_queued_and_retried_after_unlock() {
    use std::os::windows::fs::OpenOptionsExt;

    let fixture = Fixture::new("dlss5-hotkey-locked");
    fixture.install().unwrap();
    let paths = fixture.paths();
    let ini = fixture.target.join("ReShade.ini");
    let lock = fs::OpenOptions::new()
        .read(true)
        .write(true)
        .share_mode(0)
        .open(&ini)
        .unwrap();

    let outcome = Dlss5Service::set_hotkey(&fixture.target, 119, None, &paths).unwrap();

    assert_eq!(outcome, HotkeyOutcome::Queued);
    drop(lock);
    assert_eq!(Dlss5Service::process_hotkey_queue(&paths).unwrap(), 1);
    let text = fs::read_to_string(&ini).unwrap();
    assert!(text.contains("KeyOverlay=119,0,0,0"));
}

#[test]
fn set_hotkey_before_deployment_is_applied_on_install() {
    let fixture = Fixture::new("dlss5-hotkey-missing");
    let paths = fixture.paths();

    let outcome = Dlss5Service::set_hotkey(&fixture.target, 118, None, &paths).unwrap();
    assert_eq!(outcome, fgm_infra::dlss5::HotkeyOutcome::Deferred);
    assert!(!fixture.target.join("ReShade.ini").exists());

    fixture.install().unwrap();
    let text = fs::read_to_string(fixture.target.join("ReShade.ini")).unwrap();
    assert!(text.contains("KeyOverlay=118,0,0,0"));
}
