use std::path::PathBuf;

use serde::{Deserialize, Serialize};

use super::graphics_api::GraphicsApi;
use super::library_source::LibrarySource;

#[derive(Clone, Debug, Deserialize, Serialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct GameEntry {
    pub id: String,
    pub name: String,
    pub source: LibrarySource,
    pub install_directory: PathBuf,
    pub render_exe: Option<PathBuf>,
    pub target_directory: PathBuf,
    pub graphics_api: GraphicsApi,
    pub app_id: Option<String>,
    pub is_manual: bool,
}
