//! Window frame helpers that are not exposed through the command bridge.

#[cfg(windows)]
use std::ffi::c_void;

#[cfg(windows)]
const DWMWA_WINDOW_CORNER_PREFERENCE: u32 = 33;
#[cfg(windows)]
const DWMWCP_ROUND: u32 = 2;

#[cfg(windows)]
#[link(name = "dwmapi")]
unsafe extern "system" {
    fn DwmSetWindowAttribute(
        window: *mut c_void,
        attribute: u32,
        value: *const c_void,
        size: u32,
    ) -> i32;
}

/// Asks DWM to round the window corners (Windows 11+).
///
/// Older systems reject the attribute; the window then keeps its square frame.
#[cfg(windows)]
pub fn set_rounded_corners(hwnd: isize) -> bool {
    let value = DWMWCP_ROUND;
    let result = unsafe {
        DwmSetWindowAttribute(
            hwnd as *mut c_void,
            DWMWA_WINDOW_CORNER_PREFERENCE,
            (&value as *const u32).cast(),
            std::mem::size_of::<u32>() as u32,
        )
    };
    result == 0
}

#[cfg(not(windows))]
pub fn set_rounded_corners(_hwnd: isize) -> bool {
    false
}
