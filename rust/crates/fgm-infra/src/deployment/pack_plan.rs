use super::deployment_file::DeploymentFile;

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct PackPlan {
    pub files: Vec<DeploymentFile>,
    pub proxy_slot: Option<String>,
    pub warnings: Vec<String>,
}
