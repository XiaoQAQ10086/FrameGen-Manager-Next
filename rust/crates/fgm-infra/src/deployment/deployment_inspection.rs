use std::collections::BTreeMap;

use fgm_domain::deployment::DeploymentState;

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct DeploymentInspection {
    pub state: DeploymentState,
    pub pack_version: Option<String>,
    pub has_manifest: bool,
    pub details: Vec<String>,
    pub options: Option<BTreeMap<String, String>>,
}
