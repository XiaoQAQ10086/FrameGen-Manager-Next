#[link(name = "shell32")]
unsafe extern "system" {
    fn IsUserAnAdmin() -> i32;
}

/// Whether the current process runs with an elevated administrator token.
pub fn is_elevated() -> bool {
    unsafe { IsUserAnAdmin() != 0 }
}
