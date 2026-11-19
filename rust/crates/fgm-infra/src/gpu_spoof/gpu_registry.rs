use std::ffi::c_void;

type Hkey = isize;

const HKEY_LOCAL_MACHINE: Hkey = 0x8000_0002_u32 as isize;
const KEY_READ: u32 = 0x0002_0019 | 0x0100;
const KEY_WRITE: u32 = 0x0000_0002 | 0x0000_0001 | 0x0100;
const REG_SZ: u32 = 1;
const MAX_QUERY_LENGTH: u32 = 16 * 1024;

#[link(name = "advapi32")]
unsafe extern "system" {
    fn RegOpenKeyExW(
        key: Hkey,
        sub_key: *const u16,
        options: u32,
        desired: u32,
        result: *mut Hkey,
    ) -> i32;
    fn RegQueryValueExW(
        key: Hkey,
        value_name: *const u16,
        reserved: *mut u32,
        kind: *mut u32,
        data: *mut u8,
        length: *mut u32,
    ) -> i32;
    fn RegSetValueExW(
        key: Hkey,
        value_name: *const u16,
        reserved: u32,
        kind: u32,
        data: *const u8,
        length: u32,
    ) -> i32;
    fn RegEnumKeyExW(
        key: Hkey,
        index: u32,
        name: *mut u16,
        name_length: *mut u32,
        reserved: *mut u32,
        class: *mut u16,
        class_length: *mut u32,
        last_write: *mut c_void,
    ) -> i32;
    fn RegCloseKey(key: Hkey) -> i32;
}

pub(crate) fn is_admin() -> bool {
    crate::platform::is_elevated()
}

pub(crate) fn read_string(sub_key: &str, value_name: &str) -> Option<String> {
    let key = open(sub_key, KEY_READ)?;
    let name = wide(value_name);
    let mut kind = 0_u32;
    let mut length = 0_u32;
    let status = unsafe {
        RegQueryValueExW(
            key,
            name.as_ptr(),
            std::ptr::null_mut(),
            &mut kind,
            std::ptr::null_mut(),
            &mut length,
        )
    };
    if status != 0 || length == 0 || length > MAX_QUERY_LENGTH {
        unsafe { RegCloseKey(key) };
        return None;
    }
    let mut buffer = vec![0_u8; length as usize];
    let status = unsafe {
        RegQueryValueExW(
            key,
            name.as_ptr(),
            std::ptr::null_mut(),
            &mut kind,
            buffer.as_mut_ptr(),
            &mut length,
        )
    };
    unsafe { RegCloseKey(key) };
    if status != 0 {
        return None;
    }
    Some(utf16z(&buffer))
}

pub(crate) fn write_string(sub_key: &str, value_name: &str, value: &str) -> Result<(), String> {
    let key = open(sub_key, KEY_WRITE)
        .ok_or_else(|| "could not open the registry key for writing".to_string())?;
    let name = wide(value_name);
    let encoded: Vec<u16> = value.encode_utf16().chain(std::iter::once(0)).collect();
    let bytes = encoded.len() * 2;
    let status = unsafe {
        RegSetValueExW(
            key,
            name.as_ptr(),
            0,
            REG_SZ,
            encoded.as_ptr().cast(),
            bytes as u32,
        )
    };
    unsafe { RegCloseKey(key) };
    if status != 0 {
        return Err(format!("registry write failed with code {status}"));
    }
    Ok(())
}

pub(crate) fn sub_keys(sub_key: &str) -> Vec<String> {
    let Some(key) = open(sub_key, KEY_READ) else {
        return Vec::new();
    };
    let mut names = Vec::new();
    let mut index = 0_u32;
    loop {
        let mut buffer = [0_u16; 512];
        let mut length = buffer.len() as u32;
        let status = unsafe {
            RegEnumKeyExW(
                key,
                index,
                buffer.as_mut_ptr(),
                &mut length,
                std::ptr::null_mut(),
                std::ptr::null_mut(),
                std::ptr::null_mut(),
                std::ptr::null_mut(),
            )
        };
        if status != 0 {
            break;
        }
        if let Ok(name) = String::from_utf16(&buffer[..length as usize]) {
            names.push(name);
        }
        index += 1;
    }
    unsafe { RegCloseKey(key) };
    names
}

pub(crate) fn display_name(value: &str) -> String {
    value
        .rsplit_once(';')
        .map_or_else(|| value.to_string(), |(_, name)| name.trim().to_string())
}

fn open(sub_key: &str, access: u32) -> Option<Hkey> {
    let path = wide(sub_key);
    let mut key: Hkey = 0;
    let status = unsafe { RegOpenKeyExW(HKEY_LOCAL_MACHINE, path.as_ptr(), 0, access, &mut key) };
    (status == 0 && key != 0).then_some(key)
}

fn wide(value: &str) -> Vec<u16> {
    value.encode_utf16().chain(std::iter::once(0)).collect()
}

fn utf16z(bytes: &[u8]) -> String {
    let (pairs, _) = bytes.as_chunks::<2>();
    let units: Vec<u16> = pairs
        .iter()
        .map(|pair| u16::from_le_bytes(*pair))
        .take_while(|unit| *unit != 0)
        .collect();
    String::from_utf16_lossy(&units)
}
