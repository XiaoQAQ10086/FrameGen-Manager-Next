use std::ffi::c_void;
use std::path::Path;

const TH32CS_SNAPPROCESS: u32 = 0x0000_0002;
const PROCESS_QUERY_LIMITED_INFORMATION: u32 = 0x1000;
const INVALID_HANDLE_VALUE: *mut c_void = -1isize as *mut c_void;
const MAX_PATH_UNITS: usize = 32_768;

#[repr(C)]
struct ProcessEntry32W {
    size: u32,
    usage: u32,
    process_id: u32,
    default_heap_id: usize,
    module_id: u32,
    thread_count: u32,
    parent_process_id: u32,
    priority_class_base: i32,
    flags: u32,
    exe_file: [u16; 260],
}

#[link(name = "kernel32")]
unsafe extern "system" {
    fn CreateToolhelp32Snapshot(flags: u32, process_id: u32) -> *mut c_void;
    fn Process32FirstW(snapshot: *mut c_void, entry: *mut ProcessEntry32W) -> i32;
    fn Process32NextW(snapshot: *mut c_void, entry: *mut ProcessEntry32W) -> i32;
    fn CloseHandle(handle: *mut c_void) -> i32;
    fn OpenProcess(access: u32, inherit: i32, process_id: u32) -> *mut c_void;
    fn QueryFullProcessImageNameW(
        process: *mut c_void,
        flags: u32,
        name: *mut u16,
        size: *mut u32,
    ) -> i32;
}

/// Checks whether a game executable is running.
pub struct GameProcessProbe;

impl GameProcessProbe {
    /// Returns true when a process with the executable's file name is running.
    /// A path match is preferred, but an inaccessible path falls back to the
    /// name so protected game processes are not treated as stopped.
    pub fn is_running(exe_path: &Path) -> bool {
        if exe_path.as_os_str().is_empty() {
            return false;
        }

        let Some(stem) = exe_path.file_stem() else {
            return false;
        };
        let Some(stem) = stem.to_str() else {
            return false;
        };
        if stem.is_empty() {
            return false;
        }

        let wanted = format!("{stem}.exe").to_lowercase();
        let Some(full_path) = std::path::absolute(exe_path)
            .ok()
            .map(|path| path.to_string_lossy().to_lowercase())
        else {
            return false;
        };

        unsafe {
            let snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
            if snapshot == INVALID_HANDLE_VALUE || snapshot.is_null() {
                return false;
            }
            let snapshot = Handle(snapshot);

            let mut entry = ProcessEntry32W {
                size: std::mem::size_of::<ProcessEntry32W>() as u32,
                usage: 0,
                process_id: 0,
                default_heap_id: 0,
                module_id: 0,
                thread_count: 0,
                parent_process_id: 0,
                priority_class_base: 0,
                flags: 0,
                exe_file: [0; 260],
            };

            let mut more = Process32FirstW(snapshot.0, &mut entry) != 0;
            while more {
                let name = utf16z(&entry.exe_file).to_lowercase();
                if name == wanted && process_matches(entry.process_id, &full_path) {
                    return true;
                }

                entry.size = std::mem::size_of::<ProcessEntry32W>() as u32;
                more = Process32NextW(snapshot.0, &mut entry) != 0;
            }
        }

        false
    }
}

unsafe fn process_matches(process_id: u32, full_path: &str) -> bool {
    let process = unsafe { OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, 0, process_id) };
    if process.is_null() {
        // The name matched; a protected process must not be assumed stopped.
        return true;
    }
    let process = Handle(process);

    let mut buffer = [0_u16; MAX_PATH_UNITS];
    let mut size = buffer.len() as u32;
    let ok = unsafe { QueryFullProcessImageNameW(process.0, 0, buffer.as_mut_ptr(), &mut size) };
    if ok == 0 {
        return true;
    }

    let path = utf16z(&buffer[..size as usize]).to_lowercase();
    path == full_path
}

fn utf16z(units: &[u16]) -> String {
    let end = units
        .iter()
        .position(|unit| *unit == 0)
        .unwrap_or(units.len());
    String::from_utf16_lossy(&units[..end])
}

struct Handle(*mut c_void);

impl Drop for Handle {
    fn drop(&mut self) {
        unsafe {
            let _ = CloseHandle(self.0);
        }
    }
}
