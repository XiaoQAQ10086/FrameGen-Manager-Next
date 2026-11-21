use serde::{Deserialize, Serialize};

/// Persisted deployment state. Mirrors the runtime phase machine but is kept
/// in the domain so manifests do not depend on the core crate.
#[derive(Clone, Copy, Debug, Default, Deserialize, Serialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub enum DeploymentManifestState {
    #[default]
    Planned,
    BackingUp,
    Writing,
    Verifying,
    /// C# 2.x wrote this while an install was in progress.
    Installing,
    Installed,
    RolledBack,
    Partial,
    /// Pack files were removed on purpose while optional components (for
    /// example the ReShade proxy) were kept.
    Restored,
}
