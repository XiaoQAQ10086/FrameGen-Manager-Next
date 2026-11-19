#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
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
