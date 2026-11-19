use std::fs;
use std::path::PathBuf;
use std::time::{SystemTime, UNIX_EPOCH};

use fgm_domain::deployment::DeploymentState;
use fgm_domain::error::ErrorCode;
use fgm_domain::library::GraphicsApi;
use fgm_infra::deployment::RestoreReport;
use fgm_infra::dlss5::Dlss5Service;
use fgm_infra::hashing::FileHasher;
use fgm_infra::pack_json::PackJson;
use fgm_infra::paths::AppPaths;

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

    Dlss5Service::set_hotkey(&fixture.target, 118, &paths).unwrap();

    let text = fs::read_to_string(fixture.target.join("ReShade.ini")).unwrap();
    assert!(text.contains("KeyOverlay=118,0,0,0"));
    let pack = PackJson::load(&fixture.pack.join("pack.json")).unwrap();
    let inspection = Dlss5Service::inspect(&fixture.target, Some(&pack), &paths).unwrap();
    assert_eq!(inspection.state, DeploymentState::Installed);
    assert!(Dlss5Service::set_hotkey(&fixture.target, 0, &paths).is_err());
}

#[test]
fn set_hotkey_requires_an_existing_deployment() {
    let fixture = Fixture::new("dlss5-hotkey-missing");

    let error = Dlss5Service::set_hotkey(&fixture.target, 118, &fixture.paths()).unwrap_err();

    assert_eq!(error.code, ErrorCode::ManifestMissing);
    assert!(!fixture.target.join("ReShade.ini").exists());
}

struct Fixture {
    root: PathBuf,
    pack: PathBuf,
    target: PathBuf,
}

impl Fixture {
    fn new(name: &str) -> Self {
        let root = unique_dir(name);
        let pack = root.join("pack");
        let target = root.join("game");
        fs::create_dir_all(&pack).unwrap();
        fs::create_dir_all(&target).unwrap();

        fs::write(pack.join("shader.fx"), b"shader").unwrap();
        let shader_hash = FileHasher::sha256_file(&pack.join("shader.fx")).unwrap();
        fs::write(pack.join("setup.exe"), build_setup()).unwrap();
        fs::write(
            pack.join("pack.json"),
            format!(
                r#"{{
  "schemaVersion": 1,
  "id": "dlss5.reshade-gitc",
  "version": "1.0.0",
  "displayName": "DLSS5 Test",
  "minAppVersion": "1.0.0",
  "gpuRoutes": ["Sm86"],
  "files": [{{"source":"shader.fx","target":"shader.fx","role":"shader","sha256":"{shader_hash}","size":6}}],
  "steps": [{{"type":"copyFiles"}},{{"type":"runInstaller","arguments":{{"source":"setup.exe","sha256":"{}"}}}}],
  "trust": "bundled"
}}"#,
                "a".repeat(64)
            ),
        )
        .unwrap();
        fs::write(target.join("game.exe"), b"not-a-pe").unwrap();

        Self { root, pack, target }
    }

    fn install(&self) -> Result<fgm_infra::dlss5::Dlss5Report, fgm_domain::error::AppError> {
        Dlss5Service::install(
            &self.pack,
            &self.target.join("game.exe"),
            &self.target,
            GraphicsApi::Dx12,
            "2.3.4",
            &self.paths(),
        )
    }

    fn uninstall(&self) -> Result<RestoreReport, fgm_domain::error::AppError> {
        Dlss5Service::uninstall(&self.target, &self.paths())
    }

    fn paths(&self) -> AppPaths {
        let paths = AppPaths::new(self.root.join("data"), false).unwrap();
        paths.ensure_created().unwrap();
        paths
    }
}

impl Drop for Fixture {
    fn drop(&mut self) {
        let _ = fs::remove_dir_all(&self.root);
    }
}

fn build_setup() -> Vec<u8> {
    const LOCAL: [u8; 4] = [0x50, 0x4b, 0x03, 0x04];
    const CENTRAL: [u8; 4] = [0x50, 0x4b, 0x01, 0x02];
    const EOCD: [u8; 4] = [0x50, 0x4b, 0x05, 0x06];
    let name = "ReShade64.dll";
    let content = b"module";
    let mut data = vec![0_u8; 512];

    data.extend_from_slice(&LOCAL);
    data.extend_from_slice(&20_u16.to_le_bytes());
    data.extend_from_slice(&0_u16.to_le_bytes());
    data.extend_from_slice(&0_u16.to_le_bytes());
    data.extend_from_slice(&0_u16.to_le_bytes());
    data.extend_from_slice(&0_u16.to_le_bytes());
    data.extend_from_slice(&0_u32.to_le_bytes());
    data.extend_from_slice(&(content.len() as u32).to_le_bytes());
    data.extend_from_slice(&(content.len() as u32).to_le_bytes());
    data.extend_from_slice(&(name.len() as u16).to_le_bytes());
    data.extend_from_slice(&0_u16.to_le_bytes());
    data.extend_from_slice(name.as_bytes());
    data.extend_from_slice(content);

    let central_offset = (data.len() - 512) as u32;
    data.extend_from_slice(&CENTRAL);
    data.extend_from_slice(&20_u16.to_le_bytes());
    data.extend_from_slice(&20_u16.to_le_bytes());
    data.extend_from_slice(&0_u16.to_le_bytes());
    data.extend_from_slice(&0_u16.to_le_bytes());
    data.extend_from_slice(&0_u16.to_le_bytes());
    data.extend_from_slice(&0_u16.to_le_bytes());
    data.extend_from_slice(&0_u32.to_le_bytes());
    data.extend_from_slice(&(content.len() as u32).to_le_bytes());
    data.extend_from_slice(&(content.len() as u32).to_le_bytes());
    data.extend_from_slice(&(name.len() as u16).to_le_bytes());
    data.extend_from_slice(&0_u16.to_le_bytes());
    data.extend_from_slice(&0_u16.to_le_bytes());
    data.extend_from_slice(&0_u16.to_le_bytes());
    data.extend_from_slice(&0_u16.to_le_bytes());
    data.extend_from_slice(&0_u32.to_le_bytes());
    data.extend_from_slice(&0_u32.to_le_bytes());
    data.extend_from_slice(name.as_bytes());
    let central_size = (data.len() - 512 - central_offset as usize) as u32;

    data.extend_from_slice(&EOCD);
    data.extend_from_slice(&0_u16.to_le_bytes());
    data.extend_from_slice(&0_u16.to_le_bytes());
    data.extend_from_slice(&1_u16.to_le_bytes());
    data.extend_from_slice(&1_u16.to_le_bytes());
    data.extend_from_slice(&central_size.to_le_bytes());
    data.extend_from_slice(&central_offset.to_le_bytes());
    data.extend_from_slice(&0_u16.to_le_bytes());
    data
}

fn unique_dir(name: &str) -> PathBuf {
    let nanos = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap()
        .as_nanos();
    let path = std::env::temp_dir().join(format!("fgm-{name}-{}-{nanos}", std::process::id()));
    fs::create_dir_all(&path).unwrap();
    path
}
