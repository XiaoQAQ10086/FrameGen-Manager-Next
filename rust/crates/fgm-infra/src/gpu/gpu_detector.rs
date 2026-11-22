use std::path::Path;
use std::sync::atomic::AtomicBool;
use std::sync::{Mutex, OnceLock};
use std::time::{Duration, Instant, SystemTime, UNIX_EPOCH};

use fgm_domain::gpu::GpuRoute;
use serde::{Deserialize, Serialize};

use crate::fs::AtomicFile;
use crate::paths::AppPaths;
use crate::platform::registry::{HKEY_LOCAL_MACHINE, read_string};
use crate::process::ProcessRunner;

use super::gpu_info::GpuInfo;
use super::hags::{HagsProbe, HagsState};

const MEMORY_TTL: Duration = Duration::from_secs(60);
const DISK_TTL_SECONDS: u64 = 24 * 60 * 60;

struct MemoryEntry {
    at: Instant,
    fingerprint: Option<String>,
    info: GpuInfo,
}

#[derive(Deserialize, Serialize)]
struct DiskEntry {
    saved_at: u64,
    fingerprint: Option<String>,
    name: String,
    driver_version: String,
    route: GpuRoute,
    driver_supported: bool,
}

static CACHE: OnceLock<Mutex<Option<MemoryEntry>>> = OnceLock::new();

/// Detects NVIDIA GPU name, driver version and Smooth Motion route.
pub struct GpuDetector;

impl GpuDetector {
    /// Returns a cached result when it is still fresh, otherwise detects once.
    ///
    /// The cache is in memory for 60 seconds and on disk for 24 hours. Disk
    /// entries are ignored when the display driver version changed.
    pub fn detect_cached(paths: &AppPaths) -> Option<GpuInfo> {
        let cache = CACHE.get_or_init(|| Mutex::new(None));
        let mut guard = cache.lock().ok()?;
        let fingerprint = adapter_driver_fingerprint();
        if let Some(entry) = guard.as_ref()
            && entry.at.elapsed() < MEMORY_TTL
            && entry.fingerprint == fingerprint
        {
            return Some(fresh_hags(&entry.info));
        }
        if let Some(info) = load_disk(paths, fingerprint.as_deref()) {
            *guard = Some(MemoryEntry {
                at: Instant::now(),
                fingerprint,
                info: info.clone(),
            });
            return Some(fresh_hags(&info));
        }
        let info = Self::detect()?;
        save_disk(paths, &info, fingerprint.as_deref());
        *guard = Some(MemoryEntry {
            at: Instant::now(),
            fingerprint,
            info: info.clone(),
        });
        Some(info)
    }

    /// Drops both cache layers; used after registry GPU name changes.
    pub fn invalidate_cache(paths: &AppPaths) {
        if let Some(cache) = CACHE.get()
            && let Ok(mut guard) = cache.lock()
        {
            *guard = None;
        }
        let _ = std::fs::remove_file(disk_path(paths));
    }

    pub fn detect() -> Option<GpuInfo> {
        let result = ProcessRunner::run(
            Path::new("nvidia-smi.exe"),
            &[
                "--query-gpu=name,driver_version".to_string(),
                "--format=csv,noheader".to_string(),
            ],
            None,
            Duration::from_secs(5),
            &AtomicBool::new(false),
        )
        .ok()?;

        if !result.timed_out && result.exit_code == 0 {
            for line in result.standard_output.lines() {
                if let Some(info) = Self::parse_line(line) {
                    return Some(info);
                }
            }
        }
        registry_adapter().map(|(name, driver)| Self::classify(&name, &driver))
    }

