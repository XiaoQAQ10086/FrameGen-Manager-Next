use serde::{Deserialize, Serialize};

#[derive(Clone, Debug, Default, Deserialize, Serialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct IniSetting {
    #[serde(default)]
    pub section: String,
    #[serde(default)]
    pub key: String,
    #[serde(default)]
    pub value: String,
}
