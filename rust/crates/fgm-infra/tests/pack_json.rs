use std::fs;
use std::path::{Path, PathBuf};
use std::time::{SystemTime, UNIX_EPOCH};

use fgm_domain::pack::PackTrust;
use fgm_infra::pack_json::PackJson;

const VALID_JSON: &str = r#"{
  "schemaVersion": 1,
  "id": "framegen.sm86",
  "version": "0.4.2",
  "displayName": "FrameGen SM86",
  "minAppVersion": "2.0.0",
  "gpuRoutes": ["Sm86"],
  "files": [
    {
      "source": "version.dll",
      "target": "version.dll",
      "role": "proxy",
      "proxySlot": true,
      "required": true,
      "sha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
      "size": 5
    }
  ],
  "ownedDirectories": ["runtime"],
  "ini": {
    "target": "dlssg_sm86.ini",
    "template": "template.ini",
    "mode": "merge",
    "templateSha256": "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
    "templateSize": 10
  },
  "steps": [
    {"type": "copyFiles"},
    {"type": "writeIni"},
    {"type": "removeStaleFiles", "arguments": {"target": "old.dll"}}
  ],
  "trust": "bundled"
}"#;

#[test]
fn loads_a_complete_pack() {
    let dir = TestDir::new("json-valid");
    let path = dir.write("pack.json", VALID_JSON);

    let pack = PackJson::load(&path).unwrap();

    assert_eq!(pack.schema_version, 1);
    assert_eq!(pack.id, "framegen.sm86");
    assert_eq!(pack.gpu_routes, vec!["Sm86"]);
    assert_eq!(pack.files.len(), 1);
    assert_eq!(pack.files[0].as_ref().unwrap().size, Some(5));
    assert_eq!(pack.owned_directories, vec!["runtime"]);
    assert!(pack.ini.is_some());
    assert_eq!(pack.steps.len(), 3);
    assert_eq!(pack.trust, PackTrust::Bundled);
}

#[test]
fn rejects_unknown_fields() {
    let dir = TestDir::new("json-unknown");
    let json = VALID_JSON.replace(
        "\"trust\": \"bundled\"",
        "\"trust\": \"bundled\", \"extra\": 1",
    );
    let path = dir.write("pack.json", &json);

    assert!(PackJson::load(&path).is_err());
}

#[test]
fn rejects_unknown_trust() {
    let dir = TestDir::new("json-trust");
    let json = VALID_JSON.replace("\"trust\": \"bundled\"", "\"trust\": \"remote\"");
    let path = dir.write("pack.json", &json);

    assert!(PackJson::load(&path).is_err());
}

#[test]
fn missing_fields_become_schema_defaults() {
    let dir = TestDir::new("json-defaults");
    let path = dir.write("pack.json", "{}");

    let pack = PackJson::load(&path).unwrap();

    assert_eq!(pack.schema_version, 0);
    assert!(pack.id.is_empty());
    assert!(pack.files.is_empty());
    assert_eq!(pack.trust, PackTrust::Bundled);
}

#[test]
fn null_entries_are_preserved_for_validation() {
    let dir = TestDir::new("json-null");
    let json = VALID_JSON.replace(
        r#""files": [
    {
      "source": "version.dll",
      "target": "version.dll",
      "role": "proxy",
      "proxySlot": true,
      "required": true,
      "sha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
      "size": 5
    }
  ]"#,
        r#""files": [null]"#,
    );
    let path = dir.write("pack.json", &json);

    let pack = PackJson::load(&path).unwrap();

    assert_eq!(pack.files, vec![None]);
}

#[test]
fn rejects_comments_and_trailing_commas() {
    for (name, json) in [
        ("comment", "{\n// comment\n\"schemaVersion\": 1\n}"),
        ("trailing", "{\"schemaVersion\": 1,}"),
    ] {
        let dir = TestDir::new(&format!("json-{name}"));
        let path = dir.write("pack.json", json);
        assert!(PackJson::load(&path).is_err(), "{name}");
    }
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

    fn write(&self, relative: &str, contents: &str) -> PathBuf {
        let path = self.0.join(relative);
        if let Some(parent) = path.parent() {
            fs::create_dir_all(parent).unwrap();
        }
        fs::write(&path, contents).unwrap();
        path
    }

    #[allow(dead_code)]
    fn path(&self) -> &Path {
        &self.0
    }
}

impl Drop for TestDir {
    fn drop(&mut self) {
        let _ = fs::remove_dir_all(&self.0);
    }
}
