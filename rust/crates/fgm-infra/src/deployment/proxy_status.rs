use std::path::Path;

use fgm_domain::error::AppError;

use crate::hashing::FileHasher;
use crate::pack_json::PackJson;
use crate::packs::PackLocator;

use super::backup_store::BackupStore;

/// Which deployment family owns a proxy module in a game directory.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum ProxyOwner {
    Framegen,
    Dlss5,
    Anr,
    Reframework,
    Other,
}

impl ProxyOwner {
    pub fn key(self) -> &'static str {
        match self {
            Self::Framegen => "framegen",
            Self::Dlss5 => "dlss5",
            Self::Anr => "anr",
            Self::Reframework => "reframework",
            Self::Other => "other",
        }
    }
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct ProxyEntry {
    pub module: String,
    pub owner: ProxyOwner,
    pub owned_by_framegen: bool,
}

const CANDIDATES: [&str; 11] = [
    "version.dll",
    "winmm.dll",
    "dbghelp.dll",
    "dinput8.dll",
    "dxgi.dll",
    "d3d9.dll",
    "d3d10.dll",
    "d3d11.dll",
    "d3d12.dll",
    "opengl32.dll",
    "vulkan-1.dll",
];
const FAMILIES: [(&str, ProxyOwner); 4] = [
    ("framegen.sm86", ProxyOwner::Framegen),
    ("dlss5", ProxyOwner::Dlss5),
    ("anr", ProxyOwner::Anr),
    ("reframework", ProxyOwner::Reframework),
];

/// Reports occupied proxy slots and their owning deployment family.
pub struct ProxyStatus;

impl ProxyStatus {
    pub fn inspect(
        target_directory: &Path,
        backups_root: &Path,
    ) -> Result<Vec<ProxyEntry>, AppError> {
        let store = BackupStore::new(backups_root.to_path_buf());
        let mut loaded = Vec::new();
        for (family, _) in FAMILIES {
            if let Some(manifest) = store.load_manifest(family, target_directory)? {
                loaded.push((family.to_string(), manifest));
            }
        }
        Ok(Self::inspect_loaded_owned(target_directory, &loaded))
    }

    pub fn inspect_loaded(
        target_directory: &Path,
        manifests: &[(&str, &fgm_domain::deployment::DeploymentManifest)],
    ) -> Vec<ProxyEntry> {
        Self::inspect_loaded_owned(
            target_directory,
            &manifests
                .iter()
                .map(|(family, manifest)| ((*family).to_string(), (*manifest).clone()))
                .collect::<Vec<_>>(),
        )
    }

    fn inspect_loaded_owned(
        target_directory: &Path,
        manifests: &[(String, fgm_domain::deployment::DeploymentManifest)],
    ) -> Vec<ProxyEntry> {
        let mut owners: Vec<(String, ProxyOwner)> = Vec::new();
        for (family, manifest) in manifests {
            let Some((_, owner)) = FAMILIES.iter().find(|(name, _)| name == family) else {
                continue;
            };
            for record in &manifest.files {
                owners.push((record.relative_path.to_ascii_lowercase(), *owner));
            }
        }

        let mut entries = Vec::new();
        for module in CANDIDATES {
            if !target_directory.join(module).is_file() {
                continue;
            }
            let lower = module.to_ascii_lowercase();
            let owner = owners
                .iter()
                .find(|(path, _)| path == &lower)
                .map_or(ProxyOwner::Other, |(_, owner)| *owner);
            // A previous frame-gen deployment can leave its proxy behind when
            // the data directory was reset (portable rebuild, wiped cache).
            // Match the pack hash so the slot is still recognised as ours.
            let owner =
                if owner == ProxyOwner::Other && matches_framegen_proxy(target_directory, module) {
                    ProxyOwner::Framegen
                } else {
                    owner
                };
            entries.push(ProxyEntry {
                module: module.to_string(),
                owner,
                owned_by_framegen: owner == ProxyOwner::Framegen,
            });
        }
        entries
    }
}

fn matches_framegen_proxy(target_directory: &Path, module: &str) -> bool {
    let Some(pack_directory) = PackLocator::find("framegen.sm86") else {
        return false;
    };
    let Ok(pack) = PackJson::load(&pack_directory.join("pack.json")) else {
        return false;
    };
    let path = target_directory.join(module);
    let Ok(metadata) = std::fs::metadata(&path) else {
        return false;
    };
    let candidates = pack
        .files
        .iter()
        .flatten()
        .filter(|file| {
            file.proxy_slot
                && file.target.eq_ignore_ascii_case(module)
                && file.size.is_none_or(|size| size as u64 == metadata.len())
        })
        .collect::<Vec<_>>();
    if candidates.is_empty() {
        return false;
    }
    let Ok(hash) = FileHasher::sha256_file(&path) else {
        return false;
    };
    candidates.iter().any(|file| {
        file.sha256
            .as_deref()
            .is_some_and(|expected| expected.eq_ignore_ascii_case(&hash))
    })
}
