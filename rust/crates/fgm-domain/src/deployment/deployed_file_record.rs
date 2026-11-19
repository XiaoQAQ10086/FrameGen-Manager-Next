use serde::{Deserialize, Serialize};

#[derive(Clone, Debug, Default, Deserialize, Serialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct DeployedFileRecord {
    #[serde(default)]
    pub relative_path: String,
    #[serde(default)]
    pub sha256: String,
    #[serde(default)]
    pub size: i64,
    #[serde(default)]
    pub existed_before: bool,
    #[serde(default)]
    pub backup_relative_path: Option<String>,
    #[serde(default)]
    pub role: String,
    #[serde(default)]
    pub last_write_time_utc_ticks: Option<i64>,
}
