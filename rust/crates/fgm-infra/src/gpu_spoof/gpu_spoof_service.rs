use std::path::PathBuf;
use std::time::{SystemTime, UNIX_EPOCH};

use fgm_domain::error::{AppError, ErrorCode};
use serde::{Deserialize, Serialize};

use crate::fs::AtomicFile;
use crate::paths::AppPaths;

use super::display_adapter::DisplayAdapter;
use super::gpu_registry::{
    display_name, is_admin, read_dword, read_string, sub_keys, write_string,
};
use super::gpu_spoof_manifest::GpuSpoofManifest;

const CLASS_ROOT: &str =
    r"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
const ENUM_ROOT: &str = r"SYSTEM\CurrentControlSet\Enum\PCI";
const DISPLAY_CLASS_GUID: &str = "{4d36e968-e325-11ce-bfc1-08002be10318}";

/// Whitelisted adapter names; only these may be written.
pub const PRESETS: [&str; 2] = ["NVIDIA GeForce RTX 5090", "NVIDIA GeForce RTX 5060"];

#[derive(Clone, Copy, Debug, Deserialize, Serialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub enum GpuRestoreMode {
    Driver,
    Backup,
}

/// Inspected adapters, presets and the current backup state.
#[derive(Clone, Debug)]
pub struct GpuSpoofInspection {
    pub elevated: bool,
    pub adapters: Vec<DisplayAdapter>,
    pub presets: Vec<String>,
    pub applied: Option<GpuSpoofManifest>,
    /// Name reported by the running driver, not the (possibly stale) registry.
    pub live_name: Option<String>,
}

/// Registry-backed GPU name spoofing. Every write is backed up first.
pub struct GpuSpoofService;

impl GpuSpoofService {
    pub fn inspect(paths: &AppPaths) -> Result<GpuSpoofInspection, AppError> {
        Ok(GpuSpoofInspection {
            elevated: is_admin(),
            adapters: enumerate()?,
            presets: PRESETS.iter().map(|name| (*name).to_string()).collect(),
            applied: load_manifest(paths)?,
            live_name: crate::gpu::GpuDetector::detect().map(|gpu| gpu.name),
        })
    }

    pub fn apply(
        paths: &AppPaths,
        adapter_index: usize,
        preset: &str,
    ) -> Result<DisplayAdapter, AppError> {
        require_admin()?;
        let preset = PRESETS
            .iter()
            .find(|name| name.eq_ignore_ascii_case(preset))
            .copied()
            .ok_or_else(|| {
                AppError::new(ErrorCode::InvalidArgument)
                    .with("reason", "the adapter name is not whitelisted")
            })?;
        let adapters = enumerate()?;
        let adapter = adapters.get(adapter_index).ok_or_else(|| {
            AppError::new(ErrorCode::InvalidArgument).with("reason", "no such display adapter")
        })?;

        let manifest = match load_manifest(paths)? {
            Some(mut existing) if existing.instance_path == adapter.instance_path => {
                existing.applied_name = preset.to_string();
                existing.applied_at_unix_ms = now_unix_ms();
                existing
            }
            _ => GpuSpoofManifest::new(
                &adapter.instance_path,
                &adapter.device_desc,
                &adapter.driver_name,
                preset,
                now_unix_ms(),
            ),
        };
        save_manifest(paths, &manifest)?;
        verify_backup(paths, &manifest)?;

        write_string(&adapter.instance_path, "DeviceDesc", preset).map_err(write_error)?;
        let current = read_string(&adapter.instance_path, "DeviceDesc").unwrap_or_default();
        if current != preset {
            let _ = write_string(
                &adapter.instance_path,
                "DeviceDesc",
                &manifest.original_device_desc,
            );
            return Err(AppError::new(ErrorCode::Internal)
                .with("reason", "the adapter name could not be verified"));
        }
        let mut updated = adapter.clone();
        updated.device_desc = preset.to_string();
        updated.display_name = preset.to_string();
        Ok(updated)
    }

    pub fn restore(
        paths: &AppPaths,
        adapter_index: usize,
        mode: GpuRestoreMode,
    ) -> Result<DisplayAdapter, AppError> {
        require_admin()?;
        let adapters = enumerate()?;
        let adapter = adapters.get(adapter_index).ok_or_else(|| {
            AppError::new(ErrorCode::InvalidArgument).with("reason", "no such display adapter")
        })?;
        let manifest = load_manifest(paths)?.ok_or_else(|| {
            AppError::new(ErrorCode::ManifestMissing).with("reason", "no GPU name backup")
        })?;
        if manifest.instance_path != adapter.instance_path {
            return Err(AppError::new(ErrorCode::InvalidArgument)
                .with("reason", "the backup belongs to another adapter"));
        }
        let target = match mode {
            GpuRestoreMode::Driver => manifest.driver_name.clone(),
            GpuRestoreMode::Backup => manifest.original_device_desc.clone(),
        };
        write_string(&adapter.instance_path, "DeviceDesc", &target).map_err(write_error)?;
        let current = read_string(&adapter.instance_path, "DeviceDesc").unwrap_or_default();
        if current != target {
            return Err(AppError::new(ErrorCode::Internal)
                .with("reason", "the adapter name could not be restored"));
        }
        if mode == GpuRestoreMode::Backup {
            let _ = std::fs::remove_file(manifest_path(paths));
        }
        let mut updated = adapter.clone();
        updated.device_desc = target.clone();
        updated.display_name = display_name(&target);
        Ok(updated)
    }
}

