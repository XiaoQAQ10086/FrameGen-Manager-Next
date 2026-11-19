use std::path::PathBuf;

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct DeploymentFile {
    pub relative_path: String,
    pub source: Option<PathBuf>,
    pub content: Option<Vec<u8>>,
    pub sha256: String,
    pub size: i64,
    pub role: String,
}
