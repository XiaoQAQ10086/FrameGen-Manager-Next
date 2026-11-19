use std::path::PathBuf;

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct RenderExe {
    pub path: PathBuf,
    pub score: i32,
    pub size: u64,
}
