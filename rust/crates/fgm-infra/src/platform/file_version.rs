use std::ffi::c_void;
use std::path::Path;

#[link(name = "version")]
unsafe extern "system" {
    fn GetFileVersionInfoSizeW(path: *const u16, handle: *mut u32) -> u32;
    fn GetFileVersionInfoW(path: *const u16, handle: u32, length: u32, data: *mut c_void) -> i32;
    fn VerQueryValueW(
        block: *const c_void,
        sub_block: *const u16,
        buffer: *mut *mut c_void,
        length: *mut u32,
    ) -> i32;
}

/// Reads the `ProductName` string from a PE version resource.
pub(crate) fn product_name(path: &Path) -> Option<String> {
    let path = wide(&path.display().to_string());
    let mut handle = 0_u32;
    let size = unsafe { GetFileVersionInfoSizeW(path.as_ptr(), &mut handle) };
    if size == 0 {
        return None;
    }

    let mut block = vec![0_u8; size as usize];
    let ok = unsafe { GetFileVersionInfoW(path.as_ptr(), handle, size, block.as_mut_ptr().cast()) };
    if ok == 0 {
        return None;
    }

    for language in translations(&block)
        .into_iter()
        .chain(["040904B0".to_string()])
    {
        let sub = wide(&format!("\\StringFileInfo\\{language}\\ProductName"));
        let mut pointer: *mut c_void = std::ptr::null_mut();
        let mut length = 0_u32;
        let ok = unsafe {
            VerQueryValueW(
                block.as_ptr().cast(),
                sub.as_ptr(),
                &mut pointer,
                &mut length,
            )
        };
        if ok != 0 && !pointer.is_null() && length > 0 {
            let units =
                unsafe { std::slice::from_raw_parts(pointer.cast::<u16>(), length as usize / 2) };
            let text = utf16z(units);
            if !text.is_empty() {
                return Some(text);
            }
        }
    }
    None
}

fn translations(block: &[u8]) -> Vec<String> {
    let sub = wide("\\VarFileInfo\\Translation");
    let mut pointer: *mut c_void = std::ptr::null_mut();
    let mut length = 0_u32;
    let ok = unsafe {
        VerQueryValueW(
            block.as_ptr().cast(),
            sub.as_ptr(),
            &mut pointer,
            &mut length,
        )
    };
    if ok == 0 || pointer.is_null() || length < 4 {
        return Vec::new();
    }

    let units = unsafe { std::slice::from_raw_parts(pointer.cast::<u16>(), length as usize / 2) };
    units
        .as_chunks::<2>()
        .0
        .iter()
        .map(|pair| format!("{:04X}{:04X}", pair[0], pair[1]))
        .collect()
}

fn wide(value: &str) -> Vec<u16> {
    value.encode_utf16().chain(std::iter::once(0)).collect()
}

fn utf16z(units: &[u16]) -> String {
    let end = units
        .iter()
        .position(|unit| *unit == 0)
        .unwrap_or(units.len());
    String::from_utf16_lossy(&units[..end]).trim().to_string()
}
