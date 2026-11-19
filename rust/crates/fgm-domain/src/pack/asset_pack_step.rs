use std::collections::BTreeMap;

use serde::{Deserialize, Serialize};

#[derive(Clone, Debug, Default, Deserialize, Serialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct AssetPackStep {
    #[serde(rename = "type", default)]
    pub kind: String,
    #[serde(default)]
    pub arguments: Option<BTreeMap<String, String>>,
}
