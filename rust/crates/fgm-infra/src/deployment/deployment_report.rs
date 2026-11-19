use fgm_domain::deployment::DeploymentManifestState;

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct DeploymentReport {
    pub state: DeploymentManifestState,
    pub files_written: usize,
    pub files_backed_up: usize,
    pub warnings: Vec<String>,
}
