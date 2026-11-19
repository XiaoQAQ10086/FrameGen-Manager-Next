/// Receives file-level progress from the deployment engine.
pub trait DeploymentProgress: Send + Sync {
    /// Called once before the first file is written.
    fn start(&self, total: usize);
    /// Called after a file was written successfully.
    fn advance(&self);
}
