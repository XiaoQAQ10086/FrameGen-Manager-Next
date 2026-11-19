use std::fs;
use std::path::{Path, PathBuf};
use std::process::Command;
use std::time::{SystemTime, UNIX_EPOCH};

const HELLO_SHA256: &str = "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824";

#[test]
fn validates_a_pack_directory() {
    let dir = TestDir::new("cli-valid");
    dir.write("pack.json", valid_json(HELLO_SHA256));
    dir.write("version.dll", b"hello");

    let output = run(&["pack", "validate", dir.as_str(), "--app-version", "2.3.4"]);

    assert!(
        output.status.success(),
        "{}",
        String::from_utf8_lossy(&output.stderr)
    );
    assert!(String::from_utf8_lossy(&output.stdout).contains("OK framegen.sm86 0.4.2"));
}

#[test]
fn schema_only_skips_the_payload() {
    let dir = TestDir::new("cli-schema");
    dir.write("pack.json", valid_json(HELLO_SHA256));

    let output = run(&[
        "pack",
        "validate",
        dir.as_str(),
        "--schema-only",
        "--app-version",
        "2.3.4",
    ]);

    assert!(
        output.status.success(),
        "{}",
        String::from_utf8_lossy(&output.stderr)
    );
    assert!(String::from_utf8_lossy(&output.stdout).contains("schema only"));
}

#[test]
fn reports_schema_codes() {
    let dir = TestDir::new("cli-schema-error");
    dir.write(
        "pack.json",
        valid_json(HELLO_SHA256).replace("framegen.sm86", "Bad Pack"),
    );

    let output = run(&["pack", "validate", dir.as_str(), "--schema-only"]);

    assert_eq!(output.status.code(), Some(1));
    assert!(String::from_utf8_lossy(&output.stderr).contains("PACK-002"));
}

#[test]
fn no_hash_skips_hash_comparison() {
    let dir = TestDir::new("cli-no-hash");
    dir.write("pack.json", valid_json(&"b".repeat(64)));
    dir.write("version.dll", b"hello");

    let output = run(&[
        "pack",
        "validate",
        dir.as_str(),
        "--no-hash",
        "--app-version",
        "2.3.4",
    ]);

    assert!(
        output.status.success(),
        "{}",
        String::from_utf8_lossy(&output.stderr)
    );
}

#[test]
fn accepts_a_relative_pack_directory() {
    let cwd = std::env::current_dir().unwrap();
    let nanos = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap()
        .as_nanos();
    let directory = cwd
        .join("target")
        .join(format!("fgm-cli-relative-{}-{nanos}", std::process::id()));
    fs::create_dir_all(&directory).unwrap();
    fs::write(directory.join("pack.json"), valid_json(HELLO_SHA256)).unwrap();
    fs::write(directory.join("version.dll"), b"hello").unwrap();

    let relative = directory.strip_prefix(&cwd).unwrap().display().to_string();
    let output = run(&["pack", "validate", &relative, "--app-version", "2.3.4"]);
    let _ = fs::remove_dir_all(&directory);

    assert!(
        output.status.success(),
        "{}",
        String::from_utf8_lossy(&output.stderr)
    );
}

#[test]
fn missing_directory_is_an_argument_error() {
    let dir = TestDir::new("cli-missing");
    let missing = dir.path().join("does-not-exist");

    let output = run(&["pack", "validate", &missing.display().to_string()]);

    assert_eq!(output.status.code(), Some(2));
}

#[test]
fn prints_the_version() {
    let output = run(&["--version"]);
    assert!(output.status.success());
    assert!(String::from_utf8_lossy(&output.stdout).starts_with("fgm-next "));
}

fn run(args: &[&str]) -> std::process::Output {
    Command::new(env!("CARGO_BIN_EXE_fgm-next"))
        .args(args)
        .output()
        .unwrap()
}

fn valid_json(sha256: &str) -> String {
    format!(
        r#"{{
  "schemaVersion": 1,
  "id": "framegen.sm86",
  "version": "0.4.2",
  "displayName": "FrameGen SM86",
  "minAppVersion": "2.0.0",
  "gpuRoutes": ["Sm86"],
  "files": [
    {{
      "source": "version.dll",
      "target": "version.dll",
      "role": "proxy",
      "proxySlot": true,
      "required": true,
      "sha256": "{sha256}",
      "size": 5
    }}
  ],
  "trust": "bundled"
}}"#
    )
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

    fn write(&self, relative: &str, contents: impl AsRef<[u8]>) {
        let path = self.0.join(relative);
        if let Some(parent) = path.parent() {
            fs::create_dir_all(parent).unwrap();
        }
        fs::write(&path, contents).unwrap();
    }

    fn as_str(&self) -> &str {
        self.0.to_str().unwrap()
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
