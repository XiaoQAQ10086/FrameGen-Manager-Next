use fgm_domain::error::{AppError, ErrorCode};
use serde::{Deserialize, Serialize};

use crate::fs::AtomicFile;
use crate::paths::AppPaths;

type Hwnd = isize;

const GW_OWNER: u32 = 4;
const SW_MAXIMIZE: i32 = 3;
const SWP_NOZORDER: u32 = 0x0004;
const SWP_NOACTIVATE: u32 = 0x0010;

#[repr(C)]
struct Rect {
    left: i32,
    top: i32,
    right: i32,
    bottom: i32,
}

#[link(name = "user32")]
unsafe extern "system" {
    fn EnumWindows(callback: unsafe extern "system" fn(Hwnd, isize) -> i32, lparam: isize) -> i32;
    fn GetWindowThreadProcessId(hwnd: Hwnd, process_id: *mut u32) -> u32;
    fn IsWindowVisible(hwnd: Hwnd) -> i32;
    fn GetWindow(hwnd: Hwnd, command: u32) -> Hwnd;
    fn GetWindowRect(hwnd: Hwnd, rect: *mut Rect) -> i32;
    fn IsZoomed(hwnd: Hwnd) -> i32;
    fn IsIconic(hwnd: Hwnd) -> i32;
    fn SetWindowPos(
        hwnd: Hwnd,
        insert_after: Hwnd,
        x: i32,
        y: i32,
        width: i32,
        height: i32,
        flags: u32,
    ) -> i32;
    fn ShowWindow(hwnd: Hwnd, command: i32) -> i32;
}

#[link(name = "kernel32")]
unsafe extern "system" {
    fn GetCurrentProcessId() -> u32;
}

#[derive(Clone, Copy, Debug, Deserialize, Serialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
struct WindowState {
    x: i32,
    y: i32,
    width: i32,
    height: i32,
    maximized: bool,
}

/// Persists the main window bounds in `config/window.json`.
pub struct WindowStateService;

impl WindowStateService {
    /// Returns false while the window is not visible yet, so the caller can retry.
    pub fn restore(paths: &AppPaths) -> Result<bool, AppError> {
        let Some(state) = load(paths)? else {
            return Ok(true);
        };
        let Some(hwnd) = main_window() else {
            return Ok(false);
        };
        let state = sanitize(state);
        unsafe {
            SetWindowPos(
                hwnd,
                0,
                state.x,
                state.y,
                state.width,
                state.height,
                SWP_NOZORDER | SWP_NOACTIVATE,
            );
            if state.maximized {
                ShowWindow(hwnd, SW_MAXIMIZE);
            }
        }
        Ok(true)
    }

    pub fn save(paths: &AppPaths) -> Result<(), AppError> {
        let Some(hwnd) = main_window() else {
            return Ok(());
        };
        if unsafe { IsIconic(hwnd) } != 0 {
            return Ok(());
        }
        let mut rect = Rect {
            left: 0,
            top: 0,
            right: 0,
            bottom: 0,
        };
        if unsafe { GetWindowRect(hwnd, &mut rect) } == 0 {
            return Ok(());
        }
        let state = WindowState {
            x: rect.left,
            y: rect.top,
            width: rect.right - rect.left,
            height: rect.bottom - rect.top,
            maximized: unsafe { IsZoomed(hwnd) } != 0,
        };
        let directory = paths.config();
        std::fs::create_dir_all(&directory).map_err(|error| {
            AppError::new(ErrorCode::Internal)
                .with("path", directory.display().to_string())
                .with("reason", error.to_string())
        })?;
        let bytes = serde_json::to_vec_pretty(&sanitize(state)).map_err(|error| {
            AppError::new(ErrorCode::Internal).with("reason", error.to_string())
        })?;
        AtomicFile::write(&directory.join("window.json"), &bytes)
    }
}

fn load(paths: &AppPaths) -> Result<Option<WindowState>, AppError> {
    let path = paths.config().join("window.json");
    let bytes = match std::fs::read(&path) {
        Ok(bytes) => bytes,
        Err(error) if error.kind() == std::io::ErrorKind::NotFound => return Ok(None),
        Err(error) => {
            return Err(AppError::new(ErrorCode::FileNotFound)
                .with("path", path.display().to_string())
                .with("reason", error.to_string()));
        }
    };
    Ok(serde_json::from_slice(&bytes).ok().map(sanitize))
}

fn sanitize(state: WindowState) -> WindowState {
    WindowState {
        x: state.x.clamp(-10_000, 10_000),
        y: state.y.clamp(-10_000, 10_000),
        width: state.width.clamp(800, 12_000),
        height: state.height.clamp(600, 12_000),
        maximized: state.maximized,
    }
}

fn main_window() -> Option<Hwnd> {
    struct Search {
        process_id: u32,
        best: Hwnd,
        best_area: i64,
    }

    unsafe extern "system" fn visit(hwnd: Hwnd, lparam: isize) -> i32 {
        let search = unsafe { &mut *(lparam as *mut Search) };
        let mut process_id = 0_u32;
        unsafe { GetWindowThreadProcessId(hwnd, &mut process_id) };
        if process_id != search.process_id {
            return 1;
        }
        if unsafe { IsWindowVisible(hwnd) } == 0 || unsafe { GetWindow(hwnd, GW_OWNER) } != 0 {
            return 1;
        }
        let mut rect = Rect {
            left: 0,
            top: 0,
            right: 0,
            bottom: 0,
        };
        if unsafe { GetWindowRect(hwnd, &mut rect) } == 0 {
            return 1;
        }
        let area = i64::from(rect.right - rect.left) * i64::from(rect.bottom - rect.top);
        if area > search.best_area {
            search.best = hwnd;
            search.best_area = area;
        }
        1
    }

    let mut search = Search {
        process_id: unsafe { GetCurrentProcessId() },
        best: 0,
        best_area: 0,
    };
    unsafe { EnumWindows(visit, &mut search as *mut Search as isize) };
    (search.best != 0).then_some(search.best)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn sanitize_clamps_offscreen_and_tiny_bounds() {
        let state = sanitize(WindowState {
            x: -99_999,
            y: 99_999,
            width: 10,
            height: 10,
            maximized: false,
        });
        assert_eq!(state.x, -10_000);
        assert_eq!(state.y, 10_000);
        assert_eq!(state.width, 800);
        assert_eq!(state.height, 600);
    }
}
