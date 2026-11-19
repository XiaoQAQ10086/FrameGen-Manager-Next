use std::collections::BTreeMap;

use serde::{Deserialize, Serialize};

use super::deployed_file_record::DeployedFileRecord;
use super::deployment_manifest_state::DeploymentManifestState;

#[derive(Clone, Debug, Deserialize, Serialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct DeploymentManifest {
    #[serde(default = "default_schema_version")]
    pub schema_version: u32,
    #[serde(default)]
    pub family_id: String,
    #[serde(default)]
    pub pack_id: String,
    #[serde(default)]
    pub pack_version: String,
    #[serde(default)]
    pub target_directory: String,
    #[serde(default)]
    pub created_unix_ms: i64,
    #[serde(default)]
    pub state: DeploymentManifestState,
    #[serde(default)]
    pub files: Vec<DeployedFileRecord>,
    #[serde(default)]
    pub owned_directories: Vec<String>,
    #[serde(default)]
    pub options: BTreeMap<String, String>,
}

impl Default for DeploymentManifest {
    fn default() -> Self {
        Self {
            schema_version: default_schema_version(),
            family_id: String::new(),
            pack_id: String::new(),
            pack_version: String::new(),
            target_directory: String::new(),
            created_unix_ms: 0,
            state: DeploymentManifestState::default(),
            files: Vec::new(),
            owned_directories: Vec::new(),
            options: BTreeMap::new(),
        }
    }
}

const fn default_schema_version() -> u32 {
    1
}
