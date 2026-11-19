use std::path::PathBuf;

use serde::{Deserialize, Serialize};

use super::anr_package_kind::AnrPackageKind;

#[derive(Clone, Debug, Deserialize, Serialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct AnrPackageInfo {
    pub kind: AnrPackageKind,
    pub version: String,
    pub payload_directory: PathBuf,
}
