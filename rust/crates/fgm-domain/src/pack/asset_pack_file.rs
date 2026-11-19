use serde::{Deserialize, Serialize};

#[derive(Clone, Debug, Deserialize, Serialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct AssetPackFile {
    #[serde(default)]
    pub source: String,
    #[serde(default)]
    pub target: String,
    #[serde(default)]
    pub role: String,
    #[serde(default = "default_required")]
    pub required: bool,
    #[serde(default)]
    pub proxy_slot: bool,
    #[serde(default)]
    pub sha256: Option<String>,
    #[serde(default)]
    pub size: Option<i64>,
}

fn default_required() -> bool {
    true
}
