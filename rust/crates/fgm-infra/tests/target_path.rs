use std::fs;
use std::path::{Path, PathBuf};
use std::time::{SystemTime, UNIX_EPOCH};

use fgm_domain::error::ErrorCode;
use fgm_infra::paths::TargetPath;

#[test]
fn combines_a_relative_path_under_the_root() {
    let dir = TestDir::new("target-combine");
    let root = dir.path().join("game");
    fs::create_dir_all(&root).unwrap();

    let combined = TargetPath::combine(&root, "dlssg_sm86/runtime/nvngx.dll").unwrap();

    assert_eq!(
        combined,
        root.join("dlssg_sm86").join("runtime").join("nvngx.dll")
    );
}

#[test]
fn creates_nonexistent_nested_targets_lexically() {
    let dir = TestDir::new("target-nested");
    let root = dir.path().join("game");
    fs::create_dir_all(&root).unwrap();

    let combined = TargetPath::combine(&root, "plugins/new/file.dll").unwrap();

    assert!(combined.ends_with(Path::new("plugins").join("new").join("file.dll")));
}

#[test]
fn rejects_a_relative_root() {
    let error = TargetPath::combine(Path::new("relative/root"), "file.dll").unwrap_err();
    assert_eq!(error.code, ErrorCode::InvalidPath);
}

#[test]
fn rejects_absolute_and_traversal_paths() {
    let dir = TestDir::new("target-unsafe");
    let root = dir.path().join("game");
    fs::create_dir_all(&root).unwrap();

    for value in ["/etc/passwd", "\\\\server\\share\\x.dll", "..\\outside.dll"] {
        let error = TargetPath::combine(&root, value).unwrap_err();
        assert!(
            matches!(error.code, ErrorCode::InvalidPath | ErrorCode::UnsafePath),
            "{value}: {:?}",
            error.code
        );
    }
}

#[cfg(windows)]
#[test]
fn rejects_a_directory_symlink_that_escapes_the_root() {
    use std::os::windows::fs::symlink_dir;

    let dir = TestDir::new("target-symlink");
    let root = dir.path().join("game");
    let outside = dir.path().join("outside");
    fs::create_dir_all(&root).unwrap();
    fs::create_dir_all(&outside).unwrap();

    if symlink_dir(&outside, root.join("link")).is_err() {
        return;
    }

    let error = TargetPath::combine(&root, "link/evil.dll").unwrap_err();
    assert_eq!(error.code, ErrorCode::UnsafePath);
}

#[test]
fn detects_protected_system_directories() {
    let system_drive = std::env::var_os("SystemDrive")
        .map(PathBuf::from)
        .unwrap_or_else(|| PathBuf::from("C:"));
    let drive_root = PathBuf::from(format!("{}\\", system_drive.display()));
    assert!(TargetPath::is_protected_system_directory(&drive_root));

    if let Some(windows) = std::env::var_os("SystemRoot") {
        let windows = PathBuf::from(windows);
        assert!(TargetPath::is_protected_system_directory(&windows));
        assert!(TargetPath::is_protected_system_directory(
            &windows.join("System32")
        ));
    }

    if let Some(program_files) = std::env::var_os("ProgramFiles") {
        let program_files = PathBuf::from(program_files);
        assert!(TargetPath::is_protected_system_directory(&program_files));
        assert!(!TargetPath::is_protected_system_directory(
            &program_files.join("SomeGame")
        ));
    }

    let temp = std::env::temp_dir();
    assert!(!TargetPath::is_protected_system_directory(&temp));
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
