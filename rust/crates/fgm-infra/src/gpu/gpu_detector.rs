use std::path::Path;
use std::sync::atomic::AtomicBool;
use std::time::Duration;

use fgm_domain::gpu::GpuRoute;

use crate::process::ProcessRunner;

use super::gpu_info::GpuInfo;

/// Detects NVIDIA GPU name, driver version and Smooth Motion route.
pub struct GpuDetector;

impl GpuDetector {
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
        None
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
            driver_supported: driver_at_least(driver_version, 616, 64),
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
    fn parses_nvidia_smi_line() {
        let info = GpuDetector::parse_line("NVIDIA GeForce RTX 3070, 621.01").unwrap();
        assert_eq!(info.name, "NVIDIA GeForce RTX 3070");
        assert_eq!(info.driver_version, "621.01");
        assert!(info.driver_supported);
    }
}
