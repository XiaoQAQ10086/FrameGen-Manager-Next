use std::collections::BTreeMap;
use std::fs;
use std::path::{Path, PathBuf};
use std::time::{SystemTime, UNIX_EPOCH};

use fgm_domain::error::ErrorCode;
use fgm_domain::pack::{AssetPack, AssetPackFile, AssetPackIni, IniSetting, PackTrust};
use fgm_infra::deployment::PackDeploymentPlanner;
use fgm_infra::hashing::FileHasher;

#[test]
fn selects_the_first_proxy_slot_by_default() {
    let fixture = Fixture::new("planner-default");

    let plan = fixture.plan(&BTreeMap::new(), &[]).unwrap();

    assert_eq!(plan.proxy_slot.as_deref(), Some("version.dll"));
    let paths: Vec<&str> = plan
        .files
        .iter()
        .map(|file| file.relative_path.as_str())
        .collect();
    assert!(paths.contains(&"version.dll"));
    assert!(paths.contains(&"runtime.dll"));
    assert!(paths.contains(&"game.ini"));
    assert!(!paths.contains(&"winmm.dll"));
}

#[test]
fn selects_the_requested_proxy_slot() {
    let fixture = Fixture::new("planner-requested");
    let options = BTreeMap::from([("proxySlot".to_string(), "WINMM.DLL".to_string())]);

    let plan = fixture.plan(&options, &[]).unwrap();

    assert_eq!(plan.proxy_slot.as_deref(), Some("winmm.dll"));
    let paths: Vec<&str> = plan
        .files
        .iter()
        .map(|file| file.relative_path.as_str())
        .collect();
    assert!(paths.contains(&"winmm.dll"));
    assert!(!paths.contains(&"version.dll"));
}

#[test]
fn rejects_an_unknown_proxy_slot() {
    let fixture = Fixture::new("planner-bad-slot");
    let options = BTreeMap::from([("proxySlot".to_string(), "dxgi.dll".to_string())]);

    let error = fixture.plan(&options, &[]).unwrap_err();

    assert_eq!(error.code, ErrorCode::InvalidArgument);
}

#[test]
fn generates_the_merged_ini_with_crlf() {
    let fixture = Fixture::new("planner-ini");
    let overrides = vec![setting("General", "K", "2")];

    let plan = fixture.plan(&BTreeMap::new(), &overrides).unwrap();
    let ini = plan
        .files
        .iter()
        .find(|file| file.relative_path == "game.ini")
        .expect("ini plan");

    let content = String::from_utf8(ini.content.clone().unwrap()).unwrap();
    assert_eq!(content, "[General]\r\nK=2\r\n");
}

#[test]
fn preserves_existing_ini_comments_when_merging() {
    let fixture = Fixture::new("planner-ini-existing");
    fs::create_dir_all(&fixture.target).unwrap();
    fs::write(
        fixture.target.join("game.ini"),
        "; keep me\n[General]\nK=5\n",
    )
    .unwrap();
    let overrides = vec![setting("General", "K", "1")];

    let plan = fixture.plan(&BTreeMap::new(), &overrides).unwrap();
    let ini = plan
        .files
        .iter()
        .find(|file| file.relative_path == "game.ini")
        .unwrap();
    let content = String::from_utf8(ini.content.clone().unwrap()).unwrap();

    assert!(content.contains("; keep me"));
    assert!(content.contains("K=1"));
}

#[test]
fn rejects_an_ini_template_hash_mismatch() {
    let fixture = Fixture::new("planner-template");
    let mut pack = fixture.pack();
    pack.ini.as_mut().unwrap().template_sha256 = Some("a".repeat(64));

    let error = PackDeploymentPlanner::plan(
        &pack,
        &fixture.pack_dir,
        &fixture.target,
        &BTreeMap::new(),
        &[],
        Some("2.3.4"),
    )
    .unwrap_err();

    assert_eq!(error.code, ErrorCode::PackHashMismatch);
}

