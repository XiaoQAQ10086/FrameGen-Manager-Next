use std::io::{Read, Seek, SeekFrom};
use std::path::Path;

use fgm_domain::error::{AppError, ErrorCode};
use serde_json::json;

use crate::archive::ZipWriter;
use crate::gpu::GpuDetector;
use crate::library::LibraryService;
use crate::paths::AppPaths;
use crate::settings::SettingsStore;

const LOG_LIMIT: u64 = 4 * 1024 * 1024;

/// Builds the diagnostics bundle: log tail plus a non-sensitive summary.
pub struct DiagnosticsService;

impl DiagnosticsService {
    pub fn build_bundle(paths: &AppPaths) -> Result<Vec<u8>, AppError> {
        Self::build_bundle_with_log_limit(paths, LOG_LIMIT)
    }

    pub fn build_bundle_with_log_limit(
        paths: &AppPaths,
        log_limit: u64,
    ) -> Result<Vec<u8>, AppError> {
        let mut writer = ZipWriter::new();
        let log_path = paths.logs().join("fgm.log");
        if let Some(bytes) = read_tail(&log_path, log_limit) {
            writer.add_file("logs/fgm.log", &bytes);
        }

        let settings = SettingsStore::new(paths.config().join("app.json"))
            .load()
            .unwrap_or_default();
        let gpu = GpuDetector::detect_cached(paths);
        let summary = json!({
            "schemaVersion": 1,
            "version": fgm_domain::version::VERSION,
            "platform": "windows-x64",
            "games": LibraryService::list(paths).len(),
            "gpu": gpu.map(|gpu| json!({
                "name": gpu.name,
                "driverVersion": gpu.driver_version,
                "route": format!("{:?}", gpu.route),
                "driverSupported": gpu.driver_supported,
            })),
            "settings": {
                "language": format!("{:?}", settings.language),
                "autoScanOnStartup": settings.auto_scan_on_startup,
                "showCovers": settings.show_covers,
                "autoInstallReframework": settings.auto_install_reframework,
            },
        });
        writer.add_file(
            "summary.json",
            serde_json::to_vec_pretty(&summary)
                .map_err(|error| {
                    AppError::new(ErrorCode::Internal).with("reason", error.to_string())
                })?
                .as_slice(),
        );
        Ok(writer.finish())
    }

    pub fn export(paths: &AppPaths, destination: &Path) -> Result<(), AppError> {
        let bytes = Self::build_bundle(paths)?;
        std::fs::write(destination, bytes).map_err(|error| {
            let code = match error.kind() {
                std::io::ErrorKind::PermissionDenied => ErrorCode::AccessDenied,
                _ => ErrorCode::Internal,
            };
            AppError::new(code)
                .with("path", destination.display().to_string())
                .with("reason", error.to_string())
        })
    }
}

fn read_tail(path: &Path, limit: u64) -> Option<Vec<u8>> {
    let mut file = std::fs::File::open(path).ok()?;
    let length = file.metadata().ok()?.len();
    if length > limit {
        file.seek(SeekFrom::Start(length - limit)).ok()?;
    }
    let mut bytes = Vec::new();
    file.read_to_end(&mut bytes).ok()?;
    Some(bytes)
}
