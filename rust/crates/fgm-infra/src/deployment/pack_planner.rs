use std::collections::BTreeMap;
use std::path::Path;

use fgm_domain::error::{AppError, ErrorCode};
use fgm_domain::ini::{IniDocument, IniMerger};
use fgm_domain::pack::{AssetPack, AssetPackFile, AssetPackIni, IniSetting, PackValidator};

use crate::hashing::{FileHasher, sha256_hex};
use crate::paths::TargetPath;

use super::deployment_file::DeploymentFile;
use super::pack_plan::PackPlan;

/// Builds an engine-ready file list from a pack, selecting one proxy slot and
/// generating the merge-mode INI target.
pub struct PackDeploymentPlanner;

impl PackDeploymentPlanner {
    pub fn plan(
        pack: &AssetPack,
        pack_directory: &Path,
        target_directory: &Path,
        options: &BTreeMap<String, String>,
        ini_overrides: &[IniSetting],
        current_app_version: Option<&str>,
    ) -> Result<PackPlan, AppError> {
        let validation = PackValidator::validate(pack, current_app_version);
        if !validation.is_valid() {
            return Err(invalid_pack(&validation));
        }

        let pack_directory = std::path::absolute(pack_directory).map_err(|error| {
            AppError::new(ErrorCode::InvalidPath)
                .with("path", pack_directory.display().to_string())
                .with("reason", error.to_string())
        })?;
        let proxy_slot = select_proxy_slot(pack, options)?;

        let mut files = Vec::with_capacity(pack.files.len());
        for entry in &pack.files {
            let Some(file) = entry else {
                continue;
            };
            if is_proxy_slot(file)
                && proxy_slot
                    .as_deref()
                    .is_none_or(|selected| !file.target.eq_ignore_ascii_case(selected))
            {
                continue;
            }

            let source = TargetPath::combine(&pack_directory, &file.source)?;
            files.push(DeploymentFile {
                relative_path: file.target.clone(),
                source: Some(source),
                content: None,
                sha256: file.sha256.clone().unwrap_or_default(),
                size: file.size.unwrap_or(-1),
                role: file.role.clone(),
            });
        }

        if let Some(ini) = pack.ini.as_ref() {
            files.push(plan_ini(
                ini,
                &pack_directory,
                target_directory,
                ini_overrides,
            )?);
        }

        Ok(PackPlan {
            files,
            proxy_slot,
            warnings: Vec::new(),
        })
    }
}

fn select_proxy_slot(
    pack: &AssetPack,
    options: &BTreeMap<String, String>,
) -> Result<Option<String>, AppError> {
    let candidates: Vec<&AssetPackFile> = pack
        .files
        .iter()
        .flatten()
        .filter(|file| file.role.eq_ignore_ascii_case("proxy") && file.proxy_slot)
        .collect();

    let requested = options
        .iter()
        .find(|(key, _)| key.eq_ignore_ascii_case("proxySlot"))
        .map(|(_, value)| value.as_str())
        .filter(|value| !value.trim().is_empty());

    if let Some(requested) = requested {
        return candidates
            .iter()
            .find(|file| file.target.eq_ignore_ascii_case(requested))
            .map(|file| Some(file.target.clone()))
            .ok_or_else(|| {
                AppError::new(ErrorCode::InvalidArgument)
                    .with("proxySlot", requested)
                    .with(
                        "reason",
                        "the requested proxy slot is not declared by this pack",
                    )
            });
    }

    Ok(candidates.first().map(|file| file.target.clone()))
}

fn plan_ini(
    ini: &AssetPackIni,
    pack_directory: &Path,
    target_directory: &Path,
    overrides: &[IniSetting],
) -> Result<DeploymentFile, AppError> {
    let template_path = TargetPath::combine(pack_directory, &ini.template)?;
    let template_bytes = std::fs::read(&template_path).map_err(|error| {
        AppError::new(ErrorCode::PackInvalid)
            .with("path", template_path.display().to_string())
            .with("reason", error.to_string())
    })?;

    if template_bytes.len() as i64 != ini.template_size.unwrap_or(-1) {
        return Err(AppError::new(ErrorCode::PackHashMismatch)
            .with("path", template_path.display().to_string())
            .with("reason", "INI template size does not match pack.json"));
    }
    let actual_hash = FileHasher::sha256_file(&template_path)?;
    if !actual_hash.eq_ignore_ascii_case(ini.template_sha256.as_deref().unwrap_or("")) {
        return Err(AppError::new(ErrorCode::PackHashMismatch)
            .with("path", template_path.display().to_string())
            .with("reason", "INI template hash does not match pack.json"));
    }

    let template_text = String::from_utf8_lossy(&template_bytes);
    let template = IniDocument::parse(&template_text);
    let target_path = TargetPath::combine(target_directory, &ini.target)?;
    let target_text = if target_path.is_file() {
        let bytes = std::fs::read(&target_path).map_err(|error| {
            AppError::new(ErrorCode::Internal)
                .with("path", target_path.display().to_string())
                .with("reason", error.to_string())
        })?;
        String::from_utf8_lossy(&bytes).into_owned()
    } else {
        template_text.into_owned()
    };

    let mut document = IniDocument::parse(&target_text);
    let stock: Vec<IniSetting> = ini.stock_values.iter().flatten().cloned().collect();
    IniMerger::apply(&mut document, &template, &stock, overrides);

    let bytes = document.to_text("\r\n").into_bytes();
    Ok(DeploymentFile {
        relative_path: ini.target.clone(),
        source: None,
        content: Some(bytes.clone()),
        sha256: sha256_hex(&bytes),
        size: bytes.len() as i64,
        role: "config".to_string(),
    })
}

fn is_proxy_slot(file: &AssetPackFile) -> bool {
    file.role.eq_ignore_ascii_case("proxy") && file.proxy_slot
}

fn invalid_pack(validation: &fgm_domain::pack::PackValidation) -> AppError {
    let issues = validation
        .issues()
        .iter()
        .map(|issue| format!("{} {}", issue.code.as_str(), issue.field))
        .collect::<Vec<_>>()
        .join(", ");
    AppError::new(ErrorCode::PackInvalid).with("issues", issues)
}
