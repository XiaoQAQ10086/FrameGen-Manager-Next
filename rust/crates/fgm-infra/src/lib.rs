#![allow(unsafe_code)]

use fgm_core::ports::Clock;

pub mod hashing;
pub mod pack_json;
pub mod pack_payload;

pub struct SystemClock;

impl Clock for SystemClock {
    fn now_unix_ms(&self) -> i64 {
        std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .map(|value| value.as_millis() as i64)
            .unwrap_or_default()
    }
}
