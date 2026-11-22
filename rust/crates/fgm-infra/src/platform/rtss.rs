use std::ffi::c_void;

const TH32CS_SNAPPROCESS: u32 = 0x0000_0002;
const INVALID_HANDLE_VALUE: isize = -1;

#[repr(C)]
struct ProcessEntry32W {
    size: u32,
    usage: u32,
    process_id: u32,
    default_heap_id: usize,
    module_id: u32,
    threads: u32,
    parent_process_id: u32,
    priority_class: i32,
    flags: u32,
    executable: [u16; 260],
}

#[link(name = "kernel32")]
unsafe extern "system" {
    fn CreateToolhelp32Snapshot(flags: u32, process_id: u32) -> *mut c_void;
    fn Process32FirstW(snapshot: *mut c_void, entry: *mut ProcessEntry32W) -> i32;
    fn Process32NextW(snapshot: *mut c_void, entry: *mut ProcessEntry32W) -> i32;
    fn CloseHandle(handle: *mut c_void) -> i32;
}

/// Reports whether RivaTuner Statistics Server is running.
///
/// RTSS injects `RTSSHooks64.dll` into games; its version-info hooks can
/// recurse through the `version.dll` proxy used by Smooth Motion.
pub fn rtss_running() -> bool {
    let snapshot = unsafe { CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0) };
    if snapshot as isize == INVALID_HANDLE_VALUE || snapshot.is_null() {
        return false;
    }
    let mut entry = ProcessEntry32W {
        size: std::mem::size_of::<ProcessEntry32W>() as u32,
        usage: 0,
        process_id: 0,
        default_heap_id: 0,
        module_id: 0,
        threads: 0,
        parent_process_id: 0,
        priority_class: 0,
        flags: 0,
        executable: [0; 260],
    };
    let mut found = false;
    let mut has_entry = unsafe { Process32FirstW(snapshot, &mut entry) } != 0;
    while has_entry {
        let end = entry
            .executable
            .iter()
            .position(|unit| *unit == 0)
            .unwrap_or(entry.executable.len());
        let name = String::from_utf16_lossy(&entry.executable[..end]);
        if name.eq_ignore_ascii_case("RTSS.exe")
            || name.eq_ignore_ascii_case("RTSSHooksLoader64.exe")
            || name.eq_ignore_ascii_case("RTSSHooksLoader.exe")
        {
            found = true;
            break;
        }
        has_entry = unsafe { Process32NextW(snapshot, &mut entry) } != 0;
    }
    unsafe {
        CloseHandle(snapshot);
    }
    found
}
