use super::pack_code::PackCode;

/// One schema problem. `diagnostic` is English technical text for CLI/logs and
/// must not be rendered directly in the UI; UI maps `code` to localized text.
#[derive(Clone, Debug, PartialEq, Eq)]
pub struct PackIssue {
    pub code: PackCode,
    pub field: String,
    pub diagnostic: String,
}

impl PackIssue {
    pub fn new(code: PackCode, field: impl Into<String>, diagnostic: impl Into<String>) -> Self {
        Self {
            code,
            field: field.into(),
            diagnostic: diagnostic.into(),
        }
    }
}
