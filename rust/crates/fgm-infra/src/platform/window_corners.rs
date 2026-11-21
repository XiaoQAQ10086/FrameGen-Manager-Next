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

#[cfg(windows)]
#[link(name = "gdi32")]
unsafe extern "system" {
    fn CreateRoundRectRgn(
        left: i32,
        top: i32,
        right: i32,
        bottom: i32,
        width: i32,
        height: i32,
    ) -> *mut c_void;
    fn DeleteObject(object: *mut c_void) -> i32;
}

#[cfg(windows)]
#[link(name = "user32")]
unsafe extern "system" {
    fn SetWindowRgn(window: *mut c_void, region: *mut c_void, redraw: i32) -> i32;
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

/// Windows 10 fallback: clips the window with a rounded region.
#[cfg(windows)]
pub fn set_rounded_region(hwnd: isize, radius: i32) -> bool {
    let window = hwnd as *mut c_void;
    let Some((left, top, right, bottom)) = super::window_state::window_rect(hwnd) else {
        return false;
    };
    let width = right - left;
    let height = bottom - top;
    let region = unsafe { CreateRoundRectRgn(0, 0, width + 1, height + 1, radius * 2, radius * 2) };
    if region.is_null() {
        return false;
    }
    if unsafe { SetWindowRgn(window, region, 1) } == 0 {
        unsafe { DeleteObject(region) };
        return false;
    }
    true
}

/// Removes the Windows 10 rounded region (used while maximized).
#[cfg(windows)]
pub fn clear_rounded_region(hwnd: isize) -> bool {
    unsafe { SetWindowRgn(hwnd as *mut c_void, std::ptr::null_mut(), 1) != 0 }
}

#[cfg(not(windows))]
pub fn set_rounded_corners(_hwnd: isize) -> bool {
    false
}

#[cfg(not(windows))]
pub fn set_rounded_region(_hwnd: isize, _radius: i32) -> bool {
    false
}

#[cfg(not(windows))]
pub fn clear_rounded_region(_hwnd: isize) -> bool {
    false
}
