use std::ffi::c_void;
use std::path::Path;

pub(crate) const ID_INITIALIZE: u32 = 0x0150_E828;
pub(crate) const ID_UNLOAD: u32 = 0xD22B_DD7E;
pub(crate) const ID_CREATE_SESSION: u32 = 0x0694_D52E;
pub(crate) const ID_LOAD_SETTINGS: u32 = 0x375D_BD6B;
pub(crate) const ID_GET_BASE_PROFILE: u32 = 0xDA84_66A0;
pub(crate) const ID_FIND_PROFILE: u32 = 0x7E4A_9A0B;
pub(crate) const ID_FIND_APPLICATION: u32 = 0xEEE5_66B2;
pub(crate) const ID_GET_SETTING: u32 = 0xEA99_498D;
pub(crate) const ID_SET_SETTING: u32 = 0x8A2C_F5F5;
pub(crate) const ID_SAVE_SETTINGS: u32 = 0xFCBC_7E14;
pub(crate) const ID_DESTROY_SESSION: u32 = 0xDAD9_CFF8;
pub(crate) const ID_ENUM_PROFILES: u32 = 0xBC37_1EE0;
pub(crate) const ID_ENUM_APPLICATIONS: u32 = 0x7FA2_173A;

pub(crate) const SMOOTH_MOTION_SETTING_ID: u32 = 0xB0D3_84C0;

pub(crate) const SETTING_BUFFER_SIZE: usize = 12_320;
pub(crate) const APPLICATION_BUFFER_SIZE: usize = 20_492;
pub(crate) const SETTING_VALUE_OFFSET: usize = 8_220;
pub(crate) const APPLICATION_FILE_IN_FOLDER_OFFSET: usize = 12_296;
pub(crate) const APPLICATION_SLOTS: usize = 32;
pub(crate) const APPLICATION_VERSION: u32 = 4;

pub(crate) type QueryInterface = unsafe extern "C" fn(u32) -> *mut c_void;
pub(crate) type Unload = unsafe extern "C" fn() -> i32;
pub(crate) type GetBaseProfile = unsafe extern "C" fn(*mut c_void, *mut *mut c_void) -> i32;
pub(crate) type FindProfile =
    unsafe extern "C" fn(*mut c_void, *const u16, *mut *mut c_void) -> i32;
pub(crate) type FindApplication =
    unsafe extern "C" fn(*mut c_void, *const u16, *mut *mut c_void, *mut u8) -> i32;
pub(crate) type GetSetting =
    unsafe extern "C" fn(*mut c_void, *mut c_void, u32, *mut u8, *mut u32) -> i32;
pub(crate) type SetSetting =
    unsafe extern "C" fn(*mut c_void, *mut c_void, *mut u8, u32, u32) -> i32;
pub(crate) type SaveSettings = unsafe extern "C" fn(*mut c_void) -> i32;
pub(crate) type DestroySession = unsafe extern "C" fn(*mut c_void) -> i32;
pub(crate) type EnumProfiles = unsafe extern "C" fn(*mut c_void, u32, *mut *mut c_void) -> i32;
pub(crate) type EnumApplications =
    unsafe extern "C" fn(*mut c_void, *mut c_void, u32, *mut u32, *mut u8) -> i32;

pub(crate) struct NvApiFunctions {
    pub unload: Unload,
    pub get_base_profile: GetBaseProfile,
    pub find_profile: FindProfile,
    pub find_application: FindApplication,
    pub get_setting: GetSetting,
    pub set_setting: SetSetting,
    pub save_settings: SaveSettings,
    pub destroy_session: Option<DestroySession>,
    pub enum_profiles: EnumProfiles,
    pub enum_applications: EnumApplications,
}

#[link(name = "kernel32")]
unsafe extern "system" {
    fn LoadLibraryW(name: *const u16) -> *mut c_void;
    fn GetProcAddress(module: *mut c_void, name: *const u8) -> *mut c_void;
    fn FreeLibrary(module: *mut c_void) -> i32;
}

