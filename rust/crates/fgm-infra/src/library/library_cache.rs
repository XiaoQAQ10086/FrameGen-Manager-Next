use std::collections::BTreeMap;

use fgm_domain::library::GameEntry;
use serde::{Deserialize, Serialize};

#[derive(Clone, Debug, Deserialize, Serialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct LibraryCache {
    #[serde(default = "default_schema_version")]
    pub schema_version: u32,
    #[serde(default)]
    pub games: Vec<GameEntry>,
    #[serde(default)]
    pub ignored_ids: Vec<String>,
    #[serde(default)]
    pub target_overrides: BTreeMap<String, String>,
}

impl Default for LibraryCache {
    fn default() -> Self {
        Self {
            schema_version: default_schema_version(),
            games: Vec::new(),
            ignored_ids: Vec::new(),
            target_overrides: BTreeMap::new(),
        }
    }
}

const fn default_schema_version() -> u32 {
    1
}