pub(crate) fn enumerate() -> Result<Vec<DisplayAdapter>, AppError> {
    let mut adapters = Vec::new();
    for vendor in sub_keys(ENUM_ROOT) {
        if !vendor.to_uppercase().starts_with("VEN_10DE") {
            continue;
        }
        for instance in sub_keys(&format!(r"{ENUM_ROOT}\{vendor}")) {
            let instance_path = format!(r"{ENUM_ROOT}\{vendor}\{instance}");
            let class_guid = read_string(&instance_path, "ClassGUID").unwrap_or_default();
            let service = read_string(&instance_path, "Service").unwrap_or_default();
            if !class_guid.eq_ignore_ascii_case(DISPLAY_CLASS_GUID)
                || !service.eq_ignore_ascii_case("nvlddmkm")
            {
                continue;
            }
            // `CSConfigFlags = 1` marks a phantom adapter left behind by an old
            // driver or a device that is no longer present.
            if read_dword(&instance_path, "CSConfigFlags") == Some(1) {
                continue;
            }
            let Some(device_desc) = read_string(&instance_path, "DeviceDesc") else {
                continue;
            };
            let driver = read_string(&instance_path, "Driver").unwrap_or_default();
            let driver_index = driver
                .rsplit_once('\\')
                .map_or("", |(_, index)| index)
                .to_string();
            let driver_name = if driver_index.is_empty() {
                String::new()
            } else {
                read_string(&format!(r"{CLASS_ROOT}\{driver_index}"), "DriverDesc")
                    .unwrap_or_default()
            };
            let display = if driver_name.is_empty() {
                display_name(&device_desc)
            } else {
                driver_name.clone()
            };
            adapters.push(DisplayAdapter {
                instance_path,
                driver_index,
                driver_name,
                device_desc,
                display_name: display,
            });
        }
    }
    adapters.sort_by(|left, right| left.instance_path.cmp(&right.instance_path));
    Ok(adapters)
}

fn require_admin() -> Result<(), AppError> {
    if is_admin() {
        return Ok(());
    }
    Err(AppError::new(ErrorCode::AccessDenied).with(
        "reason",
        "administrator rights are required to change the adapter name",
    ))
}

fn manifest_path(paths: &AppPaths) -> PathBuf {
    paths.state().join("gpu-name").join("manifest.json")
}

fn load_manifest(paths: &AppPaths) -> Result<Option<GpuSpoofManifest>, AppError> {
    let path = manifest_path(paths);
    let bytes = match std::fs::read(&path) {
        Ok(bytes) => bytes,
        Err(error) if error.kind() == std::io::ErrorKind::NotFound => return Ok(None),
        Err(error) => {
            return Err(AppError::new(ErrorCode::FileNotFound)
                .with("path", path.display().to_string())
                .with("reason", error.to_string()));
        }
    };
    serde_json::from_slice(&bytes).map(Some).map_err(|error| {
        AppError::new(ErrorCode::Internal)
            .with("path", path.display().to_string())
            .with("reason", error.to_string())
    })
}

fn save_manifest(paths: &AppPaths, manifest: &GpuSpoofManifest) -> Result<(), AppError> {
    let directory = paths.state().join("gpu-name");
    std::fs::create_dir_all(&directory).map_err(|error| {
        AppError::new(ErrorCode::Internal)
            .with("path", directory.display().to_string())
            .with("reason", error.to_string())
    })?;
    let bytes = serde_json::to_vec_pretty(manifest)
        .map_err(|error| AppError::new(ErrorCode::Internal).with("reason", error.to_string()))?;
    AtomicFile::write(&directory.join("manifest.json"), &bytes)
}

fn verify_backup(paths: &AppPaths, expected: &GpuSpoofManifest) -> Result<(), AppError> {
    let stored = load_manifest(paths)?;
    if stored.as_ref() == Some(expected) {
        return Ok(());
    }
    Err(AppError::new(ErrorCode::Internal)
        .with("reason", "the GPU name backup could not be read back"))
}

fn write_error(reason: String) -> AppError {
    AppError::new(ErrorCode::AccessDenied).with("reason", reason)
}

fn now_unix_ms() -> i64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|value| value.as_millis() as i64)
        .unwrap_or_default()
}
