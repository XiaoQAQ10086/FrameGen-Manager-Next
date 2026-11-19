#![allow(unsafe_code)]

use fgm_core::operation::OperationPhase;
use std::ffi::c_char;
use std::sync::LazyLock;

static VERSION: LazyLock<Vec<u8>> = LazyLock::new(|| {
    let mut bytes = fgm_domain::version::VERSION.as_bytes().to_vec();
    bytes.push(0);
    bytes
});

/// NUL-terminated stable keys indexed by `OperationPhase::wire_id`.
const PHASE_NAMES: [&[u8]; 7] = [
    b"planned\0",
    b"backingUp\0",
    b"writing\0",
    b"verifying\0",
    b"installed\0",
    b"rolledBack\0",
    b"partial\0",
];

/// Returns a process-lifetime, NUL-terminated UTF-8 version string.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_version() -> *const c_char {
    VERSION.as_ptr().cast()
}

/// Maps the numeric deployment phase used by the C ABI to its stable key.
///
/// Returns a null pointer for an unknown phase so the caller can surface
/// `ErrorCode::InvalidArgument` without allocating.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_operation_phase_name(phase: u32) -> *const c_char {
    match OperationPhase::from_wire(phase) {
        Ok(phase) => PHASE_NAMES[phase.wire_id() as usize].as_ptr().cast(),
        Err(_) => std::ptr::null(),
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::ffi::CStr;

    #[test]
    fn phase_names_are_stable_keys() {
        for (id, expected) in [
            (0, "planned"),
            (1, "backingUp"),
            (2, "writing"),
            (3, "verifying"),
            (4, "installed"),
            (5, "rolledBack"),
            (6, "partial"),
        ] {
            let name = unsafe { CStr::from_ptr(fgm_operation_phase_name(id)) };
            assert_eq!(name.to_str().unwrap(), expected);
        }
    }

    #[test]
    fn unknown_phase_returns_null() {
        assert!(fgm_operation_phase_name(7).is_null());
    }

    #[test]
    fn version_is_nul_terminated() {
        let version = unsafe { CStr::from_ptr(fgm_version()) };
        assert_eq!(version.to_str().unwrap(), fgm_domain::version::VERSION);
    }
}
