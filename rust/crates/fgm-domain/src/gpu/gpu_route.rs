#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
pub enum GpuRoute {
    Sm86,
    Sm75,
    Gtx16,
    Pascal,
    NotNeeded,
    Unsupported,
    #[default]
    Unknown,
}
