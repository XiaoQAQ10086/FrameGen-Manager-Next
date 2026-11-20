use std::ffi::c_void;
use std::io::Write;
use std::path::{Path, PathBuf};
use std::sync::atomic::{AtomicBool, Ordering};

use fgm_domain::error::{AppError, ErrorCode};

const WINHTTP_ACCESS_TYPE_DEFAULT_PROXY: u32 = 0;
const WINHTTP_ACCESS_TYPE_NO_PROXY: u32 = 1;
const WINHTTP_ACCESS_TYPE_NAMED_PROXY: u32 = 3;
const WINHTTP_FLAG_SECURE: u32 = 0x0080_0000;
const WINHTTP_QUERY_STATUS_CODE: u32 = 19;
const WINHTTP_QUERY_LOCATION: u32 = 33;
const WINHTTP_QUERY_FLAG_NUMBER: u32 = 0x2000_0000;
const MAX_REDIRECTS: usize = 5;

#[link(name = "winhttp")]
unsafe extern "system" {
    fn WinHttpOpen(
        agent: *const u16,
        access: u32,
        proxy: *const u16,
        bypass: *const u16,
        flags: u32,
    ) -> *mut c_void;
    fn WinHttpConnect(
        session: *mut c_void,
        host: *const u16,
        port: u16,
        reserved: u32,
    ) -> *mut c_void;
    fn WinHttpOpenRequest(
        connect: *mut c_void,
        verb: *const u16,
        path: *const u16,
        version: *const u16,
        referrer: *const u16,
        accept: *const *const u16,
        flags: u32,
    ) -> *mut c_void;
    fn WinHttpSendRequest(
        request: *mut c_void,
        headers: *const u16,
        headers_length: u32,
        optional: *mut c_void,
        optional_length: u32,
        total_length: u32,
        context: usize,
    ) -> i32;
    fn WinHttpReceiveResponse(request: *mut c_void, reserved: *mut c_void) -> i32;
    fn WinHttpQueryHeaders(
        request: *mut c_void,
        info: u32,
        name: *const u16,
        buffer: *mut c_void,
        length: *mut u32,
        index: *mut u32,
    ) -> i32;
    fn WinHttpReadData(
        request: *mut c_void,
        buffer: *mut c_void,
        to_read: u32,
        read: *mut u32,
    ) -> i32;
    fn WinHttpCloseHandle(handle: *mut c_void) -> i32;
    fn WinHttpSetTimeouts(
        session: *mut c_void,
        resolve: i32,
        connect: i32,
        send: i32,
        receive: i32,
    ) -> i32;
}

#[link(name = "kernel32")]
unsafe extern "system" {
    fn GetLastError() -> u32;
}

/// Downloads `url` to `destination`, following redirects and reporting bytes.
pub(crate) fn download_to(
    url: &str,
    destination: &Path,
    cancelled: &AtomicBool,
    mut progress: impl FnMut(u64),
) -> Result<u64, AppError> {
    if cancelled.load(Ordering::Relaxed) {
        return Err(AppError::new(ErrorCode::OperationCancelled));
    }
    let mut last_error = None;
    let mut attempts: Vec<(u32, Option<String>)> = Vec::new();
    if let Some(proxy) = crate::platform::system_proxy() {
        attempts.push((WINHTTP_ACCESS_TYPE_NAMED_PROXY, Some(proxy)));
    }
    attempts.push((WINHTTP_ACCESS_TYPE_DEFAULT_PROXY, None));
    attempts.push((WINHTTP_ACCESS_TYPE_NO_PROXY, None));
    for (access, proxy) in attempts {
        match download_with_access(
            url,
            destination,
            cancelled,
            access,
            proxy.as_deref(),
            &mut progress,
        ) {
            Ok(bytes) => return Ok(bytes),
            Err(error) => {
                // Cancelling is the user's decision, not a transport failure;
                // retrying the other proxy modes would keep the dialog waiting.
                if error.code == ErrorCode::OperationCancelled {
                    return Err(error);
                }
                last_error = Some(error);
            }
        }
    }
    Err(last_error.unwrap_or_else(|| network("download failed")))
}

