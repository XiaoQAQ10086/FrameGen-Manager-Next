use serde::{Deserialize, Serialize};

use super::ini_setting::IniSetting;

#[derive(Clone, Debug, Default, Deserialize, Serialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct AssetPackIni {
    #[serde(default)]
    pub target: String,
    #[serde(default)]
    pub template: String,
    #[serde(default = "default_mode")]
    pub mode: String,
    #[serde(default)]
    pub template_sha256: Option<String>,
    #[serde(default)]
    pub template_size: Option<i64>,
    #[serde(default)]
    pub stock_values: Vec<Option<IniSetting>>,
}

fn default_mode() -> String {
    "merge".to_string()
}
