use std::fs;
use std::io::{Read, Write};
use std::net::TcpListener;
use std::path::{Path, PathBuf};
use std::sync::atomic::AtomicBool;
use std::thread;
use std::time::{SystemTime, UNIX_EPOCH};

use fgm_infra::net::Downloader;

#[test]
fn downloads_a_body_over_http() {
    let listener = TcpListener::bind("127.0.0.1:0").unwrap();
    let port = listener.local_addr().unwrap().port();
    let server = thread::spawn(move || {
        let (mut stream, _) = listener.accept().unwrap();
        let mut request = [0_u8; 1024];
        let _ = stream.read(&mut request);
        stream
            .write_all(b"HTTP/1.1 200 OK\r\nContent-Length: 5\r\nConnection: close\r\n\r\nhello")
            .unwrap();
    });

    let dir = TestDir::new("download");
    let destination = dir.path().join("file.bin");
    let url = format!("http://127.0.0.1:{port}/file.bin");
    let bytes = Downloader::download(&url, &destination, &AtomicBool::new(false)).unwrap();

    assert_eq!(bytes, 5);
    assert_eq!(fs::read(&destination).unwrap(), b"hello");
    let _ = server.join();
}

#[test]
fn follows_an_absolute_redirect() {
    let listener = TcpListener::bind("127.0.0.1:0").unwrap();
    let port = listener.local_addr().unwrap().port();
    let server = thread::spawn(move || {
        for index in 0..2 {
            let (mut stream, _) = listener.accept().unwrap();
            let mut request = [0_u8; 1024];
            let _ = stream.read(&mut request);
            if index == 0 {
                let response = format!(
                    "HTTP/1.1 302 Found\r\nLocation: http://127.0.0.1:{port}/final\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"
                );
                stream.write_all(response.as_bytes()).unwrap();
            } else {
                stream
                    .write_all(
                        b"HTTP/1.1 200 OK\r\nContent-Length: 5\r\nConnection: close\r\n\r\nhello",
                    )
                    .unwrap();
            }
        }
    });

    let dir = TestDir::new("download-redirect");
    let destination = dir.path().join("file.bin");
    let url = format!("http://127.0.0.1:{port}/start");
    let bytes = Downloader::download(&url, &destination, &AtomicBool::new(false)).unwrap();

    assert_eq!(bytes, 5);
    assert_eq!(fs::read(&destination).unwrap(), b"hello");
    let _ = server.join();
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
