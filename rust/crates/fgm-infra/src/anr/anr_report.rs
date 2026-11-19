use super::anr_package_kind::AnrPackageKind;

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct AnrReport {
    pub kind: AnrPackageKind,
    pub files_written: usize,
    pub proxy_module: Option<String>,
}
