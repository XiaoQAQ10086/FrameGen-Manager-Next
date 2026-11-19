use std::ffi::c_void;

/// Hardware-accelerated GPU scheduling state shown next to the driver.
#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
pub enum HagsState {
    #[default]
    Unknown,
    Disabled,
    Enabled,
    PendingReboot,
    Unsupported,
}

const WDDM_27_CAPS: i32 = 70;
const HW_SCH_SUPPORTED: u32 = 1 << 0;
const HW_SCH_ENABLED: u32 = 1 << 1;
const MAX_ADAPTERS: usize = 32;

#[repr(C)]
struct OpenAdapterFromHdc {
    hdc: *mut c_void,
    adapter: u32,
    adapter_luid: i64,
    vidpn_source_id: u32,
}

#[repr(C)]
struct QueryAdapterInfo {
    adapter: u32,
    info_type: i32,
    private_data: *mut c_void,
    size: u32,
}

#[repr(C)]
struct CloseAdapter {
    adapter: u32,
}

#[repr(C)]
struct EnumAdapters2 {
    num_adapters: u32,
    adapters: *mut c_void,
}

#[repr(C)]
#[derive(Clone, Copy)]
struct AdapterInfo {
    adapter: u32,
    adapter_luid: i64,
    sources: u32,
    present: i32,
}

#[link(name = "gdi32")]
unsafe extern "system" {
    fn CreateDCW(
        driver: *const u16,
        device: *const u16,
        output: *const u16,
        init: *mut c_void,
    ) -> *mut c_void;
    fn DeleteDC(hdc: *mut c_void) -> i32;
    fn D3DKMTOpenAdapterFromHdc(data: *mut OpenAdapterFromHdc) -> i32;
    fn D3DKMTQueryAdapterInfo(data: *mut QueryAdapterInfo) -> i32;
    fn D3DKMTCloseAdapter(data: *mut CloseAdapter) -> i32;
    fn D3DKMTEnumAdapters2(data: *mut EnumAdapters2) -> i32;
}

/// Reads the live HAGS state from every display adapter, then the registry.
pub struct HagsProbe;

impl HagsProbe {
    pub fn read() -> HagsState {
        let live = Self::query_adapters().or_else(Self::query_display_dc);
        let registry = crate::platform::registry::read_dword(
            crate::platform::registry::HKEY_LOCAL_MACHINE,
            r"SYSTEM\CurrentControlSet\Control\GraphicsDrivers",
            "HwSchMode",
        );
        classify(live, registry)
    }

    fn query_adapters() -> Option<(bool, bool)> {
        let size = std::mem::size_of::<AdapterInfo>();
        let mut buffer = vec![0_u8; size * MAX_ADAPTERS];
        let mut enumeration = EnumAdapters2 {
            num_adapters: MAX_ADAPTERS as u32,
            adapters: buffer.as_mut_ptr().cast(),
        };
        let status = unsafe { D3DKMTEnumAdapters2(&mut enumeration) };
        if status != 0 || enumeration.num_adapters == 0 {
            return None;
        }
        let count = (enumeration.num_adapters as usize).min(MAX_ADAPTERS);
        let mut enabled = false;
        let mut supported = false;
        let mut queried = false;
        for index in 0..count {
            let info = unsafe {
                std::ptr::read_unaligned(buffer.as_ptr().add(index * size).cast::<AdapterInfo>())
            };
            let caps = query_caps(info.adapter);
            let _ = unsafe {
                D3DKMTCloseAdapter(&mut CloseAdapter {
                    adapter: info.adapter,
                })
            };
            if let Some(caps) = caps {
                queried = true;
                enabled |= caps & HW_SCH_ENABLED != 0;
                supported |= caps & HW_SCH_SUPPORTED != 0;
            }
        }
        queried.then_some((enabled, supported))
    }

    fn query_display_dc() -> Option<(bool, bool)> {
        let display: Vec<u16> = "DISPLAY".encode_utf16().chain(std::iter::once(0)).collect();
        let hdc = unsafe {
            CreateDCW(
                display.as_ptr(),
                std::ptr::null(),
                std::ptr::null(),
                std::ptr::null_mut(),
            )
        };
        if hdc.is_null() {
            return None;
        }
        let mut open = OpenAdapterFromHdc {
            hdc,
            adapter: 0,
            adapter_luid: 0,
            vidpn_source_id: 0,
        };
        let result = if unsafe { D3DKMTOpenAdapterFromHdc(&mut open) } == 0 {
            let caps = query_caps(open.adapter);
            let _ = unsafe {
                D3DKMTCloseAdapter(&mut CloseAdapter {
                    adapter: open.adapter,
                })
            };
            caps.map(|caps| (caps & HW_SCH_ENABLED != 0, caps & HW_SCH_SUPPORTED != 0))
        } else {
            None
        };
        unsafe { DeleteDC(hdc) };
        result
    }
}

fn query_caps(adapter: u32) -> Option<u32> {
    let mut caps = 0_u32;
    let mut query = QueryAdapterInfo {
        adapter,
        info_type: WDDM_27_CAPS,
        private_data: (&mut caps as *mut u32).cast(),
        size: std::mem::size_of::<u32>() as u32,
    };
    (unsafe { D3DKMTQueryAdapterInfo(&mut query) } == 0).then_some(caps)
}

fn classify(live: Option<(bool, bool)>, registry: Option<u32>) -> HagsState {
    match live {
        Some((true, _)) => HagsState::Enabled,
        Some((false, _)) if registry == Some(2) => HagsState::PendingReboot,
        Some((false, _)) if registry == Some(1) => HagsState::Disabled,
        Some((false, supported)) => {
            if supported {
                HagsState::Disabled
            } else {
                HagsState::Unsupported
            }
        }
        None if registry == Some(2) => HagsState::Enabled,
        None if registry == Some(1) => HagsState::Disabled,
        None => HagsState::Unknown,
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn classifies_live_and_registry_states() {
        assert_eq!(classify(Some((true, true)), Some(1)), HagsState::Enabled);
        assert_eq!(
            classify(Some((false, true)), Some(2)),
            HagsState::PendingReboot
        );
        assert_eq!(classify(Some((false, true)), Some(1)), HagsState::Disabled);
        assert_eq!(classify(Some((false, false)), None), HagsState::Unsupported);
        assert_eq!(classify(None, Some(1)), HagsState::Disabled);
        assert_eq!(classify(None, None), HagsState::Unknown);
    }
}
