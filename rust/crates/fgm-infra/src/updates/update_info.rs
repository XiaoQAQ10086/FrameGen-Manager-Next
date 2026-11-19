use super::update_manifest::UpdateManifest;

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct UpdateInfo {
    pub available: bool,
    pub current_version: String,
    pub manifest: UpdateManifest,
}
