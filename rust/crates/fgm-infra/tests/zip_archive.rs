use std::fs;
use std::io::Write;
use std::path::{Path, PathBuf};
use std::time::{SystemTime, UNIX_EPOCH};

use fgm_domain::error::ErrorCode;
use fgm_infra::archive::ZipArchive;
use flate2::Compression;
use flate2::write::DeflateEncoder;

#[test]
fn extracts_stored_and_deflated_entries() {
    let data = build_zip(&[
        ("game/dxgi.dll", b"proxy", false),
        ("game/OptiScaler.ini", b"ini", true),
    ]);
    let dir = TestDir::new("zip-extract");

    let count = ZipArchive::extract_to(&data, dir.path()).unwrap();

    assert_eq!(count, 2);
    assert_eq!(
        fs::read(dir.path().join("game/dxgi.dll")).unwrap(),
        b"proxy"
    );
    assert_eq!(
        fs::read(dir.path().join("game/OptiScaler.ini")).unwrap(),
        b"ini"
    );
}

#[test]
fn rejects_path_traversal() {
    let data = build_zip(&[("../escape.txt", b"x", false)]);
    let dir = TestDir::new("zip-traversal");

    let error = ZipArchive::extract_to(&data, dir.path()).unwrap_err();

    assert!(matches!(
        error.code,
        ErrorCode::UnsafePath | ErrorCode::InvalidPath
    ));
    assert!(!dir.path().join("..").join("escape.txt").exists());
}

fn build_zip(entries: &[(&str, &[u8], bool)]) -> Vec<u8> {
    const LOCAL: [u8; 4] = [0x50, 0x4b, 0x03, 0x04];
    const CENTRAL: [u8; 4] = [0x50, 0x4b, 0x01, 0x02];
    const EOCD: [u8; 4] = [0x50, 0x4b, 0x05, 0x06];

    let mut data = vec![0_u8; 512];
    let mut central_entries = Vec::new();

    for (name, content, deflate) in entries {
        let payload = if *deflate {
            let mut encoder = DeflateEncoder::new(Vec::new(), Compression::default());
            encoder.write_all(content).unwrap();
            encoder.finish().unwrap()
        } else {
            content.to_vec()
        };
        let method = if *deflate { 8_u16 } else { 0_u16 };
        let local_offset = (data.len() - 512) as u32;

        data.extend_from_slice(&LOCAL);
        data.extend_from_slice(&20_u16.to_le_bytes());
        data.extend_from_slice(&0_u16.to_le_bytes());
        data.extend_from_slice(&method.to_le_bytes());
        data.extend_from_slice(&0_u16.to_le_bytes());
        data.extend_from_slice(&0_u16.to_le_bytes());
        data.extend_from_slice(&0_u32.to_le_bytes());
        data.extend_from_slice(&(payload.len() as u32).to_le_bytes());
        data.extend_from_slice(&(content.len() as u32).to_le_bytes());
        data.extend_from_slice(&(name.len() as u16).to_le_bytes());
        data.extend_from_slice(&0_u16.to_le_bytes());
        data.extend_from_slice(name.as_bytes());
        data.extend_from_slice(&payload);

        let mut central = Vec::new();
        central.extend_from_slice(&CENTRAL);
        central.extend_from_slice(&20_u16.to_le_bytes());
        central.extend_from_slice(&20_u16.to_le_bytes());
        central.extend_from_slice(&0_u16.to_le_bytes());
        central.extend_from_slice(&method.to_le_bytes());
        central.extend_from_slice(&0_u16.to_le_bytes());
        central.extend_from_slice(&0_u16.to_le_bytes());
        central.extend_from_slice(&0_u32.to_le_bytes());
        central.extend_from_slice(&(payload.len() as u32).to_le_bytes());
        central.extend_from_slice(&(content.len() as u32).to_le_bytes());
        central.extend_from_slice(&(name.len() as u16).to_le_bytes());
        central.extend_from_slice(&0_u16.to_le_bytes());
        central.extend_from_slice(&0_u16.to_le_bytes());
        central.extend_from_slice(&0_u16.to_le_bytes());
        central.extend_from_slice(&0_u16.to_le_bytes());
        central.extend_from_slice(&0_u32.to_le_bytes());
        central.extend_from_slice(&local_offset.to_le_bytes());
        central.extend_from_slice(name.as_bytes());
        central_entries.extend_from_slice(&central);
    }

    let central_offset = (data.len() - 512) as u32;
    data.extend_from_slice(&central_entries);
    let central_size = (data.len() - 512 - central_offset as usize) as u32;

    data.extend_from_slice(&EOCD);
    data.extend_from_slice(&0_u16.to_le_bytes());
    data.extend_from_slice(&0_u16.to_le_bytes());
    data.extend_from_slice(&(entries.len() as u16).to_le_bytes());
    data.extend_from_slice(&(entries.len() as u16).to_le_bytes());
    data.extend_from_slice(&central_size.to_le_bytes());
    data.extend_from_slice(&central_offset.to_le_bytes());
    data.extend_from_slice(&0_u16.to_le_bytes());
    data
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
