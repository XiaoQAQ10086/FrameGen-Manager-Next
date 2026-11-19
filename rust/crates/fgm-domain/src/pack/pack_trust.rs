use serde::{Deserialize, Serialize};

/// How much the application trusts a pack's executable steps.
#[derive(Clone, Copy, Debug, Default, Deserialize, Serialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub enum PackTrust {
    #[default]
    Bundled,
    Verified,
}
