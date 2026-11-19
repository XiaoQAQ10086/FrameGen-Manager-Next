use std::collections::BTreeMap;
use std::fs;
use std::path::{Path, PathBuf};
use std::time::{SystemTime, UNIX_EPOCH};

use fgm_domain::pack::{
    AssetPack, AssetPackFile, AssetPackIni, AssetPackStep, PackCode, PackTrust, PackValidation,
};
use fgm_infra::hashing::FileHasher;
use fgm_infra::pack_payload::PackPayloadValidator;

const HELLO_SHA256: &str = "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824";

#[test]
fn valid_payload_passes() {
    let dir = TestDir::new("payload-valid");
    dir.write("version.dll", b"hello");

    let validation = PackPayloadValidator::validate(&valid_pack(), dir.path(), true);

    assert!(validation.is_valid(), "{:?}", validation.issues());
}

#[test]
fn missing_file_is_reported() {
    let dir = TestDir::new("payload-missing");
    let validation = PackPayloadValidator::validate(&valid_pack(), dir.path(), true);
    assert_has(&validation, PackCode::PayloadMissing);
}

#[test]
fn size_mismatch_is_reported() {
    let dir = TestDir::new("payload-size");
    dir.write("version.dll", b"hello!");

    let mut pack = valid_pack();
    pack.files[0].as_mut().unwrap().size = Some(5);

    assert_has(
        &PackPayloadValidator::validate(&pack, dir.path(), true),
        PackCode::PayloadSize,
    );
}

#[test]
fn hash_mismatch_is_reported() {
    let dir = TestDir::new("payload-hash");
    dir.write("version.dll", b"hello");

    let mut pack = valid_pack();
    pack.files[0].as_mut().unwrap().sha256 = Some("b".repeat(64));

    assert_has(
        &PackPayloadValidator::validate(&pack, dir.path(), true),
        PackCode::PayloadHash,
    );
}

#[test]
fn hash_check_can_be_skipped() {
    let dir = TestDir::new("payload-no-hash");
    dir.write("version.dll", b"hello");

    let mut pack = valid_pack();
    pack.files[0].as_mut().unwrap().sha256 = Some("b".repeat(64));

    let validation = PackPayloadValidator::validate(&pack, dir.path(), false);
    assert!(validation.is_valid(), "{:?}", validation.issues());
}

#[test]
fn unsafe_source_is_reported() {
    let dir = TestDir::new("payload-unsafe");
    let mut pack = valid_pack();
    pack.files[0].as_mut().unwrap().source = "../outside.dll".to_string();

    assert_has(
        &PackPayloadValidator::validate(&pack, dir.path(), true),
        PackCode::PayloadMissing,
    );
}

#[test]
fn ini_template_must_exist() {
    let dir = TestDir::new("payload-ini-missing");
    dir.write("version.dll", b"hello");

    let mut pack = valid_pack();
    pack.ini = Some(AssetPackIni {
        target: "dlssg_sm86.ini".to_string(),
        template: "template.ini".to_string(),
        mode: "merge".to_string(),
        template_sha256: Some("c".repeat(64)),
        template_size: Some(10),
        stock_values: Vec::new(),
    });

    assert_has(
        &PackPayloadValidator::validate(&pack, dir.path(), true),
        PackCode::IniTemplateMissing,
    );
}

#[test]
fn ini_template_size_and_hash_are_checked() {
    let dir = TestDir::new("payload-ini-mismatch");
    dir.write("version.dll", b"hello");
    dir.write("template.ini", b"template");

    let mut pack = valid_pack();
    pack.ini = Some(AssetPackIni {
        target: "dlssg_sm86.ini".to_string(),
        template: "template.ini".to_string(),
        mode: "merge".to_string(),
        template_sha256: Some("c".repeat(64)),
        template_size: Some(99),
        stock_values: Vec::new(),
    });

    let validation = PackPayloadValidator::validate(&pack, dir.path(), true);
    assert_has(&validation, PackCode::IniTemplateMismatch);
}

#[test]
fn valid_ini_template_passes() {
    let dir = TestDir::new("payload-ini-valid");
    dir.write("version.dll", b"hello");
    let template = dir.write("template.ini", b"template");
    let hash = FileHasher::sha256_file(&template).unwrap();

    let mut pack = valid_pack();
    pack.ini = Some(AssetPackIni {
        target: "dlssg_sm86.ini".to_string(),
        template: "template.ini".to_string(),
        mode: "merge".to_string(),
        template_sha256: Some(hash),
        template_size: Some(8),
        stock_values: Vec::new(),
    });

    assert!(PackPayloadValidator::validate(&pack, dir.path(), true).is_valid());
}

#[test]
fn installer_payload_is_checked() {
    let dir = TestDir::new("payload-installer");
    dir.write("version.dll", b"hello");

    let mut pack = valid_pack();
    pack.steps = vec![Some(AssetPackStep {
        kind: "runInstaller".to_string(),
        arguments: Some(BTreeMap::from([
            ("source".to_string(), "setup.exe".to_string()),
            ("sha256".to_string(), "b".repeat(64)),
        ])),
    })];

    assert_has(
        &PackPayloadValidator::validate(&pack, dir.path(), true),
        PackCode::InstallerPayload,
    );
}

fn valid_pack() -> AssetPack {
    AssetPack {
        schema_version: 1,
        id: "framegen.sm86".to_string(),
        version: "0.4.2".to_string(),
        display_name: "FrameGen SM86".to_string(),
        min_app_version: "2.0.0".to_string(),
        gpu_routes: vec!["Sm86".to_string()],
        files: vec![Some(AssetPackFile {
            source: "version.dll".to_string(),
            target: "version.dll".to_string(),
            role: "proxy".to_string(),
            required: true,
            proxy_slot: true,
            sha256: Some(HELLO_SHA256.to_string()),
            size: Some(5),
        })],
        owned_directories: Vec::new(),
        ini: None,
        steps: Vec::new(),
        trust: PackTrust::Bundled,
    }
}

fn assert_has(validation: &PackValidation, code: PackCode) {
    assert!(
        validation.issues().iter().any(|issue| issue.code == code),
        "missing {:?} in {:?}",
        code,
        validation.issues()
    );
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

    fn write(&self, relative: &str, contents: &[u8]) -> PathBuf {
        let path = self.0.join(relative);
        if let Some(parent) = path.parent() {
            fs::create_dir_all(parent).unwrap();
        }
        fs::write(&path, contents).unwrap();
        path
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
