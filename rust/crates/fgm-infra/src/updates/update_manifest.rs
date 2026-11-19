use serde::{Deserialize, Serialize};

use super::update_installer::UpdateInstaller;

#[derive(Clone, Debug, Deserialize, Serialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct UpdateManifest {
    #[serde(default)]
    pub version: String,
    #[serde(default)]
    pub notes: String,
    #[serde(default)]
    pub page_url: Option<String>,
    pub installer: Option<UpdateInstaller>,
}
