#[derive(Clone, Debug, PartialEq, Eq)]
pub struct SmoothMotionChange {
    pub ok: bool,
    pub enabled: bool,
    pub profile_names: Vec<String>,
    pub detail: String,
}
