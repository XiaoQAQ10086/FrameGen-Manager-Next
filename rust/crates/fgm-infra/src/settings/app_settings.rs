use serde::{Deserialize, Serialize};

use super::app_language::AppLanguage;

#[derive(Clone, Debug, Deserialize, Serialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct AppSettings {
    #[serde(default = "default_schema_version")]
    pub schema_version: u32,
    #[serde(default)]
    pub language: AppLanguage,
    #[serde(default = "default_theme")]
    pub theme: String,
    #[serde(default)]
    pub auto_scan_on_startup: bool,
    #[serde(default = "default_true")]
    pub show_covers: bool,
    #[serde(default = "default_true")]
    pub auto_install_reframework: bool,
}

impl Default for AppSettings {
    fn default() -> Self {
        Self {
            schema_version: default_schema_version(),
            language: AppLanguage::System,
            theme: default_theme(),
            auto_scan_on_startup: false,
            show_covers: true,
            auto_install_reframework: true,
        }
    }
}

const fn default_schema_version() -> u32 {
    3
}

fn default_theme() -> String {
    "light".to_string()
}

const fn default_true() -> bool {
    true
}