#[test]
fn planned_files_can_be_applied_and_restored() {
    let fixture = Fixture::new("planner-e2e");
    fs::write(fixture.target.join("version.dll"), b"original").unwrap();
    let plan = fixture.plan(&BTreeMap::new(), &[]).unwrap();
    let backups = fixture.root.join("backups");
    let locks = fixture.root.join("locks");

    let report = fgm_infra::deployment::DeploymentEngine::apply(
        &plan.files,
        &fixture.target,
        "framegen.sm86",
        "framegen.sm86",
        "0.4.2",
        &BTreeMap::new(),
        &backups,
        &locks,
        &std::sync::atomic::AtomicBool::new(false),
    )
    .unwrap();
    assert_eq!(report.files_written, plan.files.len());
    assert_eq!(
        fs::read_to_string(fixture.target.join("version.dll")).unwrap(),
        "proxy-v"
    );
    assert!(fixture.target.join("runtime.dll").is_file());
    assert!(fixture.target.join("game.ini").is_file());

    let restore = fgm_infra::deployment::DeploymentEngine::restore(
        "framegen.sm86",
        &fixture.target,
        &backups,
        &locks,
    )
    .unwrap();
    assert!(restore.conflicts.is_empty());
    assert_eq!(
        fs::read_to_string(fixture.target.join("version.dll")).unwrap(),
        "original"
    );
    assert!(!fixture.target.join("runtime.dll").exists());
    assert!(!fixture.target.join("game.ini").exists());
}

struct Fixture {
    root: PathBuf,
    pack_dir: PathBuf,
    target: PathBuf,
}

impl Fixture {
    fn new(name: &str) -> Self {
        let root = unique_dir(name);
        let pack_dir = root.join("pack");
        let target = root.join("game");
        fs::create_dir_all(&pack_dir).unwrap();
        fs::create_dir_all(&target).unwrap();

        fs::write(pack_dir.join("version.dll"), b"proxy-v").unwrap();
        fs::write(pack_dir.join("winmm.dll"), b"proxy-w").unwrap();
        fs::write(pack_dir.join("runtime.dll"), b"runtime").unwrap();
        fs::write(pack_dir.join("template.ini"), b"[General]\nK=1\n").unwrap();

        Self {
            root,
            pack_dir,
            target,
        }
    }

    fn pack(&self) -> AssetPack {
        let template = self.pack_dir.join("template.ini");
        AssetPack {
            schema_version: 1,
            id: "framegen.sm86".to_string(),
            version: "0.4.2".to_string(),
            display_name: "FrameGen SM86".to_string(),
            min_app_version: "2.0.0".to_string(),
            gpu_routes: vec!["Sm86".to_string()],
            files: vec![
                Some(file(&self.pack_dir, "version.dll", "proxy", true)),
                Some(file(&self.pack_dir, "winmm.dll", "proxy", true)),
                Some(file(&self.pack_dir, "runtime.dll", "runtime", false)),
            ],
            owned_directories: Vec::new(),
            ini: Some(AssetPackIni {
                target: "game.ini".to_string(),
                template: "template.ini".to_string(),
                mode: "merge".to_string(),
                template_sha256: Some(FileHasher::sha256_file(&template).unwrap()),
                template_size: Some(fs::metadata(&template).unwrap().len() as i64),
                stock_values: vec![Some(setting("General", "K", "1"))],
            }),
            steps: Vec::new(),
            trust: PackTrust::Bundled,
        }
    }

    fn plan(
        &self,
        options: &BTreeMap<String, String>,
        overrides: &[IniSetting],
    ) -> Result<fgm_infra::deployment::PackPlan, fgm_domain::error::AppError> {
        PackDeploymentPlanner::plan(
            &self.pack(),
            &self.pack_dir,
            &self.target,
            options,
            overrides,
            Some("2.3.4"),
        )
    }
}

impl Drop for Fixture {
    fn drop(&mut self) {
        let _ = fs::remove_dir_all(&self.root);
    }
}

fn file(pack_dir: &Path, name: &str, role: &str, proxy_slot: bool) -> AssetPackFile {
    let path = pack_dir.join(name);
    AssetPackFile {
        source: name.to_string(),
        target: name.to_string(),
        role: role.to_string(),
        required: true,
        proxy_slot,
        sha256: Some(FileHasher::sha256_file(&path).unwrap()),
        size: Some(fs::metadata(&path).unwrap().len() as i64),
    }
}

fn setting(section: &str, key: &str, value: &str) -> IniSetting {
    IniSetting {
        section: section.to_string(),
        key: key.to_string(),
        value: value.to_string(),
    }
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
