use std::fs;
use std::path::{Path, PathBuf};
use std::time::{SystemTime, UNIX_EPOCH};

use fgm_domain::library::GraphicsApi;
use fgm_infra::library::GraphicsApiDetector;

#[test]
fn detects_api_from_the_executable_name() {
    let dir = TestDir::new("api-name");
    let exe = dir.path().join("Game-d3d12.exe");

    assert_eq!(
        GraphicsApiDetector::detect(&exe, dir.path()),
        GraphicsApi::Dx12
    );
}

#[test]
fn detects_api_from_colocated_runtime_dlls() {
    let dir = TestDir::new("api-runtime");
    let exe = dir.path().join("game.exe");
    fs::write(dir.path().join("vulkan-1.dll"), b"not-a-pe").unwrap();

    assert_eq!(
        GraphicsApiDetector::detect(&exe, dir.path()),
        GraphicsApi::Vulkan
    );
}

#[test]
fn vulkan_wrapper_takes_priority() {
    let dir = TestDir::new("api-wrapper");
    let exe = dir.path().join("game-d3d11.exe");
    write_pe(&dir.path().join("dxgi.dll"), "vulkan-1.dll");

    assert_eq!(
        GraphicsApiDetector::detect(&exe, dir.path()),
        GraphicsApi::Vulkan
    );
}

#[test]
fn falls_back_to_engine_dll_imports() {
    let dir = TestDir::new("api-engine");
    let exe = dir.path().join("game.exe");
    write_pe(&dir.path().join("unityplayer.dll"), "d3d11.dll");

    assert_eq!(
        GraphicsApiDetector::detect(&exe, dir.path()),
        GraphicsApi::Dx11
    );
}

#[test]
fn returns_unknown_without_evidence() {
    let dir = TestDir::new("api-unknown");
    let exe = dir.path().join("game.exe");

    assert_eq!(
        GraphicsApiDetector::detect(&exe, dir.path()),
        GraphicsApi::Unknown
    );
}

fn write_pe(path: &Path, import_name: &str) {
    let bytes = build_pe(import_name);
    fs::write(path, bytes).unwrap();
}

fn build_pe(import_name: &str) -> Vec<u8> {
    let mut bytes = vec![0_u8; 0x400];
    bytes[0..2].copy_from_slice(b"MZ");
    put_u32(&mut bytes, 0x3c, 0x40);
    bytes[0x40..0x44].copy_from_slice(b"PE\0\0");
    put_u16(&mut bytes, 0x44, 0x14c);
    put_u16(&mut bytes, 0x46, 1);
    put_u16(&mut bytes, 0x54, 0xe0);
    put_u16(&mut bytes, 0x58, 0x10b);
    put_u32(&mut bytes, 0x58 + 96 + 8, 0x1000);
    put_u32(&mut bytes, 0x58 + 96 + 12, 20);
    bytes[0x138..0x140].copy_from_slice(b".idata\0\0");
    put_u32(&mut bytes, 0x138 + 8, 0x200);
    put_u32(&mut bytes, 0x138 + 12, 0x1000);
    put_u32(&mut bytes, 0x138 + 16, 0x200);
    put_u32(&mut bytes, 0x138 + 20, 0x200);
    put_u32(&mut bytes, 0x200 + 12, 0x1050);
    bytes[0x250..0x250 + import_name.len()].copy_from_slice(import_name.as_bytes());
    bytes
}

fn put_u16(bytes: &mut [u8], offset: usize, value: u16) {
    bytes[offset..offset + 2].copy_from_slice(&value.to_le_bytes());
}

fn put_u32(bytes: &mut [u8], offset: usize, value: u32) {
    bytes[offset..offset + 4].copy_from_slice(&value.to_le_bytes());
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