    pub fn classify(name: &str, driver_version: &str) -> GpuInfo {
        let upper = name.to_uppercase();
        let route = if upper.contains("RTX 50") || upper.contains("RTX 40") {
            GpuRoute::NotNeeded
        } else if upper.contains("AMD") || upper.contains("RADEON") || upper.contains("INTEL") {
            GpuRoute::Unsupported
        } else if upper.contains("QUADRO RTX") {
            GpuRoute::Sm75
        } else if upper.contains("RTX A") || upper.contains("RTX 2050") || upper.contains("RTX 30")
        {
            GpuRoute::Sm86
        } else if upper.contains("GTX 16") {
            GpuRoute::Gtx16
        } else if upper.contains("RTX 20") || upper.contains("TITAN RTX") {
            GpuRoute::Sm75
        } else if upper.contains("GTX 10")
            || upper.contains("GTX 9")
            || upper.contains("GTX 7")
            || upper.contains("GT 10")
            || upper.contains("GT 9")
            || upper.contains("MX1")
            || upper.contains("QUADRO")
            || upper.contains("TESLA")
        {
            GpuRoute::Pascal
        } else {
            GpuRoute::Unsupported
        };

        GpuInfo {
            name: name.to_string(),
            driver_version: driver_version.to_string(),
            route,
            driver_supported: route == GpuRoute::Unsupported
                || driver_at_least(driver_version, 616, 64),
            hags: HagsProbe::read(),
        }
    }

    fn parse_line(line: &str) -> Option<GpuInfo> {
        let (name, driver) = line.split_once(',')?;
        let name = name.trim();
        let driver = driver.trim();
        if name.is_empty() || driver.is_empty() {
            return None;
        }
        Some(Self::classify(name, driver))
    }
}

fn fresh_hags(info: &GpuInfo) -> GpuInfo {
    let mut copy = info.clone();
    copy.hags = HagsProbe::read();
    copy
}

fn disk_path(paths: &AppPaths) -> std::path::PathBuf {
    paths.cache().join("gpu.json")
}

fn load_disk(paths: &AppPaths, fingerprint: Option<&str>) -> Option<GpuInfo> {
    let bytes = std::fs::read(disk_path(paths)).ok()?;
    let entry: DiskEntry = serde_json::from_slice(&bytes).ok()?;
    let now = SystemTime::now().duration_since(UNIX_EPOCH).ok()?.as_secs();
    if now.saturating_sub(entry.saved_at) >= DISK_TTL_SECONDS {
        return None;
    }
    if entry.fingerprint.as_deref() != fingerprint {
        return None;
    }
    Some(GpuInfo {
        name: entry.name,
        driver_version: entry.driver_version,
        route: entry.route,
        driver_supported: entry.driver_supported,
        hags: HagsState::Unknown,
    })
}

fn save_disk(paths: &AppPaths, info: &GpuInfo, fingerprint: Option<&str>) {
    let Ok(saved_at) = SystemTime::now().duration_since(UNIX_EPOCH) else {
        return;
    };
    let entry = DiskEntry {
        saved_at: saved_at.as_secs(),
        fingerprint: fingerprint.map(str::to_string),
        name: info.name.clone(),
        driver_version: info.driver_version.clone(),
        route: info.route,
        driver_supported: info.driver_supported,
    };
    if let Ok(bytes) = serde_json::to_vec_pretty(&entry) {
        let _ = AtomicFile::write(&disk_path(paths), &bytes);
    }
}

/// Cheap display-driver fingerprint from the registry, without spawning nvidia-smi.
fn adapter_driver_fingerprint() -> Option<String> {
    registry_adapter().map(|(_, driver)| driver)
}

fn registry_adapter() -> Option<(String, String)> {
    const CLASS: &str =
        r"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
    let mut best: Option<(u8, String, String)> = None;
    for index in 0..32 {
        let key = format!(r"{CLASS}\{index:04}");
        let Some(desc) = read_string(HKEY_LOCAL_MACHINE, &key, "DriverDesc") else {
            continue;
        };
        if is_virtual_adapter(&desc) {
            continue;
        }
        let Some(raw) = read_string(HKEY_LOCAL_MACHINE, &key, "DriverVersion") else {
            continue;
        };
        let upper = desc.to_uppercase();
        let score = if upper.contains("NVIDIA") {
            4
        } else if upper.contains("AMD") || upper.contains("RADEON") {
            3
        } else if upper.contains("INTEL") {
            2
        } else {
            1
        };
        let driver = if upper.contains("NVIDIA") {
            normalize_driver_version(&raw)
        } else {
            raw
        };
        if best
            .as_ref()
            .is_none_or(|(best_score, _, _)| score > *best_score)
        {
            best = Some((score, desc, driver));
        }
    }
    best.map(|(_, name, driver)| (name, driver))
}

