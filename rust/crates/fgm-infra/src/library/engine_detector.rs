use std::path::Path;

/// Engine family that decides whether REFramework is offered.
#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
pub enum GameEngine {
    #[default]
    Unknown,
    ReEngine,
    MtFramework,
}

/// Detects RE Engine by pak markers, with an MT Framework name fallback.
pub struct EngineDetector;

impl EngineDetector {
    const MT_FRAMEWORK_EXES: [&str; 5] = ["mhw", "mhworld", "re5", "re6", "dmc4"];

    pub fn detect(executable: &Path, install_directory: &Path) -> GameEngine {
        Self::detect_with_roots(executable, &[install_directory])
    }

    /// Checks the explicit roots (target and library install directory) plus
    /// the executable's parent, matching the C# detector.
    pub fn detect_with_roots(executable: &Path, roots: &[&Path]) -> GameEngine {
        let mut candidates = roots
            .iter()
            .map(|root| (*root).to_path_buf())
            .collect::<Vec<_>>();
        if let Some(parent) = executable.parent() {
            candidates.push(parent.to_path_buf());
        }
        if candidates
            .iter()
            .any(|root| Self::has_re_engine_marker(root))
        {
            return GameEngine::ReEngine;
        }
        let stem = executable
            .file_stem()
            .map(|stem| stem.to_string_lossy().to_lowercase())
            .unwrap_or_default();
        if Self::MT_FRAMEWORK_EXES.iter().any(|name| stem == *name) {
            return GameEngine::MtFramework;
        }
        GameEngine::Unknown
    }

    fn has_re_engine_marker(directory: &Path) -> bool {
        let mut current = Some(directory);
        for _ in 0..4 {
            let Some(directory) = current else {
                break;
            };
            if contains_marker(directory) {
                return true;
            }
            current = directory.parent();
        }
        false
    }
}

fn contains_marker(directory: &Path) -> bool {
    let Ok(entries) = std::fs::read_dir(directory) else {
        return false;
    };
    entries.flatten().any(|entry| {
        let name = entry.file_name().to_string_lossy().to_lowercase();
        name == "re_chunk_000.pak"
            || name == "re_dlc_000.pak"
            || name.starts_with("re_chunk_000.pak.patch_")
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::fs;

    fn temp_dir(name: &str) -> std::path::PathBuf {
        let nanos = std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .unwrap()
            .as_nanos();
        let path =
            std::env::temp_dir().join(format!("fgm-engine-{name}-{}-{nanos}", std::process::id()));
        fs::create_dir_all(&path).unwrap();
        path
    }

    #[test]
    fn detects_re_engine_from_chunk_marker() {
        let root = temp_dir("chunk");
        fs::write(root.join("re_chunk_000.pak"), b"pak").unwrap();
        assert_eq!(
            EngineDetector::detect(&root.join("game.exe"), &root),
            GameEngine::ReEngine
        );
        let _ = fs::remove_dir_all(root);
    }

    #[test]
    fn detects_re_engine_from_patch_marker_in_parent() {
        let root = temp_dir("patch");
        let nested = root.join("bin");
        fs::create_dir_all(&nested).unwrap();
        fs::write(root.join("re_chunk_000.pak.patch_002.pak"), b"pak").unwrap();
        assert_eq!(
            EngineDetector::detect(&nested.join("game.exe"), &nested),
            GameEngine::ReEngine
        );
        let _ = fs::remove_dir_all(root);
    }

    #[test]
    fn detects_marker_from_the_library_install_root() {
        let root = temp_dir("install-root");
        let target = root.join("Binaries").join("Win64");
        std::fs::create_dir_all(&target).unwrap();
        fs::write(root.join("re_chunk_000.pak"), b"pak").unwrap();
        assert_eq!(
            EngineDetector::detect_with_roots(
                &target.join("game.exe"),
                &[target.as_path(), root.as_path()]
            ),
            GameEngine::ReEngine
        );
        let _ = fs::remove_dir_all(root);
    }

    #[test]
    fn mt_framework_names_do_not_trigger_reframework() {
        let root = temp_dir("mt");
        assert_eq!(
            EngineDetector::detect(&root.join("mhw.exe"), &root),
            GameEngine::MtFramework
        );
        assert_eq!(
            EngineDetector::detect(&root.join("other.exe"), &root),
            GameEngine::Unknown
        );
        let _ = fs::remove_dir_all(root);
    }
}
