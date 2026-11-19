use std::path::Path;

use fgm_domain::library::GraphicsApi;

use crate::pe::PeImports;

const WRAPPER_DLLS: [&str; 4] = ["dxgi.dll", "d3d12.dll", "d3d11.dll", "d3d9.dll"];
const ENGINE_DLLS: [&str; 4] = [
    "unityplayer.dll",
    "engine2.dll",
    "engine.dll",
    "crysystem.dll",
];

/// Detects the graphics API from wrapper DLLs, filename tokens, PE imports and
/// colocated runtime DLLs, in the same priority order as the legacy scanner.
pub struct GraphicsApiDetector;

impl GraphicsApiDetector {
    pub fn detect(executable: &Path, install_directory: &Path) -> GraphicsApi {
        if wrapper_is_vulkan(install_directory) {
            return GraphicsApi::Vulkan;
        }

        if let Some(api) = api_from_tokens(executable) {
            return api;
        }

        if let Some(api) = api_from_imports(executable) {
            return api;
        }

        if let Some(api) = api_from_runtime_dlls(install_directory) {
            return api;
        }

        for dll in ENGINE_DLLS {
            let path = install_directory.join(dll);
            if !path.is_file() {
                continue;
            }
            if let Some(api) = api_from_tokens(&path).or_else(|| api_from_imports(&path)) {
                return api;
            }
        }

        GraphicsApi::Unknown
    }
}

fn wrapper_is_vulkan(install_directory: &Path) -> bool {
    WRAPPER_DLLS.iter().any(|dll| {
        imports(&install_directory.join(dll))
            .iter()
            .any(|name| name == "vulkan-1.dll")
    })
}

fn api_from_tokens(path: &Path) -> Option<GraphicsApi> {
    let stem = path.file_stem()?.to_string_lossy().to_lowercase();
    let tokens: Vec<&str> = stem
        .split(|character: char| !character.is_ascii_alphanumeric())
        .filter(|token| !token.is_empty())
        .collect();

    if tokens
        .iter()
        .any(|token| matches!(*token, "d3d12" | "dx12"))
    {
        return Some(GraphicsApi::Dx12);
    }
    if tokens.iter().any(|token| matches!(*token, "vulkan" | "vk")) {
        return Some(GraphicsApi::Vulkan);
    }
    if tokens
        .iter()
        .any(|token| matches!(*token, "d3d11" | "dx11"))
    {
        return Some(GraphicsApi::Dx11);
    }
    if tokens
        .iter()
        .any(|token| matches!(*token, "d3d10" | "dx10"))
    {
        return Some(GraphicsApi::Dx10);
    }
    if tokens.iter().any(|token| matches!(*token, "d3d9" | "dx9")) {
        return Some(GraphicsApi::Dx9);
    }
    if tokens.iter().any(|token| matches!(*token, "opengl" | "gl")) {
        return Some(GraphicsApi::OpenGl);
    }
    None
}

fn api_from_imports(path: &Path) -> Option<GraphicsApi> {
    let imports = imports(path);
    if imports
        .iter()
        .any(|name| name == "d3d12.dll" || name == "d3d12core.dll")
    {
        return Some(GraphicsApi::Dx12);
    }
    if imports.iter().any(|name| name == "vulkan-1.dll") {
        return Some(GraphicsApi::Vulkan);
    }
    if imports.iter().any(|name| name == "d3d11.dll") {
        return Some(GraphicsApi::Dx11);
    }
    if imports
        .iter()
        .any(|name| name == "d3d10.dll" || name == "d3d10_1.dll")
    {
        return Some(GraphicsApi::Dx10);
    }
    if imports.iter().any(|name| name == "d3d9.dll") {
        return Some(GraphicsApi::Dx9);
    }
    if imports.iter().any(|name| name == "opengl32.dll") {
        return Some(GraphicsApi::OpenGl);
    }
    None
}

fn api_from_runtime_dlls(install_directory: &Path) -> Option<GraphicsApi> {
    if install_directory.join("vulkan-1.dll").is_file() {
        return Some(GraphicsApi::Vulkan);
    }
    if install_directory.join("d3d12.dll").is_file() {
        return Some(GraphicsApi::Dx12);
    }
    None
}

fn imports(path: &Path) -> Vec<String> {
    if !path.is_file() {
        return Vec::new();
    }
    PeImports::read_file(path)
        .unwrap_or_default()
        .into_iter()
        .map(|name| name.to_ascii_lowercase())
        .collect()
}
