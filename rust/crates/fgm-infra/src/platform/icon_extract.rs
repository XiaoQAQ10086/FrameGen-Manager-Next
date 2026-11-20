//! Extracts the icon embedded in a game executable and saves it as PNG.
//!
//! Used as the last-resort cover for games that have no store artwork. The
//! icon comes from the shell image list, so it matches what Explorer shows.

use std::ffi::c_void;
use std::path::Path;

use fgm_domain::error::{AppError, ErrorCode};

type Handle = isize;

const CLSID_PNG: Guid = Guid {
    data1: 0x557C_F406,
    data2: 0x1A04,
    data3: 0x11D3,
    data4: [0x9A, 0x73, 0x00, 0x00, 0xF8, 0x1E, 0xF3, 0x2E],
};

#[repr(C)]
struct Guid {
    data1: u32,
    data2: u16,
    data3: u16,
    data4: [u8; 8],
}

#[repr(C)]
struct GdiplusStartupInput {
    version: u32,
    callback: *const c_void,
    callback_data: *mut c_void,
    suppress_background: i32,
    suppress_external_codecs: i32,
}

#[repr(C)]
struct ShFileInfoW {
    icon: Handle,
    icon_index: i32,
    attributes: u32,
    display_name: [u16; 260],
    type_name: [u16; 80],
}

const SHGFI_ICON: u32 = 0x0000_0100;
const SHGFI_USEFILEATTRIBUTES: u32 = 0x0000_0010;
const FILE_ATTRIBUTE_NORMAL: u32 = 0x0000_0080;

#[link(name = "shell32")]
unsafe extern "system" {
    fn SHGetFileInfoW(
        path: *const u16,
        attributes: u32,
        info: *mut ShFileInfoW,
        size: u32,
        flags: u32,
    ) -> usize;
    fn ExtractIconExW(
        file: *const u16,
        index: i32,
        large: *mut Handle,
        small: *mut Handle,
        count: u32,
    ) -> u32;
    fn DestroyIcon(icon: Handle) -> i32;
}

#[link(name = "gdiplus")]
unsafe extern "system" {
    fn GdiplusStartup(
        token: *mut usize,
        input: *const GdiplusStartupInput,
        output: *mut c_void,
    ) -> i32;
    fn GdiplusShutdown(token: usize);
    fn GdipCreateBitmapFromHICON(icon: Handle, bitmap: *mut *mut c_void) -> i32;
    fn GdipSaveImageToFile(
        image: *mut c_void,
        filename: *const u16,
        clsid: *const Guid,
        params: *const c_void,
    ) -> i32;
    fn GdipDisposeImage(image: *mut c_void) -> i32;
}

/// Extracts the first large icon and writes it to `destination` as PNG.
pub fn extract_exe_icon(exe: &Path, destination: &Path) -> Result<(), AppError> {
    let file = wide(&exe.to_string_lossy());
    let mut icon: Handle = 0;
    let count = unsafe { ExtractIconExW(file.as_ptr(), 0, &mut icon, std::ptr::null_mut(), 1) };
    if count == 0 || icon == 0 {
        // Icon-less executables still have a shell icon (generic exe image).
        let mut info = ShFileInfoW {
            icon: 0,
            icon_index: 0,
            attributes: 0,
            display_name: [0; 260],
            type_name: [0; 80],
        };
        let ok = unsafe {
            SHGetFileInfoW(
                file.as_ptr(),
                FILE_ATTRIBUTE_NORMAL,
                &mut info,
                std::mem::size_of::<ShFileInfoW>() as u32,
                SHGFI_ICON | SHGFI_USEFILEATTRIBUTES,
            )
        };
        if ok == 0 || info.icon == 0 {
            return Err(
                AppError::new(ErrorCode::FileNotFound).with("reason", "executable has no icon")
            );
        }
        icon = info.icon;
    }

    let mut token = 0_usize;
    let input = GdiplusStartupInput {
        version: 1,
        callback: std::ptr::null(),
        callback_data: std::ptr::null_mut(),
        suppress_background: 1,
        suppress_external_codecs: 0,
    };
    let status = unsafe { GdiplusStartup(&mut token, &input, std::ptr::null_mut()) };
    if status != 0 {
        unsafe { DestroyIcon(icon) };
        return Err(AppError::new(ErrorCode::Internal).with("reason", "GDI+ startup failed"));
    }

    let mut bitmap: *mut c_void = std::ptr::null_mut();
    let status = unsafe { GdipCreateBitmapFromHICON(icon, &mut bitmap) };
    if status != 0 || bitmap.is_null() {
        unsafe {
            GdiplusShutdown(token);
            DestroyIcon(icon);
        }
        return Err(AppError::new(ErrorCode::Internal)
            .with("reason", "could not convert the executable icon"));
    }

    let output = wide(&destination.to_string_lossy());
    let status =
        unsafe { GdipSaveImageToFile(bitmap, output.as_ptr(), &CLSID_PNG, std::ptr::null()) };
    unsafe {
        GdipDisposeImage(bitmap);
        GdiplusShutdown(token);
        DestroyIcon(icon);
    }
    if status != 0 {
        return Err(
            AppError::new(ErrorCode::Internal).with("reason", "could not save the icon as PNG")
        );
    }
    Ok(())
}

fn wide(value: &str) -> Vec<u16> {
    value.encode_utf16().chain(std::iter::once(0)).collect()
}
