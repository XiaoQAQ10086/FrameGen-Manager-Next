use std::path::PathBuf;

/// Native file dialog for user-selected assets.
pub struct FilePicker;

impl FilePicker {
    pub fn pick_anr_zip() -> Option<PathBuf> {
        rfd::FileDialog::new()
            .set_title("Select an ANR package")
            .add_filter("ANR package", &["zip"])
            .pick_file()
    }

    pub fn pick_executable(title: Option<&str>) -> Option<PathBuf> {
        rfd::FileDialog::new()
            .set_title(title.unwrap_or("Select the game executable"))
            .add_filter("Executable", &["exe"])
            .pick_file()
    }

    pub fn save_zip(default_name: &str) -> Option<PathBuf> {
        rfd::FileDialog::new()
            .set_title("Save diagnostics bundle")
            .add_filter("ZIP", &["zip"])
            .set_file_name(default_name)
            .save_file()
    }
}
