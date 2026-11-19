use serde::{Deserialize, Serialize};

#[derive(Clone, Copy, Debug, Default, Deserialize, Serialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
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
