use super::pack_issue::PackIssue;

#[derive(Clone, Debug, Default, PartialEq, Eq)]
pub struct PackValidation {
    issues: Vec<PackIssue>,
}

impl PackValidation {
    pub fn new(issues: Vec<PackIssue>) -> Self {
        Self { issues }
    }

    pub fn ok() -> Self {
        Self::default()
    }

    pub fn is_valid(&self) -> bool {
        self.issues.is_empty()
    }

    pub fn issues(&self) -> &[PackIssue] {
        &self.issues
    }

    pub fn into_issues(self) -> Vec<PackIssue> {
        self.issues
    }
}
