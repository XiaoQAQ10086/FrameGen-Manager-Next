use std::ffi::c_void;

/// Opaque handle to an NVIDIA driver profile.
#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub struct NvApiProfile(usize);

impl NvApiProfile {
    pub(crate) fn from_raw(pointer: *mut c_void) -> Self {
        Self(pointer as usize)
    }

    pub(crate) fn as_ptr(self) -> *mut c_void {
        self.0 as *mut c_void
    }

    pub fn is_null(self) -> bool {
        self.0 == 0
    }
}