fn download_with_access(
    url: &str,
    destination: &Path,
    cancelled: &AtomicBool,
    access: u32,
    proxy: Option<&str>,
    progress: &mut impl FnMut(u64),
) -> Result<u64, AppError> {
    let agent = wide("FrameGen-Manager-Next");
    let proxy = proxy.map(wide);
    let session = Handle::new(unsafe {
        WinHttpOpen(
            agent.as_ptr(),
            access,
            proxy
                .as_ref()
                .map_or(std::ptr::null(), |value| value.as_ptr()),
            std::ptr::null(),
            0,
        )
    })?;
    unsafe {
        // Keep a dead endpoint from blocking cover/update work for a minute.
        let _ = WinHttpSetTimeouts(session.0, 2_000, 3_000, 5_000, 10_000);
    }

    let mut current = url.to_string();
    for _ in 0..=MAX_REDIRECTS {
        let parsed = parse_url(&current)?;
        let connect = Handle::new(unsafe {
            WinHttpConnect(session.0, wide(&parsed.host).as_ptr(), parsed.port, 0)
        })?;
        let path = wide(&parsed.path);
        let request = Handle::new(unsafe {
            WinHttpOpenRequest(
                connect.0,
                wide("GET").as_ptr(),
                path.as_ptr(),
                std::ptr::null(),
                std::ptr::null(),
                std::ptr::null(),
                if parsed.secure {
                    WINHTTP_FLAG_SECURE
                } else {
                    0
                },
            )
        })?;

        if unsafe {
            WinHttpSendRequest(
                request.0,
                std::ptr::null(),
                0,
                std::ptr::null_mut(),
                0,
                0,
                0,
            )
        } == 0
            || unsafe { WinHttpReceiveResponse(request.0, std::ptr::null_mut()) } == 0
        {
            return Err(last_network_error());
        }

        let status = query_status(request.0)?;
        if matches!(status, 301 | 302 | 303 | 307 | 308) {
            current = query_location(request.0)?
                .ok_or_else(|| network("redirect without a Location header"))?;
            continue;
        }
        if status != 200 {
            // A 404 means "this URL exists but has nothing at it", which update
            // sources answer before anything is published. Keep it apart from
            // transport failures so callers can report "no update" instead of
            // "network error".
            let code = if status == 404 {
                ErrorCode::FileNotFound
            } else {
                ErrorCode::NetworkError
            };
            return Err(AppError::new(code)
                .with("status", status.to_string())
                .with("reason", format!("HTTP status {status}")));
        }

        return write_body(request.0, destination, cancelled, progress);
    }

    Err(network("too many redirects"))
}

fn write_body(
    request: *mut c_void,
    destination: &Path,
    cancelled: &AtomicBool,
    progress: &mut impl FnMut(u64),
) -> Result<u64, AppError> {
    let parent = destination.parent().ok_or_else(|| {
        AppError::new(ErrorCode::InvalidPath).with("path", destination.display().to_string())
    })?;
    std::fs::create_dir_all(parent).map_err(|error| io_error(parent, error))?;

    let temp = temp_path(destination);
    let mut file = std::fs::File::create(&temp).map_err(|error| io_error(&temp, error))?;
    let mut buffer = vec![0_u8; 64 * 1024];
    let mut total = 0_u64;

    loop {
        if cancelled.load(Ordering::Relaxed) {
            drop(file);
            let _ = std::fs::remove_file(&temp);
            return Err(AppError::new(ErrorCode::OperationCancelled));
        }
        let mut read = 0_u32;
        if unsafe {
            WinHttpReadData(
                request,
                buffer.as_mut_ptr().cast(),
                buffer.len() as u32,
                &mut read,
            )
        } == 0
        {
            drop(file);
            let _ = std::fs::remove_file(&temp);
            return Err(last_network_error());
        }
        if read == 0 {
            break;
        }
        let chunk = &buffer[..read as usize];
        file.write_all(chunk)
            .map_err(|error| io_error(&temp, error))?;
        total += read as u64;
        progress(total);
    }

    file.sync_all().map_err(|error| io_error(&temp, error))?;
    drop(file);
    std::fs::rename(&temp, destination).map_err(|error| io_error(destination, error))?;
    Ok(total)
}

