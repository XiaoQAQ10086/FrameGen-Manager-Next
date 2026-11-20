use fgm_domain::error::{AppError, ErrorCode};
use serde::{Deserialize, Serialize};

use crate::fs::AtomicFile;
use crate::paths::AppPaths;

type Hwnd = isize;

const GW_OWNER: u32 = 4;
const SW_MAXIMIZE: i32 = 3;
const SWP_NOMOVE: u32 = 0x0002;
const SWP_NOZORDER: u32 = 0x0004;
const SWP_NOACTIVATE: u32 = 0x0010;
const SPI_GETWORKAREA: u32 = 0x0030;

/// Default window size used when the desktop cannot report a work area.
const FALLBACK_SIZE: (i32, i32) = (1280, 850);
/// Minimum size for a first launch, so the three-column layout still fits.
const MIN_SIZE: (i32, i32) = (1180, 760);
/// Share of the work area a first-launch window fills.
const WORK_AREA_RATIO: f64 = 0.85;
/// Gap kept between a window and the screen edge.
const SCREEN_MARGIN: i32 = 48;

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
    fn SystemParametersInfoW(
        action: u32,
        param: u32,
        out: *mut core::ffi::c_void,
        win_ini: u32,
    ) -> i32;
}

#[link(name = "kernel32")]
unsafe extern "system" {
    fn GetCurrentProcessId() -> u32;
}

#[derive(Clone, Copy, Debug, Deserialize, Serialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
struct WindowState {
    width: i32,
    height: i32,
    maximized: bool,
}

/// Persists the main window bounds in `config/window.json`.
pub struct WindowStateService;

impl WindowStateService {
    /// Returns false while the window is not visible yet, so the caller can retry.
    ///
    /// With no saved state the first launch sizes itself from the desktop work
    /// area instead of a hardcoded default, and saved bounds are pulled back
    /// inside the current screen so a size remembered on a larger monitor does
    /// not open off-screen.
    pub fn restore(paths: &AppPaths) -> Result<bool, AppError> {
        let saved = load(paths)?;
        let Some(hwnd) = main_window() else {
            return Ok(false);
        };
        let work = work_area();
        // Position is left to Windows so a saved size cannot open off-screen.
        let state = match saved {
            Some(state) => fit_screen(sanitize(state), work),
            None => first_launch(work),
        };
        unsafe {
            SetWindowPos(
                hwnd,
                0,
                0,
                0,
                state.width,
                state.height,
                SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE,
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
        width: state.width.clamp(800, 12_000),
        height: state.height.clamp(600, 12_000),
        maximized: state.maximized,
    }
}

/// Primary monitor work area (screen minus taskbar and docked bars).
fn work_area() -> Option<(i32, i32)> {
    let mut rect = Rect {
        left: 0,
        top: 0,
        right: 0,
        bottom: 0,
    };
    let ok = unsafe {
        SystemParametersInfoW(SPI_GETWORKAREA, 0, std::ptr::addr_of_mut!(rect).cast(), 0)
    };
    if ok == 0 {
        return None;
    }
    let width = rect.right - rect.left;
    let height = rect.bottom - rect.top;
    (width > 0 && height > 0).then_some((width, height))
}

fn first_launch(work: Option<(i32, i32)>) -> WindowState {
    let (width, height) = match work {
        Some(area) => (
            fit_axis(
                (f64::from(area.0) * WORK_AREA_RATIO) as i32,
                area.0,
                MIN_SIZE.0,
            ),
            fit_axis(
                (f64::from(area.1) * WORK_AREA_RATIO) as i32,
                area.1,
                MIN_SIZE.1,
            ),
        ),
        None => FALLBACK_SIZE,
    };
    WindowState {
        width,
        height,
        maximized: false,
    }
}

/// Keeps a remembered size inside the current screen.
fn fit_screen(state: WindowState, work: Option<(i32, i32)>) -> WindowState {
    let Some(area) = work else {
        return state;
    };
    WindowState {
        width: fit_axis(state.width, area.0, 800),
        height: fit_axis(state.height, area.1, 600),
        maximized: state.maximized,
    }
}

/// Clamps a size to `[minimum, available - margin]`, keeping the floor below the
/// ceiling on very small desktops.
fn fit_axis(preferred: i32, available: i32, minimum: i32) -> i32 {
    let ceiling = (available - SCREEN_MARGIN).max(480);
    let floor = minimum.min(ceiling);
    preferred.clamp(floor, ceiling)
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
            width: 10,
            height: 99_999,
            maximized: false,
        });
        assert_eq!(state.width, 800);
        assert_eq!(state.height, 12_000);
    }

    #[test]
    fn first_launch_scales_with_the_work_area() {
        let state = first_launch(Some((1920, 1040)));
        assert_eq!(state.width, 1632);
        assert_eq!(state.height, 884);
        assert!(!state.maximized);
    }

    #[test]
    fn first_launch_never_exceeds_a_small_desktop() {
        let state = first_launch(Some((1366, 728)));
        assert_eq!(state.width, MIN_SIZE.0);
        assert_eq!(state.height, 680);
    }

    #[test]
    fn first_launch_falls_back_without_a_work_area() {
        assert_eq!(first_launch(None).width, FALLBACK_SIZE.0);
        assert_eq!(first_launch(None).height, FALLBACK_SIZE.1);
    }

    #[test]
    fn remembered_size_is_pulled_inside_a_smaller_screen() {
        let state = fit_screen(
            WindowState {
                width: 3800,
                height: 2100,
                maximized: false,
            },
            Some((1920, 1040)),
        );
        assert_eq!(state.width, 1872);
        assert_eq!(state.height, 992);
    }

    #[test]
    fn remembered_size_is_kept_when_it_already_fits() {
        let state = fit_screen(
            WindowState {
                width: 1280,
                height: 850,
                maximized: true,
            },
            Some((1920, 1040)),
        );
        assert_eq!(state.width, 1280);
        assert_eq!(state.height, 850);
        assert!(state.maximized);
    }
}
