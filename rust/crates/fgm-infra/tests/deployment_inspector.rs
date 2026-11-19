use std::collections::BTreeMap;
use std::fs;
use std::path::PathBuf;
use std::sync::atomic::AtomicBool;
use std::time::{SystemTime, UNIX_EPOCH};

use fgm_domain::deployment::DeploymentState;
use fgm_domain::pack::{AssetPack, AssetPackFile, PackTrust};
use fgm_infra::deployment::{DeploymentEngine, DeploymentFile, DeploymentInspector};
use fgm_infra::hashing::FileHasher;

#[test]
fn inspect_reports_installed_then_conflict_and_partial() {
    let fixture = Fixture::new("inspect-states");
    fixture.deploy();
    let installed = fixture.inspect(None).unwrap();
    assert_eq!(installed.state, DeploymentState::Installed);
    assert!(installed.has_manifest);

    fs::write(fixture.target.join("runtime.dll"), b"modified").unwrap();
    let conflict = fixture.inspect(None).unwrap();
    assert_eq!(conflict.state, DeploymentState::Conflict);
    assert!(!conflict.details.is_empty());

    fs::remove_file(fixture.target.join("runtime.dll")).unwrap();
    let partial = fixture.inspect(None).unwrap();
    assert_eq!(partial.state, DeploymentState::Partial);
}

#[test]
fn inspect_reports_outdated_when_the_pack_version_changes() {
    let fixture = Fixture::new("inspect-outdated");
    fixture.deploy();
    let mut pack = fixture.pack();
    pack.version = "9.9.9".to_string();

    let inspection = fixture.inspect(Some(&pack)).unwrap();

    assert_eq!(inspection.state, DeploymentState::Outdated);
}

#[test]
fn inspect_detects_files_without_a_manifest() {
    let fixture = Fixture::new("inspect-without-manifest");
    fs::copy(
        fixture.source.join("version.dll"),
        fixture.target.join("version.dll"),
    )
    .unwrap();

    let inspection = fixture.inspect(Some(&fixture.pack())).unwrap();

    assert_eq!(inspection.state, DeploymentState::InstalledWithoutManifest);
    assert!(!inspection.has_manifest);
}

#[test]
fn inspect_reports_not_deployed_for_an_empty_target() {
    let fixture = Fixture::new("inspect-empty");
    let inspection = fixture.inspect(Some(&fixture.pack())).unwrap();
    assert_eq!(inspection.state, DeploymentState::NotDeployed);
}

struct Fixture {
    root: PathBuf,
    source: PathBuf,
    target: PathBuf,
    backups: PathBuf,
    locks: PathBuf,
}

impl Fixture {
    fn new(name: &str) -> Self {
        let root = unique_dir(name);
        let source = root.join("pack");
        let target = root.join("game");
        let backups = root.join("backups");
        let locks = root.join("locks");
        fs::create_dir_all(&source).unwrap();
        fs::create_dir_all(&target).unwrap();
        fs::write(source.join("version.dll"), b"new-proxy").unwrap();
        fs::write(source.join("runtime.dll"), b"new-runtime").unwrap();
        Self {
            root,
            source,
            target,
            backups,
            locks,
        }
    }

    fn files(&self) -> Vec<DeploymentFile> {
        ["version.dll", "runtime.dll"]
            .iter()
            .map(|name| {
                let source = self.source.join(name);
                DeploymentFile {
                    relative_path: name.to_string(),
                    source: Some(source.clone()),
                    content: None,
                    sha256: FileHasher::sha256_file(&source).unwrap(),
                    size: fs::metadata(&source).unwrap().len() as i64,
                    role: "runtime".to_string(),
                }
            })
            .collect()
    }

    fn deploy(&self) {
        DeploymentEngine::apply(
            &self.files(),
            &self.target,
            "test.family",
            "test.pack",
            "1.0.0",
            &BTreeMap::new(),
            &[],
            None,
            &self.backups,
            &self.locks,
            &AtomicBool::new(false),
            None,
        )
        .unwrap();
    }

    fn pack(&self) -> AssetPack {
        AssetPack {
            schema_version: 1,
            id: "test.pack".to_string(),
            version: "1.0.0".to_string(),
            display_name: "Test Pack".to_string(),
            min_app_version: "1.0.0".to_string(),
            gpu_routes: vec!["Any".to_string()],
            files: ["version.dll", "runtime.dll"]
                .iter()
                .map(|name| {
                    let source = self.source.join(name);
                    Some(AssetPackFile {
                        source: name.to_string(),
                        target: name.to_string(),
                        role: "runtime".to_string(),
                        required: true,
                        proxy_slot: false,
                        sha256: Some(FileHasher::sha256_file(&source).unwrap()),
                        size: Some(fs::metadata(&source).unwrap().len() as i64),
                    })
                })
                .collect(),
            owned_directories: Vec::new(),
            ini: None,
            steps: Vec::new(),
            trust: PackTrust::Bundled,
        }
    }

    fn inspect(
        &self,
        pack: Option<&AssetPack>,
    ) -> Result<fgm_infra::deployment::DeploymentInspection, fgm_domain::error::AppError> {
        DeploymentInspector::inspect("test.family", &self.target, &self.backups, pack)
    }
}

impl Drop for Fixture {
    fn drop(&mut self) {
        let _ = fs::remove_dir_all(&self.root);
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
