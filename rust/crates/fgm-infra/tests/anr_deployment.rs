use std::fs;
use std::io::Write;
use std::path::PathBuf;
use std::time::{SystemTime, UNIX_EPOCH};

use fgm_infra::anr::{AnrDeploymentService, AnrPackageService};
use fgm_infra::paths::AppPaths;
use flate2::Compression;
use flate2::write::DeflateEncoder;

#[test]
fn deploys_and_restores_a_full_package() {
    let fixture = Fixture::new("anr-full-deploy");
    let zip = fixture.zip(
        "full.zip",
        &[
            ("game/dxgi.dll", b"proxy", false),
            ("game/OptiScaler.ini", b"[FrameGen]\nFGInput=old\n", false),
            ("nr_fg_package.json", br#"{"version":"1.0.0"}"#, false),
        ],
    );
    let package = AnrPackageService::import(&zip, &fixture.data).unwrap();

    let report =
        AnrDeploymentService::deploy(&package, &fixture.target, None, &fixture.paths()).unwrap();

    assert_eq!(report.proxy_module.as_deref(), Some("dxgi.dll"));
    assert_eq!(fs::read(fixture.target.join("dxgi.dll")).unwrap(), b"proxy");
    AnrDeploymentService::restore(&fixture.target, &fixture.paths()).unwrap();
    assert!(!fixture.target.join("dxgi.dll").exists());
    assert!(!fixture.target.join("OptiScaler.ini").exists());
}

#[test]
fn deploys_and_restores_an_addon_package() {
    let fixture = Fixture::new("anr-addon-deploy");
    fs::create_dir_all(fixture.target.join("OptiScaler")).unwrap();
    fs::write(
        fixture.target.join("OptiScaler.ini"),
        b"[FrameGen]\nFGInput=old\n",
    )
    .unwrap();
    let zip = fixture.zip(
        "addon.zip",
        &[
            (
                "Apex Neural Rendering FG/dlssg/nvngx_dlssg.dll",
                b"dll",
                false,
            ),
            ("nr_fg_package.json", br#"{"version":"2.0.0"}"#, false),
        ],
    );
    let package = AnrPackageService::import(&zip, &fixture.data).unwrap();

    AnrDeploymentService::deploy(&package, &fixture.target, None, &fixture.paths()).unwrap();

    assert!(
        fixture
            .target
            .join("OptiScaler/custom/Apex Neural Rendering FG/dlssg/nvngx_dlssg.dll")
            .is_file()
    );
    let ini = fs::read_to_string(fixture.target.join("OptiScaler.ini")).unwrap();
    assert!(ini.contains("FGInput=nvngxfg"));
    assert!(ini.contains("FGNvngxReplacement=Apex Neural Rendering FG"));

    AnrDeploymentService::restore(&fixture.target, &fixture.paths()).unwrap();
    assert!(
        !fixture
            .target
            .join("OptiScaler/custom/Apex Neural Rendering FG")
            .exists()
    );
    assert_eq!(
        fs::read_to_string(fixture.target.join("OptiScaler.ini")).unwrap(),
        "[FrameGen]\nFGInput=old\n"
    );
}

struct Fixture {
    root: PathBuf,
    data: PathBuf,
    target: PathBuf,
}

impl Fixture {
    fn new(name: &str) -> Self {
        let root = unique_dir(name);
        let data = root.join("data");
        let target = root.join("game");
        fs::create_dir_all(&data).unwrap();
        fs::create_dir_all(&target).unwrap();
        Self { root, data, target }
    }

    fn paths(&self) -> AppPaths {
        let paths = AppPaths::new(&self.data, false).unwrap();
        paths.ensure_created().unwrap();
        paths
    }

    fn zip(&self, name: &str, entries: &[(&str, &[u8], bool)]) -> PathBuf {
        let path = self.root.join(name);
        fs::write(&path, build_zip(entries)).unwrap();
        path
    }
}

impl Drop for Fixture {
    fn drop(&mut self) {
        let _ = fs::remove_dir_all(&self.root);
    }
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

fn unique_dir(name: &str) -> PathBuf {
    let nanos = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap()
        .as_nanos();
    let path = std::env::temp_dir().join(format!("fgm-{name}-{}-{nanos}", std::process::id()));
    fs::create_dir_all(&path).unwrap();
    path
}
