#![allow(unsafe_code)]

use fgm_core::ports::Clock;

pub mod deployment;
pub mod dlss5;
pub mod fs;
pub mod gpu;
pub mod hashing;
pub mod library;
pub mod logging;
pub mod net;
pub mod nvapi;
pub mod pack_json;
pub mod pack_payload;
pub mod packs;
pub mod paths;
pub mod pe;
pub mod platform;
pub mod process;
pub mod settings;
pub mod smooth_motion;
pub mod updates;

pub struct SystemClock;

impl Clock for SystemClock {
    fn now_unix_ms(&self) -> i64 {
        std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .map(|value| value.as_millis() as i64)
            .unwrap_or_default()
    }
}
