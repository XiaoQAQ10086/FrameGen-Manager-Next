use std::collections::BTreeMap;
use std::path::{Path, PathBuf};
use std::sync::atomic::AtomicBool;

use fgm_domain::error::{AppError, ErrorCode};
use fgm_domain::ini::IniDocument;

use crate::deployment::{DeploymentEngine, DeploymentFile, DeploymentProgress};
use crate::hashing::{FileHasher, sha256_hex};
use crate::paths::AppPaths;

use super::anr_package_info::AnrPackageInfo;
use super::anr_package_kind::AnrPackageKind;
use super::anr_report::AnrReport;

const PROXY_CANDIDATES: [&str; 3] = ["dxgi.dll", "winmm.dll", "version.dll"];

/// Deploys imported ANR packages through the shared deployment engine.
pub struct AnrDeploymentService;

impl AnrDeploymentService {
    pub fn select_proxy(target_directory: &Path) -> Result<&'static str, AppError> {
        PROXY_CANDIDATES
            .iter()
            .find(|candidate| !target_directory.join(candidate).is_file())
            .copied()
            .ok_or_else(|| {
                AppError::new(ErrorCode::Conflict)
                    .with("reason", "all ANR proxy slots are occupied")
            })
    }

    pub fn deploy(
        package: &AnrPackageInfo,
        target_directory: &Path,
        game_exe: Option<&Path>,
        paths: &AppPaths,
        cancelled: &AtomicBool,
        progress: Option<&dyn DeploymentProgress>,
        proxy: Option<&str>,
    ) -> Result<AnrReport, AppError> {
        if game_exe.is_some_and(crate::process::GameProcessProbe::is_running) {
            return Err(
                AppError::new(ErrorCode::GameRunning).with("reason", "the game is still running")
            );
        }

        match package.kind {
            AnrPackageKind::Full => deploy_full(
                package,
                target_directory,
                game_exe,
                paths,
                cancelled,
                progress,
                proxy,
            ),
            AnrPackageKind::Addon => deploy_addon(
                package,
                target_directory,
                game_exe,
                paths,
                cancelled,
                progress,
            ),
        }
    }

    /// Honours a caller-chosen slot, refusing one that is already taken.
    fn resolve_proxy(target_directory: &Path, proxy: Option<&str>) -> Result<String, AppError> {
        let Some(proxy) = proxy.filter(|value| !value.is_empty()) else {
            return Ok(AnrDeploymentService::select_proxy(target_directory)?.to_string());
        };
        let allowed = PROXY_CANDIDATES
            .iter()
            .any(|candidate| candidate.eq_ignore_ascii_case(proxy));
        if !allowed {
            return Err(
                AppError::new(ErrorCode::InvalidArgument).with("reason", "unknown proxy slot")
            );
        }
        if target_directory.join(proxy).is_file() {
            return Err(AppError::new(ErrorCode::Conflict)
                .with("module", proxy)
                .with("reason", "the chosen proxy slot is already occupied"));
        }
        Ok(proxy.to_string())
    }

    pub fn restore(target_directory: &Path, paths: &AppPaths) -> Result<(), AppError> {
        let _report = DeploymentEngine::restore(
            "anr",
            target_directory,
            &paths.backups(),
            &paths.state().join("locks"),
        )?;
        // ANR's custom provider and runtime log are owned by this deployment,
        // even when a tracked file was modified and restore reported a conflict.
        for relative in [
            "OptiScaler/custom/Apex Neural Rendering FG",
            "custom/Apex Neural Rendering FG",
        ] {
            let directory = target_directory.join(relative);
            if directory.is_dir() {
                let _ = std::fs::remove_dir_all(&directory);
                if let Some(parent) = directory.parent()
                    && parent.is_dir()
                    && std::fs::read_dir(parent).is_ok_and(|mut entries| entries.next().is_none())
                {
                    let _ = std::fs::remove_dir(parent);
                }
            }
        }
        let _ = std::fs::remove_file(target_directory.join("OptiScaler.log"));
        Ok(())
    }
}

fn deploy_full(
    package: &AnrPackageInfo,
    target_directory: &Path,
    game_exe: Option<&Path>,
    paths: &AppPaths,
    cancelled: &AtomicBool,
    progress: Option<&dyn DeploymentProgress>,
    proxy: Option<&str>,
) -> Result<AnrReport, AppError> {
    let proxy = AnrDeploymentService::resolve_proxy(target_directory, proxy)?;
    let files = collect_files(&package.payload_directory)?
        .into_iter()
        .map(|(relative, source)| {
            let target = if relative.eq_ignore_ascii_case("dxgi.dll") {
                proxy.clone()
            } else {
                relative
            };
            deployment_file(&source, &target, "runtime")
        })
        .collect::<Result<Vec<_>, _>>()?;

    apply(
        &files,
        target_directory,
        game_exe,
        paths,
        cancelled,
        progress,
    )?;
    Ok(AnrReport {
        kind: AnrPackageKind::Full,
        files_written: files.len(),
        proxy_module: Some(proxy),
    })
}

