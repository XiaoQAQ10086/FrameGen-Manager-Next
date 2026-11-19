use serde::{Deserialize, Serialize};

/// Backup written before the first spoof; kept until the original is restored.
#[derive(Clone, Debug, Deserialize, Serialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct GpuSpoofManifest {
    pub schema_version: u32,
    pub instance_path: String,
    pub original_device_desc: String,
    pub driver_name: String,
    pub applied_name: String,
    pub applied_at_unix_ms: i64,
}

impl GpuSpoofManifest {
    pub fn new(
        instance_path: &str,
        original_device_desc: &str,
        driver_name: &str,
        applied_name: &str,
        applied_at_unix_ms: i64,
    ) -> Self {
        Self {
            schema_version: 1,
            instance_path: instance_path.to_string(),
            original_device_desc: original_device_desc.to_string(),
            driver_name: driver_name.to_string(),
            applied_name: applied_name.to_string(),
            applied_at_unix_ms,
        }
    }
}
