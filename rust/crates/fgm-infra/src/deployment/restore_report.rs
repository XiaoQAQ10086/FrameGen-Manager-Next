#[derive(Clone, Debug, Default, PartialEq, Eq)]
pub struct RestoreReport {
    pub restored_files: usize,
    pub removed_files: usize,
    pub conflicts: Vec<String>,
    pub warnings: Vec<String>,
}
