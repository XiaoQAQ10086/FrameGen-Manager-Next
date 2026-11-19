use std::collections::BTreeMap;
use std::fs;
use std::path::PathBuf;
use std::sync::atomic::AtomicBool;
use std::time::{SystemTime, UNIX_EPOCH};

use fgm_domain::error::ErrorCode;
use fgm_domain::gpu::GpuRoute;
use fgm_infra::deployment::{DeploymentEngine, DeploymentFile};
use fgm_infra::hashing::FileHasher;
use fgm_infra::paths::AppPaths;
use fgm_infra::smooth_motion::SmoothMotionComponents;

#[test]
fn maps_routes_to_packs() {
    assert_eq!(
        SmoothMotionComponents::pack_id(GpuRoute::Sm86),
        Some("smooth-motion.rtx30")
    );
    assert_eq!(
        SmoothMotionComponents::pack_id(GpuRoute::Sm75),
        Some("smooth-motion.rtx20")
    );
    assert_eq!(SmoothMotionComponents::pack_id(GpuRoute::NotNeeded), None);
}

#[test]
fn deploys_and_restores_components() {
    let fixture = Fixture::new("sm-components");
    let paths = fixture.paths();

    let report =
        SmoothMotionComponents::deploy_from(&fixture.pack, &fixture.target, None, "2.3.4", &paths)
            .unwrap();
    assert_eq!(report.files_written, 1);
    assert_eq!(
        fs::read_to_string(fixture.target.join("version.dll")).unwrap(),
        "runtime"
    );

    SmoothMotionComponents::restore(&fixture.target, &paths).unwrap();
    assert!(!fixture.target.join("version.dll").exists());
}

#[test]
fn refuses_when_frame_generation_is_installed() {
    let fixture = Fixture::new("sm-conflict");
    let paths = fixture.paths();
    fixture.install_framegen(&paths);

    let error =
        SmoothMotionComponents::deploy_from(&fixture.pack, &fixture.target, None, "2.3.4", &paths)
            .unwrap_err();

    assert_eq!(error.code, ErrorCode::Conflict);
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
        fs::write(pack.join("version.dll"), b"runtime").unwrap();
        let hash = FileHasher::sha256_file(&pack.join("version.dll")).unwrap();
        let size = fs::metadata(pack.join("version.dll")).unwrap().len();
        fs::write(
            pack.join("pack.json"),
            format!(
                r#"{{
  "schemaVersion": 1,
  "id": "smooth-motion.rtx30",
  "version": "1.0.0",
  "displayName": "SM Test",
  "minAppVersion": "1.0.0",
  "gpuRoutes": ["Sm86"],
  "files": [{{"source":"version.dll","target":"version.dll","role":"runtime","sha256":"{hash}","size":{size}}}],
  "trust": "bundled"
}}"#
            ),
        )
        .unwrap();
        Self { root, pack, target }
    }

    fn paths(&self) -> AppPaths {
        let paths = AppPaths::new(self.root.join("data"), false).unwrap();
        paths.ensure_created().unwrap();
        paths
    }

    fn install_framegen(&self, paths: &AppPaths) {
        let source = self.root.join("framegen.dll");
        fs::write(&source, b"framegen").unwrap();
        let file = DeploymentFile {
            relative_path: "framegen.dll".to_string(),
            source: Some(source.clone()),
            content: None,
            sha256: FileHasher::sha256_file(&source).unwrap(),
            size: 8,
            role: "runtime".to_string(),
        };
        DeploymentEngine::apply(
            &[file],
            &self.target,
            "framegen",
            "framegen.sm86",
            "0.4.2",
            &BTreeMap::new(),
            &[],
            None,
            &paths.backups(),
            &paths.state().join("locks"),
            &AtomicBool::new(false),
        )
        .unwrap();
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
