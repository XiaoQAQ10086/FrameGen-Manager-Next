use std::path::PathBuf;

const DRIVE_REMOVABLE: u32 = 2;
const DRIVE_FIXED: u32 = 3;

#[link(name = "kernel32")]
unsafe extern "system" {
    fn GetLogicalDrives() -> u32;
    fn GetDriveTypeW(root: *const u16) -> u32;
}

pub struct DriveRoots;

impl DriveRoots {
    pub fn roots() -> Vec<PathBuf> {
        let mask = unsafe { GetLogicalDrives() };
        let mut roots = Vec::new();

        for index in 0..26_u32 {
            if mask & (1 << index) == 0 {
                continue;
            }
            let letter = (b'A' + index as u8) as char;
            let root = format!("{letter}:\\");
            let wide: Vec<u16> = root.encode_utf16().chain(std::iter::once(0)).collect();
            let drive_type = unsafe { GetDriveTypeW(wide.as_ptr()) };
            if matches!(drive_type, DRIVE_REMOVABLE | DRIVE_FIXED) {
                roots.push(PathBuf::from(root));
            }
        }

        roots
    }
}
