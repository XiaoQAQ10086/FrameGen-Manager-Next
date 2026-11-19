use serde::{Deserialize, Serialize};

#[derive(Clone, Debug, Deserialize, Serialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct SmProfileTarget {
    pub profile: String,
    pub ours: bool,
    pub had: bool,
    pub value: Option<u32>,
}
