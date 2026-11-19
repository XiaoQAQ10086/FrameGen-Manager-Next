use std::fs;
use std::path::{Path, PathBuf};
use std::time::{SystemTime, UNIX_EPOCH};

use fgm_domain::error::ErrorCode;
use fgm_infra::dlss5::ReShadeInstaller;

#[test]
fn installs_module_and_default_ini() {
    let dir = TestDir::new("reshade-install");
    let target = dir.path().join("game");
    fs::create_dir_all(&target).unwrap();

    ReShadeInstaller::install_module(&target, "dxgi.dll", b"module").unwrap();

    assert_eq!(fs::read(target.join("dxgi.dll")).unwrap(), b"module");
    assert!(target.join("ReShade.ini").is_file());
}

#[test]
fn refuses_to_overwrite_a_non_reshade_module() {
    let dir = TestDir::new("reshade-conflict");
    let target = dir.path().join("game");
    fs::create_dir_all(&target).unwrap();
    fs::write(target.join("dxgi.dll"), b"other").unwrap();

    let error = ReShadeInstaller::install_module(&target, "dxgi.dll", b"module").unwrap_err();

    assert_eq!(error.code, ErrorCode::Conflict);
    assert_eq!(fs::read(target.join("dxgi.dll")).unwrap(), b"other");
}

struct TestDir(PathBuf);

impl TestDir {
    fn new(name: &str) -> Self {
        let nanos = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .unwrap()
            .as_nanos();
        let path = std::env::temp_dir().join(format!("fgm-{name}-{}-{nanos}", std::process::id()));
        fs::create_dir_all(&path).unwrap();
        Self(path)
    }

    fn path(&self) -> &Path {
        &self.0
    }
}

impl Drop for TestDir {
    fn drop(&mut self) {
        let _ = fs::remove_dir_all(&self.0);
    }
}