fn deploy_addon(
    package: &AnrPackageInfo,
    target_directory: &Path,
    game_exe: Option<&Path>,
    paths: &AppPaths,
    cancelled: &AtomicBool,
    progress: Option<&dyn DeploymentProgress>,
) -> Result<AnrReport, AppError> {
    let ini_path = target_directory.join("OptiScaler.ini");
    if !ini_path.is_file() {
        return Err(AppError::new(ErrorCode::FileNotFound)
            .with("path", ini_path.display().to_string())
            .with(
                "reason",
                "addon packages require an existing OptiScaler.ini",
            ));
    }

    let opti_directory = target_directory.join("OptiScaler");
    let (custom_rel, custom_source) = if opti_directory.is_dir() {
        (
            "OptiScaler/custom/Apex Neural Rendering FG".to_string(),
            package.payload_directory.join("Apex Neural Rendering FG"),
        )
    } else {
        (
            "custom/Apex Neural Rendering FG".to_string(),
            package.payload_directory.join("Apex Neural Rendering FG"),
        )
    };
    if target_directory.join(&custom_rel).exists() {
        return Err(AppError::new(ErrorCode::Conflict)
            .with("path", custom_rel)
            .with("reason", "an ANR custom provider already exists"));
    }

    let mut files = collect_files(&custom_source)?
        .into_iter()
        .map(|(relative, source)| {
            deployment_file(&source, &format!("{custom_rel}/{relative}"), "addon")
        })
        .collect::<Result<Vec<_>, _>>()?;

    let text = std::fs::read_to_string(&ini_path).map_err(|error| {
        AppError::new(ErrorCode::Internal)
            .with("path", ini_path.display().to_string())
            .with("reason", error.to_string())
    })?;
    let mut document = IniDocument::parse(&text);
    document.set_value("FrameGen", "FGInput", "nvngxfg");
    document.set_value("FrameGen", "FGNvngxReplacement", "Apex Neural Rendering FG");
    let content = document.to_text("\r\n").into_bytes();
    files.push(DeploymentFile {
        relative_path: "OptiScaler.ini".to_string(),
        source: None,
        content: Some(content.clone()),
        sha256: sha256_hex(&content),
        size: content.len() as i64,
        role: "config".to_string(),
    });

    apply(
        &files,
        target_directory,
        game_exe,
        paths,
        cancelled,
        progress,
    )?;
    Ok(AnrReport {
        kind: AnrPackageKind::Addon,
        files_written: files.len(),
        proxy_module: None,
    })
}

fn apply(
    files: &[DeploymentFile],
    target_directory: &Path,
    game_exe: Option<&Path>,
    paths: &AppPaths,
    cancelled: &AtomicBool,
    progress: Option<&dyn DeploymentProgress>,
) -> Result<(), AppError> {
    DeploymentEngine::apply(
        files,
        target_directory,
        "anr",
        "anr",
        "imported",
        &BTreeMap::new(),
        &[],
        game_exe,
        &paths.backups(),
        &paths.state().join("locks"),
        cancelled,
        progress,
    )?;
    Ok(())
}

fn collect_files(root: &Path) -> Result<Vec<(String, PathBuf)>, AppError> {
    let mut files = Vec::new();
    let mut stack = vec![(root.to_path_buf(), String::new())];
    while let Some((directory, prefix)) = stack.pop() {
        let entries = std::fs::read_dir(&directory).map_err(|error| {
            AppError::new(ErrorCode::Internal)
                .with("path", directory.display().to_string())
                .with("reason", error.to_string())
        })?;
        for entry in entries.flatten() {
            let path = entry.path();
            let name = entry.file_name().to_string_lossy().into_owned();
            let relative = if prefix.is_empty() {
                name
            } else {
                format!("{prefix}/{name}")
            };
            if path.is_dir() {
                stack.push((path, relative));
            } else if path.is_file() {
                files.push((relative, path));
            }
        }
    }
    files.sort_by(|left, right| left.0.cmp(&right.0));
    Ok(files)
}

fn deployment_file(source: &Path, target: &str, role: &str) -> Result<DeploymentFile, AppError> {
    let sha256 = FileHasher::sha256_file(source)?;
    let size = std::fs::metadata(source)
        .map_err(|error| {
            AppError::new(ErrorCode::Internal)
                .with("path", source.display().to_string())
                .with("reason", error.to_string())
        })?
        .len() as i64;
    Ok(DeploymentFile {
        relative_path: target.to_string(),
        source: Some(source.to_path_buf()),
        content: None,
        sha256,
        size,
        role: role.to_string(),
    })
}