fn is_virtual_adapter(name: &str) -> bool {
    let upper = name.to_uppercase();
    [
        "MICROSOFT BASIC",
        "VIRTUAL",
        "REMOTE",
        "PARSEC",
        "GAMEVIEWER",
        "SUNSHINE",
        "SPACEDESK",
    ]
    .iter()
    .any(|marker| upper.contains(marker))
}

/// Converts the registry form `32.0.16.2124` to the nvidia-smi form `621.24`.
fn normalize_driver_version(raw: &str) -> String {
    let parts = raw
        .split('.')
        .filter_map(|part| part.parse::<u32>().ok())
        .collect::<Vec<_>>();
    if parts.len() != 4 {
        return raw.to_string();
    }
    let major = parts[2].saturating_sub(10);
    let build = parts[3];
    format!("{}.{:02}", major * 100 + build / 100, build % 100)
}

fn driver_at_least(value: &str, major: u32, minor: u32) -> bool {
    let mut parts = value.trim().split('.');
    let Some(major_value) = parts.next().and_then(|part| part.parse::<u32>().ok()) else {
        return false;
    };
    let minor_value = parts
        .next()
        .and_then(|part| part.parse::<u32>().ok())
        .unwrap_or(0);
    (major_value, minor_value) >= (major, minor)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn classifies_known_routes() {
        assert_eq!(
            GpuDetector::classify("NVIDIA GeForce RTX 3070", "621.01").route,
            GpuRoute::Sm86
        );
        assert_eq!(
            GpuDetector::classify("NVIDIA GeForce RTX 2080", "621.01").route,
            GpuRoute::Sm75
        );
        assert_eq!(
            GpuDetector::classify("NVIDIA GeForce RTX 4090", "621.01").route,
            GpuRoute::NotNeeded
        );
        assert_eq!(
            GpuDetector::classify("NVIDIA GeForce GTX 1660", "621.01").route,
            GpuRoute::Gtx16
        );
        assert_eq!(
            GpuDetector::classify("NVIDIA GeForce RTX 2050", "621.01").route,
            GpuRoute::Sm86
        );
        assert_eq!(
            GpuDetector::classify("NVIDIA TITAN RTX", "621.01").route,
            GpuRoute::Sm75
        );
        assert_eq!(
            GpuDetector::classify("AMD Radeon RX 7900", "1.0").route,
            GpuRoute::Unsupported
        );
    }

    #[test]
    fn checks_driver_threshold() {
        assert!(!driver_at_least("616.63", 616, 64));
        assert!(driver_at_least("616.64", 616, 64));
        assert!(driver_at_least("621.01", 616, 64));
        assert!(!driver_at_least("unknown", 616, 64));
    }

    #[test]
    fn skips_virtual_adapters() {
        assert!(is_virtual_adapter("GameViewer Virtual Display Adapter"));
        assert!(is_virtual_adapter("Microsoft Basic Display Adapter"));
        assert!(!is_virtual_adapter("NVIDIA GeForce RTX 3070"));
        assert!(!is_virtual_adapter("AMD Radeon RX 7900 XTX"));
    }

    #[test]
    fn amd_and_intel_are_not_marked_driver_too_old() {
        assert!(GpuDetector::classify("AMD Radeon RX 7900", "31.0.12027.9001").driver_supported);
        assert!(GpuDetector::classify("Intel(R) Arc(TM) A770", "31.0.101.5333").driver_supported);
    }

    #[test]
    fn normalizes_registry_driver_versions() {
        assert_eq!(normalize_driver_version("32.0.16.2124"), "621.24");
        assert_eq!(normalize_driver_version("32.0.15.6094"), "560.94");
        assert_eq!(normalize_driver_version("unknown"), "unknown");
    }

    #[test]
    fn parses_nvidia_smi_line() {
        let info = GpuDetector::parse_line("NVIDIA GeForce RTX 3070, 621.01").unwrap();
        assert_eq!(info.name, "NVIDIA GeForce RTX 3070");
        assert_eq!(info.driver_version, "621.01");
        assert!(info.driver_supported);
    }
}
