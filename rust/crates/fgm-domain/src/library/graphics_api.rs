use serde::{Deserialize, Serialize};

#[derive(Clone, Copy, Debug, Default, Deserialize, Serialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub enum GraphicsApi {
    #[default]
    Unknown,
    Dx12,
    Vulkan,
    Dx11,
    Dx10,
    Dx9,
    OpenGl,
}
