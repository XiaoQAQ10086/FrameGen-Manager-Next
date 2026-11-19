use fgm_domain::deployment::DeploymentState;

/// Read-only DLSS5 state for the UI: pack deployment plus ReShade and legacy
/// RenoDX residue that lives outside the deployment manifest.
#[derive(Clone, Debug, PartialEq, Eq)]
pub struct Dlss5Inspection {
    pub state: DeploymentState,
    pub has_manifest: bool,
    pub pack_version: Option<String>,
    pub proxy_module: Option<String>,
    pub re_shade_present: bool,
    pub hotkey_vk: Option<u32>,
    pub legacy_files: Vec<String>,
    pub details: Vec<String>,
}
