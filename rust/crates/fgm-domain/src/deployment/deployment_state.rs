use serde::{Deserialize, Serialize};

#[derive(Clone, Copy, Debug, Default, Deserialize, Serialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub enum DeploymentState {
    #[default]
    NotDeployed,
    Installed,
    Outdated,
    Partial,
    Conflict,
    InstalledWithoutManifest,
}
