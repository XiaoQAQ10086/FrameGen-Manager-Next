pub mod drive_roots;
pub mod elevation;
pub mod file_picker;
pub(crate) mod file_version;
pub mod icon_extract;
pub(crate) mod registry;
pub mod window_state;

pub use drive_roots::DriveRoots;
pub use elevation::is_elevated;
pub use file_picker::FilePicker;
pub use icon_extract::extract_exe_icon;
pub use window_state::WindowStateService;

/// Human readable Windows version from the registry (product name + build).
pub fn windows_version() -> String {
    const ROOT: &str = r"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
    let product = registry::read_string(registry::HKEY_LOCAL_MACHINE, ROOT, "ProductName")
        .unwrap_or_else(|| "Windows".to_string());
    let build = registry::read_string(registry::HKEY_LOCAL_MACHINE, ROOT, "CurrentBuild")
        .unwrap_or_default();
    let ubr = registry::read_dword(registry::HKEY_LOCAL_MACHINE, ROOT, "UBR").unwrap_or(0);
    if build.is_empty() {
        product
    } else {
        format!("{product} build {build}.{ubr}")
    }
}
