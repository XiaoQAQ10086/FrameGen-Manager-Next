use fgm_domain::gpu::GpuRoute;

use super::hags::HagsState;

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct GpuInfo {
    pub name: String,
    pub driver_version: String,
    pub route: GpuRoute,
    pub driver_supported: bool,
    pub hags: HagsState,
}
