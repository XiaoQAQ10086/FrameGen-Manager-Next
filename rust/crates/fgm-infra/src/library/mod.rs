pub mod cover_service;
pub mod engine_detector;
pub mod epic_game_scanner;
pub mod epic_locator;
pub mod graphics_api_detector;
pub mod library_cache;
pub mod library_scanner;
pub mod library_service;
pub mod library_store;
pub mod loose_game_scanner;
pub mod render_exe;
pub mod render_exe_selector;
pub mod steam_game_scanner;
pub mod steam_locator;

mod valve_key_values;

pub use cover_service::CoverService;
pub use engine_detector::{EngineDetector, GameEngine};
pub use epic_game_scanner::EpicGameScanner;
pub use epic_locator::EpicLocator;
pub use graphics_api_detector::GraphicsApiDetector;
pub use library_cache::LibraryCache;
pub use library_scanner::LibraryScanner;
pub use library_service::LibraryService;
pub use library_store::LibraryStore;
pub use loose_game_scanner::LooseGameScanner;
pub use render_exe::RenderExe;
pub use render_exe_selector::RenderExeSelector;
pub use steam_game_scanner::SteamGameScanner;
pub use steam_locator::SteamLocator;

pub(crate) fn path_key(path: &std::path::Path) -> String {
    let mut value = path.to_string_lossy().replace('/', "\\").to_lowercase();
    while value.len() > 3 && value.ends_with('\\') {
        value.pop();
    }
    value
}
