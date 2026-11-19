#[derive(Clone, Debug, Default, PartialEq, Eq)]
pub struct SmoothMotionState {
    pub enabled: Option<bool>,
    pub profile_names: Vec<String>,
    pub ours: bool,
    pub detail: Option<String>,
}
