use std::path::Path;

use super::render_exe::RenderExe;

const MAX_DEPTH: usize = 5;
const MAX_ENTRIES: usize = 30_000;
const MIN_EXE_SIZE: u64 = 64 * 1024;
const EXCLUDED_PARTS: [&str; 17] = [
    "unins",
    "vcredist",
    "dxsetup",
    "dxwebsetup",
    "crashreport",
    "unitycrashhandler",
    "unrealcefsubprocess",
    "epicwebhelper",
    "eossdk",
    "easyanticheat",
    "eaclauncher",
    "beservice",
    "prereq",
    "installer",
    "subprocess",
    "helper",
    "launcher",
];

/// Scores a game directory's executables and picks the most likely render EXE.
pub struct RenderExeSelector;

impl RenderExeSelector {
    pub fn select(install_directory: &Path) -> Option<RenderExe> {
        if !install_directory.is_dir() {
            return None;
        }

        let install_name = install_directory
            .file_name()
            .map_or_else(String::new, |name| name.to_string_lossy().to_lowercase());
        let mut stack = vec![(install_directory.to_path_buf(), 0_usize)];
        let mut entries_seen = 0_usize;
        let mut best: Option<RenderExe> = None;

        while let Some((directory, depth)) = stack.pop() {
            let Ok(read_dir) = std::fs::read_dir(&directory) else {
                continue;
            };

            for entry in read_dir.flatten() {
                entries_seen += 1;
                if entries_seen > MAX_ENTRIES {
                    return best.filter(|candidate| candidate.score > 0);
                }

                let path = entry.path();
                let Ok(metadata) = std::fs::symlink_metadata(&path) else {
                    continue;
                };
                if is_reparse_point(&metadata) {
                    continue;
                }

                if metadata.is_dir() {
                    if depth < MAX_DEPTH {
                        let name = path
                            .file_name()
                            .map_or_else(String::new, |name| name.to_string_lossy().to_lowercase());
                        if !is_excluded_directory(&name) {
                            stack.push((path, depth + 1));
                        }
                    }
                    continue;
                }

                if !metadata.is_file() || metadata.len() < MIN_EXE_SIZE {
                    continue;
                }
                if path
                    .extension()
                    .is_none_or(|extension| !extension.eq_ignore_ascii_case("exe"))
                {
                    continue;
                }

                let file_name = path
                    .file_name()
                    .map_or_else(String::new, |name| name.to_string_lossy().to_lowercase());
                if EXCLUDED_PARTS.iter().any(|part| file_name.contains(part)) {
                    continue;
                }

                let score = score_executable(&path, &install_name, metadata.len());
                let candidate = RenderExe {
                    path,
                    score,
                    size: metadata.len(),
                };
                if best.as_ref().is_none_or(|current| {
                    candidate.score > current.score
                        || (candidate.score == current.score && candidate.size > current.size)
                }) {
                    best = Some(candidate);
                }
            }
        }

        best.filter(|candidate| candidate.score > 0)
    }
}

fn score_executable(path: &Path, install_name: &str, size: u64) -> i32 {
    let file_name = path
        .file_name()
        .map_or_else(String::new, |name| name.to_string_lossy().to_lowercase());
    let stem = path
        .file_stem()
        .map_or_else(String::new, |name| name.to_string_lossy().to_lowercase());
    let parent = path
        .parent()
        .and_then(Path::file_name)
        .map_or_else(String::new, |name| name.to_string_lossy().to_lowercase());
    let grandparent = path
        .parent()
        .and_then(Path::parent)
        .and_then(Path::file_name)
        .map_or_else(String::new, |name| name.to_string_lossy().to_lowercase());
    let great_grandparent = path
        .parent()
        .and_then(Path::parent)
        .and_then(Path::parent)
        .and_then(Path::file_name)
        .map_or_else(String::new, |name| name.to_string_lossy().to_lowercase());

    let mut score = 0;
    if file_name.ends_with("-win64-shipping.exe") {
        score += 50;
    } else if file_name.ends_with("-win32-shipping.exe") {
        score += 20;
    }

    if parent == "win64" {
        score += 10;
        if grandparent == "binaries" && great_grandparent == stem {
            score += 30;
        }
    }

    if install_name == stem {
        score += 20;
    }

    if size > 10 * 1024 * 1024 {
        score += 15;
    } else if size > 2 * 1024 * 1024 {
        score += 3;
    }

    score
}

fn is_excluded_directory(name: &str) -> bool {
    matches!(
        name,
        "_commonredist" | "directx" | "vcredist" | "easyanticheat" | "engine"
    )
}

#[cfg(windows)]
fn is_reparse_point(metadata: &std::fs::Metadata) -> bool {
    use std::os::windows::fs::MetadataExt;
    metadata.file_attributes() & 0x400 != 0
}

#[cfg(not(windows))]
fn is_reparse_point(metadata: &std::fs::Metadata) -> bool {
    metadata.file_type().is_symlink()
}
