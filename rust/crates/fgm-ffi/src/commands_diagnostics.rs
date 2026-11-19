use std::time::{SystemTime, UNIX_EPOCH};

use fgm_domain::error::AppError;
use fgm_infra::diagnostics::DiagnosticsService;
use fgm_infra::platform::FilePicker;
use serde::Deserialize;
use serde_json::{Value, json};

use super::commands::{data_paths, parse};

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct ExportRequest {
    #[serde(default)]
    data_dir: Option<String>,
}

pub(crate) fn export(request: &str) -> Result<Value, AppError> {
    let request: ExportRequest = parse(request)?;
    let paths = data_paths(request.data_dir.as_deref())?;
    paths.ensure_created()?;
    let name = format!("fgm-diagnostics-{}.zip", timestamp());
    let Some(destination) = FilePicker::save_zip(&name) else {
        return Ok(json!({ "ok": true, "cancelled": true }));
    };
    DiagnosticsService::export(&paths, &destination)?;
    Ok(json!({
        "ok": true,
        "path": destination.to_string_lossy(),
    }))
}

fn timestamp() -> u64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|value| value.as_secs())
        .unwrap_or_default()
}
