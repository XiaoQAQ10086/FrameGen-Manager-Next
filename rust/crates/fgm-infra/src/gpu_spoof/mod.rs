pub mod display_adapter;
pub mod gpu_registry;
pub mod gpu_spoof_manifest;
pub mod gpu_spoof_service;

pub use display_adapter::DisplayAdapter;
pub use gpu_spoof_manifest::GpuSpoofManifest;
pub use gpu_spoof_service::{GpuRestoreMode, GpuSpoofInspection, GpuSpoofService, PRESETS};
