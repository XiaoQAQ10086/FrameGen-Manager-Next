use serde::{Deserialize, Serialize};

use super::asset_pack_file::AssetPackFile;
use super::asset_pack_ini::AssetPackIni;
use super::asset_pack_step::AssetPackStep;
use super::pack_trust::PackTrust;

/// Parsed `pack.json`. The schema is data; deployment rules live in core.
#[derive(Clone, Debug, Default, Deserialize, Serialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct AssetPack {
    #[serde(default)]
    pub schema_version: u32,
    #[serde(default)]
    pub id: String,
    #[serde(default)]
    pub version: String,
    #[serde(default)]
    pub display_name: String,
    #[serde(default)]
    pub min_app_version: String,
    #[serde(default)]
    pub gpu_routes: Vec<String>,
    #[serde(default)]
    pub files: Vec<Option<AssetPackFile>>,
    #[serde(default)]
    pub owned_directories: Vec<String>,
    #[serde(default)]
    pub ini: Option<AssetPackIni>,
    #[serde(default)]
    pub steps: Vec<Option<AssetPackStep>>,
    #[serde(default)]
    pub trust: PackTrust,
}
