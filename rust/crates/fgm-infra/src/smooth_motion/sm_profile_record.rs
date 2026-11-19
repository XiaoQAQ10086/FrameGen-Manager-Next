use serde::{Deserialize, Serialize};

use super::sm_profile_target::SmProfileTarget;

#[derive(Clone, Debug, Deserialize, Serialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct SmProfileRecord {
    #[serde(default = "default_schema_version")]
    pub schema_version: u32,
    pub game: String,
    pub exe_path: String,
    pub target_directory: String,
    pub targets: Vec<SmProfileTarget>,
}

impl SmProfileRecord {
    pub fn new(
        game: impl Into<String>,
        exe_path: impl Into<String>,
        target_directory: impl Into<String>,
        targets: Vec<SmProfileTarget>,
    ) -> Self {
        Self {
            schema_version: 1,
            game: game.into(),
            exe_path: exe_path.into(),
            target_directory: target_directory.into(),
            targets,
        }
    }

    pub fn profile_names(&self) -> Vec<&str> {
        self.targets
            .iter()
            .map(|target| target.profile.as_str())
            .collect()
    }
}

const fn default_schema_version() -> u32 {
    1
}
