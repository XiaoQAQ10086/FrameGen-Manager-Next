use std::fs;
use std::path::PathBuf;
use std::process::{Command, Output};
use std::time::{SystemTime, UNIX_EPOCH};

use fgm_infra::hashing::FileHasher;

#[test]
fn deploys_and_restores_a_pack() {
    let fixture = Fixture::new("deploy-roundtrip");
    fs::write(fixture.target.join("version.dll"), b"original").unwrap();

    let deploy = fixture.run(&[
        "deploy",
        fixture.pack.to_str().unwrap(),
        fixture.target.to_str().unwrap(),
        "--family",
        "test.pack",
        "--data-dir",
        fixture.data.to_str().unwrap(),
        "--app-version",
        "2.3.4",
    ]);
    assert!(deploy.status.success(), "{}", stderr(&deploy));
    assert!(stdout(&deploy).contains("OK test.pack 1.0.0"));
    assert_eq!(
        fs::read_to_string(fixture.target.join("version.dll")).unwrap(),
        "runtime"
    );

    let restore = fixture.run(&[
        "restore",
        fixture.target.to_str().unwrap(),
        "--family",
        "test.pack",
        "--data-dir",
        fixture.data.to_str().unwrap(),
    ]);
    assert!(restore.status.success(), "{}", stderr(&restore));
    assert!(stdout(&restore).contains("restored=1"));
    assert_eq!(
        fs::read_to_string(fixture.target.join("version.dll")).unwrap(),
        "original"
    );
}

#[test]
fn dry_run_does_not_write() {
    let fixture = Fixture::new("deploy-dry");
    let result = fixture.run(&[
        "deploy",
        fixture.pack.to_str().unwrap(),
        fixture.target.to_str().unwrap(),
        "--data-dir",
        fixture.data.to_str().unwrap(),
        "--dry-run",
        "--app-version",
        "2.3.4",
    ]);

    assert!(result.status.success(), "{}", stderr(&result));
    assert!(stdout(&result).contains("PLAN version.dll"));
    assert!(!fixture.target.join("version.dll").exists());
}

#[test]
fn unknown_proxy_slot_fails_without_writing() {
    let fixture = Fixture::new_with_proxy("deploy-bad-slot");
    let result = fixture.run(&[
        "deploy",
        fixture.pack.to_str().unwrap(),
        fixture.target.to_str().unwrap(),
        "--proxy-slot",
        "dxgi.dll",
        "--data-dir",
        fixture.data.to_str().unwrap(),
        "--app-version",
        "2.3.4",
    ]);

    assert_eq!(result.status.code(), Some(1));
    assert!(!fixture.target.join("version.dll").exists());
}

#[test]
fn inspect_reports_the_current_state() {
    let fixture = Fixture::new("deploy-inspect");
    fixture.run(&[
        "deploy",
        fixture.pack.to_str().unwrap(),
        fixture.target.to_str().unwrap(),
        "--family",
        "test.pack",
        "--data-dir",
        fixture.data.to_str().unwrap(),
        "--app-version",
        "2.3.4",
    ]);

    let installed = fixture.run(&[
        "inspect",
        fixture.target.to_str().unwrap(),
        "--family",
        "test.pack",
        "--data-dir",
        fixture.data.to_str().unwrap(),
    ]);
    assert!(installed.status.success(), "{}", stderr(&installed));
    assert!(stdout(&installed).contains("state=Installed"));

    fs::write(fixture.target.join("version.dll"), b"modified").unwrap();
    let conflict = fixture.run(&[
        "inspect",
        fixture.target.to_str().unwrap(),
        "--family",
        "test.pack",
        "--data-dir",
        fixture.data.to_str().unwrap(),
    ]);
    assert!(stdout(&conflict).contains("state=Conflict"));
}

#[test]
fn restore_requires_a_family() {
    let fixture = Fixture::new("deploy-family");
    let result = fixture.run(&["restore", fixture.target.to_str().unwrap()]);
    assert_eq!(result.status.code(), Some(2));
}

struct Fixture {
    root: PathBuf,
    pack: PathBuf,
    target: PathBuf,
    data: PathBuf,
}

impl Fixture {
    fn new(name: &str) -> Self {
        Self::new_with_role(name, "runtime", false)
    }

    fn new_with_proxy(name: &str) -> Self {
        Self::new_with_role(name, "proxy", true)
    }

    fn new_with_role(name: &str, role: &str, proxy_slot: bool) -> Self {
        let root = unique_dir(name);
        let pack = root.join("pack");
        let target = root.join("game");
        let data = root.join("data");
        fs::create_dir_all(&pack).unwrap();
        fs::create_dir_all(&target).unwrap();
        fs::write(pack.join("version.dll"), b"runtime").unwrap();

        let path = pack.join("version.dll");
        let sha256 = FileHasher::sha256_file(&path).unwrap();
        let size = fs::metadata(&path).unwrap().len();
        let json = format!(
            r#"{{
  "schemaVersion": 1,
  "id": "test.pack",
  "version": "1.0.0",
  "displayName": "Test Pack",
  "minAppVersion": "1.0.0",
  "gpuRoutes": ["Any"],
  "files": [
    {{
      "source": "version.dll",
      "target": "version.dll",
      "role": "{role}",
      "proxySlot": {proxy_slot},
      "required": true,
      "sha256": "{sha256}",
      "size": {size}
    }}
  ],
  "trust": "bundled"
}}"#
        );
        fs::write(pack.join("pack.json"), json).unwrap();

        Self {
            root,
            pack,
            target,
            data,
        }
    }

    fn run(&self, args: &[&str]) -> Output {
        Command::new(env!("CARGO_BIN_EXE_fgm-next"))
            .args(args)
            .output()
            .unwrap()
    }
}

impl Drop for Fixture {
    fn drop(&mut self) {
        let _ = fs::remove_dir_all(&self.root);
    }
}

fn stdout(output: &Output) -> String {
    String::from_utf8_lossy(&output.stdout).into_owned()
}

fn stderr(output: &Output) -> String {
    String::from_utf8_lossy(&output.stderr).into_owned()
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
