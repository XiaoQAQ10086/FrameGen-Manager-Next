use std::path::PathBuf;

use fgm_domain::safety::SafePath;

use crate::paths::AppPaths;

/// Finds a pack directory by id from the standard pack roots.
pub struct PackLocator;

impl PackLocator {
    pub fn find(pack_id: &str) -> Option<PathBuf> {
        Self::find_in(&Self::roots(), pack_id)
    }

    pub fn roots() -> Vec<PathBuf> {
        let mut roots = Vec::new();
        if let Some(root) = std::env::var_os("FGM_PACKS_DIR") {
            roots.push(PathBuf::from(root));
        }
        if let Ok(executable) = std::env::current_exe()
            && let Some(directory) = executable.parent()
        {
            roots.push(directory.join("packs"));
        }
        if let Ok(paths) = AppPaths::create_default(None) {
            roots.push(paths.packs());
        }
        let current = std::env::current_dir().unwrap_or_default();
        roots.push(current.join("packs"));
        roots.push(current.join("assets").join("packs"));
        if let Some(parent) = current.parent() {
            roots.push(parent.join("packs"));
        }

        let mut seen = std::collections::HashSet::new();
        roots.retain(|root| seen.insert(root.to_string_lossy().to_lowercase()));
        roots
    }

    fn find_in(roots: &[PathBuf], pack_id: &str) -> Option<PathBuf> {
        let normalized = SafePath::normalize_relative(pack_id).ok()?;
        if normalized.contains('/') {
            return None;
        }

        roots.iter().find_map(|root| {
            let directory = root.join(&normalized);
            directory.join("pack.json").is_file().then_some(directory)
        })
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::time::{SystemTime, UNIX_EPOCH};

    #[test]
    fn finds_a_pack_in_the_first_matching_root() {
        let nanos = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .unwrap()
            .as_nanos();
        let root =
            std::env::temp_dir().join(format!("fgm-pack-locator-{}-{nanos}", std::process::id()));
        let pack = root.join("framegen.sm86");
        std::fs::create_dir_all(&pack).unwrap();
        std::fs::write(pack.join("pack.json"), b"{}").unwrap();

        let found = PackLocator::find_in(std::slice::from_ref(&root), "framegen.sm86");

        assert_eq!(found.as_deref(), Some(pack.as_path()));
        let _ = std::fs::remove_dir_all(root);
    }

    #[test]
    fn rejects_path_like_ids() {
        assert!(PackLocator::find_in(&[], "../escape").is_none());
        assert!(PackLocator::find_in(&[], "a/b").is_none());
    }
}
