use std::fs;
use std::io::{Read, Write};
use std::net::TcpListener;
use std::path::{Path, PathBuf};
use std::sync::atomic::AtomicBool;
use std::thread;
use std::time::{SystemTime, UNIX_EPOCH};

use fgm_infra::anr::{AnrOnlineService, AnrPackageKind};
use fgm_infra::hashing::FileHasher;
use flate2::Compression;
use flate2::write::DeflateEncoder;

#[test]
fn downloads_and_imports_the_latest_full_package() {
    let dir = TestDir::new("anr-online");
    let zip = dir.path().join("anr.zip");
    fs::write(
        &zip,
        build_zip(&[
            ("game/dxgi.dll", b"proxy", false),
            ("game/OptiScaler.ini", b"ini", false),
            ("nr_fg_package.json", br#"{"version":"9.9.9"}"#, true),
        ]),
    )
    .unwrap();
    let zip_bytes = fs::read(&zip).unwrap();
    let sha = FileHasher::sha256_file(&zip).unwrap();
    let size = zip_bytes.len();

    let listener = TcpListener::bind("127.0.0.1:0").unwrap();
    let port = listener.local_addr().unwrap().port();
    let server = thread::spawn(move || {
        for index in 0..2 {
            let (mut stream, _) = listener.accept().unwrap();
            let mut request = [0_u8; 2048];
            let _ = stream.read(&mut request);
            if index == 0 {
                let json = format!(
                    r#"{{"tag_name":"v1.0.0","assets":[{{"name":"anr-fg-1.0.0-full.zip","size":{size},"digest":"sha256:{sha}","browser_download_url":"http://127.0.0.1:{port}/anr.zip"}}]}}"#
                );
                let response = format!(
                    "HTTP/1.1 200 OK\r\nContent-Length: {}\r\nConnection: close\r\n\r\n{json}",
                    json.len()
                );
                stream.write_all(response.as_bytes()).unwrap();
            } else {
                let header = format!(
                    "HTTP/1.1 200 OK\r\nContent-Length: {}\r\nConnection: close\r\n\r\n",
                    zip_bytes.len()
                );
                stream.write_all(header.as_bytes()).unwrap();
                stream.write_all(&zip_bytes).unwrap();
            }
        }
    });

    let data = dir.path().join("data");
    let api = format!("http://127.0.0.1:{port}/latest");
    let info =
        AnrOnlineService::download_from(&api, &data, &AtomicBool::new(false), |_| {}).unwrap();

    assert_eq!(info.kind, AnrPackageKind::Full);
    assert_eq!(info.version, "9.9.9");
    let _ = server.join();
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
