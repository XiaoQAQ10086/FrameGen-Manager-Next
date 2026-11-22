//! Feedback log bundle staging. Upload itself happens in the Electron main
//! process so the upload key never enters the renderer.

use std::time::{SystemTime, UNIX_EPOCH};

use fgm_domain::error::{AppError, ErrorCode};
use fgm_infra::diagnostics::DiagnosticsService;
use fgm_infra::paths::AppPaths;
use serde_json::{Value, json};

const MAX_UPLOAD_BYTES: usize = 2 * 1024 * 1024;
const LOG_LIMIT: u64 = 1_500_000;

pub(crate) fn bundle() -> Result<Value, AppError> {
    let paths = AppPaths::create_default(None)?;
    paths.ensure_created()?;
    let bytes = DiagnosticsService::build_bundle_with_log_limit(&paths, LOG_LIMIT)?;
    if bytes.len() > MAX_UPLOAD_BYTES {
        return Err(AppError::new(ErrorCode::InvalidArgument)
            .with("size", bytes.len().to_string())
            .with("reason", "feedback log bundle exceeds 2 MB"));
    }
    let directory = paths.state().join("feedback");
    std::fs::create_dir_all(&directory).map_err(|error| {
        AppError::new(ErrorCode::Internal)
            .with("path", directory.display().to_string())
            .with("reason", error.to_string())
    })?;
    let stamp = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map_or(0, |value| value.as_secs());
    let path = directory.join(format!("logs-{}-{stamp}.zip", fgm_domain::version::VERSION));
    std::fs::write(&path, &bytes).map_err(|error| {
        AppError::new(ErrorCode::Internal)
            .with("path", path.display().to_string())
            .with("reason", error.to_string())
    })?;
    Ok(json!({
        "ok": true,
        "path": path.to_string_lossy(),
        "size": bytes.len(),
    }))
}