struct ParsedUrl {
    host: String,
    port: u16,
    path: String,
    secure: bool,
}

fn parse_url(url: &str) -> Result<ParsedUrl, AppError> {
    let (scheme, rest) = url
        .split_once("://")
        .ok_or_else(|| network("URL has no scheme"))?;
    let secure = match scheme.to_lowercase().as_str() {
        "https" => true,
        "http" => false,
        _ => return Err(network("unsupported URL scheme")),
    };
    let (authority, path) = match rest.split_once('/') {
        Some((authority, path)) => (authority, format!("/{path}")),
        None => (rest, "/".to_string()),
    };
    if authority.contains('@') {
        return Err(network("URL userinfo is not supported"));
    }
    let (host, port) = match authority.rsplit_once(':') {
        Some((host, port)) if port.bytes().all(|byte| byte.is_ascii_digit()) => (
            host.to_string(),
            port.parse::<u16>().map_err(|_| network("invalid port"))?,
        ),
        _ => (authority.to_string(), if secure { 443 } else { 80 }),
    };
    if host.is_empty() {
        return Err(network("URL host is empty"));
    }
    Ok(ParsedUrl {
        host,
        port,
        path,
        secure,
    })
}

fn query_status(request: *mut c_void) -> Result<u32, AppError> {
    let mut status = 0_u32;
    let mut length = std::mem::size_of::<u32>() as u32;
    let ok = unsafe {
        WinHttpQueryHeaders(
            request,
            WINHTTP_QUERY_STATUS_CODE | WINHTTP_QUERY_FLAG_NUMBER,
            std::ptr::null(),
            (&mut status as *mut u32).cast(),
            &mut length,
            std::ptr::null_mut(),
        )
    };
    if ok == 0 {
        return Err(last_network_error());
    }
    Ok(status)
}

fn query_location(request: *mut c_void) -> Result<Option<String>, AppError> {
    let mut length = 0_u32;
    unsafe {
        WinHttpQueryHeaders(
            request,
            WINHTTP_QUERY_LOCATION,
            std::ptr::null(),
            std::ptr::null_mut(),
            &mut length,
            std::ptr::null_mut(),
        )
    };
    if length == 0 {
        return Ok(None);
    }
    let mut buffer = vec![0_u16; length as usize / 2 + 1];
    let mut length = (buffer.len() * 2) as u32;
    let ok = unsafe {
        WinHttpQueryHeaders(
            request,
            WINHTTP_QUERY_LOCATION,
            std::ptr::null(),
            buffer.as_mut_ptr().cast(),
            &mut length,
            std::ptr::null_mut(),
        )
    };
    if ok == 0 {
        return Err(last_network_error());
    }
    let end = buffer
        .iter()
        .position(|unit| *unit == 0)
        .unwrap_or(buffer.len());
    Ok(Some(String::from_utf16_lossy(&buffer[..end])))
}

struct Handle(*mut c_void);

impl Handle {
    fn new(handle: *mut c_void) -> Result<Self, AppError> {
        if handle.is_null() {
            Err(last_network_error())
        } else {
            Ok(Self(handle))
        }
    }
}

impl Drop for Handle {
    fn drop(&mut self) {
        unsafe {
            let _ = WinHttpCloseHandle(self.0);
        }
    }
}

fn temp_path(path: &Path) -> PathBuf {
    let name = path
        .file_name()
        .map_or_else(String::new, |name| name.to_string_lossy().into_owned());
    path.with_file_name(format!("{name}.fgm-download"))
}

fn wide(value: &str) -> Vec<u16> {
    value.encode_utf16().chain(std::iter::once(0)).collect()
}

fn network(reason: &str) -> AppError {
    AppError::new(ErrorCode::NetworkError).with("reason", reason)
}

fn last_network_error() -> AppError {
    let code = unsafe { GetLastError() };
    network(&format!("WinHTTP error {code}"))
}

fn io_error(path: &Path, error: std::io::Error) -> AppError {
    AppError::new(ErrorCode::Internal)
        .with("path", path.display().to_string())
        .with("reason", error.to_string())
}
