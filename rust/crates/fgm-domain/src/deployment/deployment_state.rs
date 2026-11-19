#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
pub enum DeploymentState {
    #[default]
    NotDeployed,
    Installed,
    Outdated,
    Partial,
    Conflict,
    InstalledWithoutManifest,
}
