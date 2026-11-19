#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum ErrorCode {
    Unknown,
    InvalidArgument,
    InvalidPath,
    UnsafePath,
    FileNotFound,
    FileLocked,
    GameRunning,
    AccessDenied,
    Conflict,
    ManifestMissing,
    PackInvalid,
    PackNotFound,
    PackHashMismatch,
    InvalidPe,
    NvApiUnavailable,
    NvApiCallFailed,
    OperationCancelled,
    OperationTimedOut,
    Internal,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct AppError {
    pub code: ErrorCode,
    pub context: Vec<(String, String)>,
}

impl AppError {
    pub fn new(code: ErrorCode) -> Self {
        Self {
            code,
            context: Vec::new(),
        }
    }

    pub fn with(mut self, key: impl Into<String>, value: impl Into<String>) -> Self {
        self.context.push((key.into(), value.into()));
        self
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn error_keeps_structured_context() {
        let error = AppError::new(ErrorCode::Conflict)
            .with("path", "C:/game.dll")
            .with("owner", "ANR");

        assert_eq!(error.code, ErrorCode::Conflict);
        assert_eq!(error.context.len(), 2);
    }
}
