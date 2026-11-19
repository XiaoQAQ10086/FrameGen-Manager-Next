//! FFI contract tests live in this child module so the export surface stays
//! under the file-size budget.

use std::ffi::CStr;

use super::*;

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

#[test]
fn operation_progress_and_cancel_are_exposed() {
    use std::sync::atomic::Ordering;

    use serde_json::Value;

    fn read(pointer: *mut c_char) -> Value {
        let text = unsafe { CStr::from_ptr(pointer) }
            .to_str()
            .unwrap()
            .to_string();
        unsafe { fgm_string_free(pointer) };
        serde_json::from_str(&text).unwrap()
    }

    let operation = commands_operation::begin("test");
    let progress = read(commands_operation::fgm_operation_progress_json());
    assert_eq!(progress["active"], true);
    assert_eq!(progress["kind"], "test");

    let cancelled = read(commands_operation::fgm_operation_cancel_json());
    assert_eq!(cancelled["cancelled"], true);
    assert!(operation.cancelled.load(Ordering::Relaxed));

    commands_operation::finish();
    let finished = read(commands_operation::fgm_operation_progress_json());
    assert_eq!(finished["active"], false);
}
