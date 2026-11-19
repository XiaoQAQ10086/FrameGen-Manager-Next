#[derive(Clone, Debug, PartialEq, Eq)]
pub struct ProcessResult {
    pub exit_code: i32,
    pub standard_output: String,
    pub standard_error: String,
    pub timed_out: bool,
}