pub(crate) unsafe fn load_nvapi() -> Option<(*mut c_void, QueryInterface)> {
    let name = wide("nvapi64.dll");
    let library = unsafe { LoadLibraryW(name.as_ptr()) };
    if library.is_null() {
        return None;
    }

    let symbol = c"nvapi_QueryInterface";
    let pointer = unsafe { GetProcAddress(library, symbol.as_ptr().cast()) };
    if pointer.is_null() {
        unsafe { FreeLibrary(library) };
        return None;
    }

    let query: QueryInterface = unsafe { std::mem::transmute(pointer) };
    Some((library, query))
}

pub(crate) unsafe fn free_library(library: *mut c_void) {
    unsafe {
        let _ = FreeLibrary(library);
    }
}

pub(crate) unsafe fn query_fn<T: Copy>(query: QueryInterface, id: u32) -> Option<T> {
    let pointer = unsafe { query(id) };
    if pointer.is_null() {
        return None;
    }
    Some(unsafe { std::mem::transmute_copy(&pointer) })
}

pub(crate) fn setting_buffer(setting_id: u32) -> Vec<u8> {
    let mut buffer = vec![0_u8; SETTING_BUFFER_SIZE];
    write_version(&mut buffer, SETTING_BUFFER_SIZE as u32, 1);
    write_wide(&mut buffer, 4, 2_048, "PLACEHOLDER");
    write_u32(&mut buffer, 4_100, setting_id);
    buffer
}

pub(crate) fn set_setting_value(buffer: &mut [u8], value: u32) {
    write_u32(buffer, SETTING_VALUE_OFFSET, value);
}

pub(crate) fn get_setting_value(buffer: &[u8]) -> u32 {
    read_u32(buffer, SETTING_VALUE_OFFSET)
}

pub(crate) fn application_buffer(executable: &Path) -> Vec<u8> {
    let mut buffer = vec![0_u8; APPLICATION_BUFFER_SIZE];
    write_version(
        &mut buffer,
        APPLICATION_BUFFER_SIZE as u32,
        APPLICATION_VERSION,
    );

    let name = executable
        .file_name()
        .map_or_else(String::new, |value| value.to_string_lossy().into_owned());
    write_wide(&mut buffer, 8, 2_048, &name);

    if let Some(parent) = executable.parent() {
        write_wide(
            &mut buffer,
            APPLICATION_FILE_IN_FOLDER_OFFSET,
            2_048,
            &parent.to_string_lossy(),
        );
    }
    buffer
}

pub(crate) fn initialize_application_slots(buffer: &mut [u8]) {
    let version = (APPLICATION_BUFFER_SIZE as u32) | (APPLICATION_VERSION << 16);
    for index in 0..APPLICATION_SLOTS {
        write_u32(buffer, index * APPLICATION_BUFFER_SIZE, version);
    }
}

pub(crate) fn read_wide(buffer: &[u8], offset: usize, max_chars: usize) -> String {
    let mut units = Vec::new();
    for index in 0..max_chars {
        let start = offset + index * 2;
        let Some(bytes) = buffer.get(start..start + 2) else {
            break;
        };
        let unit = u16::from_le_bytes([bytes[0], bytes[1]]);
        if unit == 0 {
            break;
        }
        units.push(unit);
    }
    String::from_utf16_lossy(&units)
}

pub(crate) fn wide(value: &str) -> Vec<u16> {
    value.encode_utf16().chain(std::iter::once(0)).collect()
}

fn write_version(buffer: &mut [u8], size: u32, version: u32) {
    write_u32(buffer, 0, size | (version << 16));
}

fn write_u32(buffer: &mut [u8], offset: usize, value: u32) {
    buffer[offset..offset + 4].copy_from_slice(&value.to_le_bytes());
}

fn read_u32(buffer: &[u8], offset: usize) -> u32 {
    u32::from_le_bytes([
        buffer[offset],
        buffer[offset + 1],
        buffer[offset + 2],
        buffer[offset + 3],
    ])
}

fn write_wide(buffer: &mut [u8], offset: usize, max_chars: usize, value: &str) {
    for (index, unit) in value.encode_utf16().take(max_chars - 1).enumerate() {
        let start = offset + index * 2;
        buffer[start..start + 2].copy_from_slice(&unit.to_le_bytes());
    }
}
