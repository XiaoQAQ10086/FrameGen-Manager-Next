use fgm_domain::gpu::GpuRoute;

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct GpuInfo {
    pub name: String,
    pub driver_version: String,
    pub route: GpuRoute,
    pub driver_supported: bool,
}
