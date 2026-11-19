use std::fs;
use std::io::{Read, Write};
use std::net::TcpListener;
use std::path::{Path, PathBuf};
use std::sync::atomic::AtomicBool;
use std::thread;
use std::time::{SystemTime, UNIX_EPOCH};

use fgm_infra::updates::{UpdateChecker, UpdateDownloader};

const HELLO_SHA256: &str = "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824";

#[test]
fn checks_and_downloads_an_update() {
    let listener = TcpListener::bind("127.0.0.1:0").unwrap();
    let port = listener.local_addr().unwrap().port();
    let server = thread::spawn(move || {
        for _ in 0..2 {
            let (mut stream, _) = listener.accept().unwrap();
            let mut request = [0_u8; 1024];
            let _ = stream.read(&mut request);
            let body = if String::from_utf8_lossy(&request).contains("/update.json") {
                format!(
                    r#"{{"version":"2.3.4","notes":"test","installer":{{"name":"Setup.exe","size":5,"sha256":"{HELLO_SHA256}","urls":["http://127.0.0.1:{port}/Setup.exe"]}}}}"#
                )
            } else {
                "hello".to_string()
            };
            let response = format!(
                "HTTP/1.1 200 OK\r\nContent-Length: {}\r\nConnection: close\r\n\r\n{body}",
                body.len()
            );
            stream.write_all(response.as_bytes()).unwrap();
        }
    });

    let dir = TestDir::new("updates");
    let manifest_url = format!("http://127.0.0.1:{port}/update.json");
    let info = UpdateChecker::check(&[manifest_url], "2.3.3", &AtomicBool::new(false)).unwrap();
    assert!(info.available);
    assert_eq!(info.manifest.version, "2.3.4");

    let installer = info.manifest.installer.unwrap();
    let path = UpdateDownloader::download(
        &installer,
        &info.manifest.version,
        &AtomicBool::new(false),
        |_| {},
    )
    .unwrap();
    assert_eq!(fs::read(&path).unwrap(), b"hello");
    let _ = server.join();
    let _ = fs::remove_dir_all(dir.path());
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
