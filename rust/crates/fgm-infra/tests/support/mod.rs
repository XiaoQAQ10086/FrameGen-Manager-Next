use std::fs;
use std::path::PathBuf;
use std::time::{SystemTime, UNIX_EPOCH};

use fgm_domain::library::GraphicsApi;
use fgm_infra::deployment::RestoreReport;
use fgm_infra::dlss5::Dlss5Service;
use fgm_infra::hashing::FileHasher;
use fgm_infra::paths::AppPaths;

pub struct Fixture {
    pub root: PathBuf,
    pub pack: PathBuf,
    pub target: PathBuf,
}

impl Fixture {
    pub fn new(name: &str) -> Self {
        let root = unique_dir(name);
        let pack = root.join("pack");
        let target = root.join("game");
        fs::create_dir_all(&pack).unwrap();
        fs::create_dir_all(&target).unwrap();

        fs::write(pack.join("shader.fx"), b"shader").unwrap();
        fs::create_dir_all(pack.join("nested")).unwrap();
        fs::write(pack.join("nested").join("data.bin"), b"nested").unwrap();
        let shader_hash = FileHasher::sha256_file(&pack.join("shader.fx")).unwrap();
        let nested_hash = FileHasher::sha256_file(&pack.join("nested").join("data.bin")).unwrap();
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
  "files": [{{"source":"shader.fx","target":"shader.fx","role":"shader","sha256":"{shader_hash}","size":6}},{{"source":"nested/data.bin","target":"nested/data.bin","role":"shader","sha256":"{nested_hash}","size":6}}],
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

    pub fn install(&self) -> Result<fgm_infra::dlss5::Dlss5Report, fgm_domain::error::AppError> {
        Dlss5Service::install(
            &self.pack,
            &self.target.join("game.exe"),
            &self.target,
            GraphicsApi::Dx12,
            "2.3.4",
            &self.paths(),
        )
    }

    pub fn uninstall(&self) -> Result<RestoreReport, fgm_domain::error::AppError> {
        Dlss5Service::uninstall(&self.target, &self.paths())
    }

    pub fn paths(&self) -> AppPaths {
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

#[allow(dead_code)]
pub fn build_setup() -> Vec<u8> {
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
