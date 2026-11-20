use std::sync::atomic::{AtomicBool, Ordering};

use fgm_domain::version::VERSION;
use fgm_infra::paths::AppPaths;

static STARTUP_LOGGED: AtomicBool = AtomicBool::new(false);

/// Appends one structured line to `logs/fgm.log`; failures stay silent.
pub(crate) fn log_event(paths: &AppPaths, level: &str, message: &str, fields: &[(&str, &str)]) {
    let operation_id = std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)
        .map_or(0, |value| value.as_millis() as u64);
    let log = fgm_infra::logging::OperationLog::new(paths.logs().join("fgm.log"));
    let _ = log.append(operation_id, level, message, fields);
}

/// Writes the one-time startup banner with environment and GPU details.
pub(crate) fn log_startup(paths: &AppPaths, settings: &fgm_infra::settings::AppSettings) {
    if STARTUP_LOGGED.swap(true, Ordering::Relaxed) {
        return;
    }
    let portable = if paths.is_portable() { "true" } else { "false" };
    let data_dir = paths.root().display().to_string();
    let os = fgm_infra::platform::windows_version();
    log_event(
        paths,
        "info",
        "app start",
        &[
            ("version", VERSION),
            ("portable", portable),
            ("dataDir", &data_dir),
            ("os", &os),
        ],
    );
    if let Some(gpu) = fgm_infra::gpu::GpuDetector::detect() {
        let route = format!("{:?}", gpu.route);
        let hags = format!("{:?}", gpu.hags);
        log_event(
            paths,
            "info",
            "gpu detected",
            &[
                ("name", &gpu.name),
                ("driver", &gpu.driver_version),
                ("route", &route),
                ("hags", &hags),
            ],
        );
    }
    let auto_scan = if settings.auto_scan_on_startup {
        "true"
    } else {
        "false"
    };
    let language = format!("{:?}", settings.language);
    log_event(
        paths,
        "info",
        "settings loaded",
        &[("autoScanOnStartup", auto_scan), ("language", &language)],
    );
    let games = fgm_infra::library::LibraryService::list(paths)
        .len()
        .to_string();
    log_event(paths, "info", "library cache loaded", &[("games", &games)]);
}
