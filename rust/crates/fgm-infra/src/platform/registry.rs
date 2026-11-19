use std::ffi::c_void;
use std::ptr;

pub(crate) const HKEY_CURRENT_USER: usize = 0x8000_0001;
pub(crate) const HKEY_LOCAL_MACHINE: usize = 0x8000_0002;

const RRF_RT_REG_SZ: u32 = 0x0000_0002;

#[link(name = "advapi32")]
unsafe extern "system" {
    fn RegGetValueW(
        hkey: *mut c_void,
        subkey: *const u16,
        value: *const u16,
        flags: u32,
        kind: *mut u32,
        data: *mut c_void,
        size: *mut u32,
    ) -> i32;
}

pub(crate) fn read_string(root: usize, subkey: &str, value: &str) -> Option<String> {
    let subkey = wide(subkey);
    let value = wide(value);
    let root = root as *mut c_void;
    let mut size = 0_u32;

    let result = unsafe {
        RegGetValueW(
            root,
            subkey.as_ptr(),
            value.as_ptr(),
            RRF_RT_REG_SZ,
            ptr::null_mut(),
            ptr::null_mut(),
            &mut size,
        )
    };
    if result != 0 || size == 0 {
        return None;
    }

    let mut buffer = vec![0_u16; (size as usize).div_ceil(2)];
    let result = unsafe {
        RegGetValueW(
            root,
            subkey.as_ptr(),
            value.as_ptr(),
            RRF_RT_REG_SZ,
            ptr::null_mut(),
            buffer.as_mut_ptr().cast(),
            &mut size,
        )
    };
    if result != 0 {
        return None;
    }

    let end = buffer
        .iter()
        .position(|unit| *unit == 0)
        .unwrap_or(buffer.len());
    let text = String::from_utf16_lossy(&buffer[..end]);
    (!text.is_empty()).then_some(text)
}

fn wide(value: &str) -> Vec<u16> {
    value.encode_utf16().chain(std::iter::once(0)).collect()
}
