use std::fs;
use std::io::Write;
use std::path::{Path, PathBuf};
use std::time::{SystemTime, UNIX_EPOCH};

use fgm_domain::error::ErrorCode;
use fgm_infra::anr::{AnrPackageKind, AnrPackageService};
use flate2::Compression;
use flate2::write::DeflateEncoder;

#[test]
fn imports_a_full_package() {
    let dir = TestDir::new("anr-full");
    let zip = dir.path().join("anr-full.zip");
    fs::write(
        &zip,
        build_zip(&[
            ("game/dxgi.dll", b"proxy", false),
            ("game/OptiScaler.ini", b"ini", false),
            ("nr_fg_package.json", br#"{"version":"1.2.3"}"#, true),
        ]),
    )
    .unwrap();
    let data = dir.path().join("data");

    let info = AnrPackageService::import(&zip, &data).unwrap();

    assert_eq!(info.kind, AnrPackageKind::Full);
    assert_eq!(info.version, "1.2.3");
    assert!(info.payload_directory.join("dxgi.dll").is_file());
    assert!(data.join("packs/anr-fg/current.json").is_file());
    assert_eq!(AnrPackageService::load_current(&data).unwrap(), info);
}

#[test]
fn imports_an_addon_package() {
    let dir = TestDir::new("anr-addon");
    let zip = dir.path().join("anr-addon.zip");
    fs::write(
        &zip,
        build_zip(&[
            (
                "Apex Neural Rendering FG/dlssg/nvngx_dlssg.dll",
                b"dll",
                false,
            ),
            ("nr_fg_package.json", br#"{"version":"2.0.0"}"#, false),
        ]),
    )
    .unwrap();
    let data = dir.path().join("data");

    let info = AnrPackageService::import(&zip, &data).unwrap();

    assert_eq!(info.kind, AnrPackageKind::Addon);
    assert_eq!(info.version, "2.0.0");
    assert!(
        info.payload_directory
            .join("Apex Neural Rendering FG/dlssg/nvngx_dlssg.dll")
            .is_file()
    );
}

#[test]
fn rejects_unrelated_archives() {
    let dir = TestDir::new("anr-invalid");
    let zip = dir.path().join("other.zip");
    fs::write(&zip, build_zip(&[("readme.txt", b"x", false)])).unwrap();

    let error = AnrPackageService::import(&zip, &dir.path().join("data")).unwrap_err();

    assert_eq!(error.code, ErrorCode::PackInvalid);
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

        central_entries.extend_from_slice(&CENTRAL);
        central_entries.extend_from_slice(&20_u16.to_le_bytes());
        central_entries.extend_from_slice(&20_u16.to_le_bytes());
        central_entries.extend_from_slice(&0_u16.to_le_bytes());
        central_entries.extend_from_slice(&method.to_le_bytes());
        central_entries.extend_from_slice(&0_u16.to_le_bytes());
        central_entries.extend_from_slice(&0_u16.to_le_bytes());
        central_entries.extend_from_slice(&0_u32.to_le_bytes());
        central_entries.extend_from_slice(&(payload.len() as u32).to_le_bytes());
        central_entries.extend_from_slice(&(content.len() as u32).to_le_bytes());
        central_entries.extend_from_slice(&(name.len() as u16).to_le_bytes());
        central_entries.extend_from_slice(&0_u16.to_le_bytes());
        central_entries.extend_from_slice(&0_u16.to_le_bytes());
        central_entries.extend_from_slice(&0_u16.to_le_bytes());
        central_entries.extend_from_slice(&0_u16.to_le_bytes());
        central_entries.extend_from_slice(&0_u32.to_le_bytes());
        central_entries.extend_from_slice(&local_offset.to_le_bytes());
        central_entries.extend_from_slice(name.as_bytes());
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
