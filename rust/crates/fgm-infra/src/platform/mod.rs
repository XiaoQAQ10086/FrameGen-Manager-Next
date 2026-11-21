pub mod drive_roots;
pub mod elevation;
pub mod file_picker;
pub(crate) mod file_version;
pub mod icon_extract;
pub(crate) mod registry;
pub mod window_corners;
pub mod window_state;

pub use drive_roots::DriveRoots;
pub use elevation::is_elevated;
pub use file_picker::FilePicker;
pub use file_version::product_name;
pub use icon_extract::extract_exe_icon;
pub use window_corners::set_rounded_corners;
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

/// Reads the WinINET proxy (used by browsers/Flutter) so WinHTTP downloads can
/// follow the same proxy as the launcher.
pub fn system_proxy() -> Option<String> {
    const ROOT: &str = r"Software\Microsoft\Windows\CurrentVersion\Internet Settings";
    let enabled = registry::read_dword(registry::HKEY_CURRENT_USER, ROOT, "ProxyEnable")?;
    if enabled == 0 {
        return None;
    }
    let raw = registry::read_string(registry::HKEY_CURRENT_USER, ROOT, "ProxyServer")?;
    if raw.trim().is_empty() {
        return None;
    }
    if let Some((_, value)) = raw.split_once('=') {
        for part in raw.split(';') {
            if let Some((scheme, value)) = part.split_once('=')
                && (scheme.eq_ignore_ascii_case("https") || scheme.eq_ignore_ascii_case("http"))
            {
                return Some(value.trim().to_string());
            }
        }
        return Some(value.trim().to_string());
    }
    Some(raw.trim().to_string())
}
