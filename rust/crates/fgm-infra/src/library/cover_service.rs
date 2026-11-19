use std::path::PathBuf;
use std::sync::atomic::AtomicBool;

use fgm_domain::error::AppError;
use fgm_domain::library::{GameEntry, LibrarySource};

use crate::net::Downloader;
use crate::paths::AppPaths;

/// Downloads and caches Steam library covers.
pub struct CoverService;

impl CoverService {
    pub fn cover(paths: &AppPaths, game: &GameEntry) -> Result<Option<PathBuf>, AppError> {
        if game.source != LibrarySource::Steam {
            return Ok(None);
        }
        let Some(app_id) = game
            .app_id
            .as_deref()
            .filter(|id| !id.is_empty() && id.chars().all(|character| character.is_ascii_digit()))
        else {
            return Ok(None);
        };

        let directory = paths.cache().join("covers");
        let path = directory.join(format!("{app_id}.jpg"));
        if path.metadata().is_ok_and(|metadata| metadata.len() > 1024) {
            return Ok(Some(path));
        }

        std::fs::create_dir_all(&directory).map_err(|error| {
            AppError::new(fgm_domain::error::ErrorCode::Internal)
                .with("path", directory.display().to_string())
                .with("reason", error.to_string())
        })?;
        let url = format!(
            "https://shared.steamstatic.com/store_item_assets/steam/apps/{app_id}/library_600x900.jpg"
        );
        match Downloader::download(&url, &path, &AtomicBool::new(false)) {
            Ok(_) => Ok(Some(path)),
            Err(_) => {
                let _ = std::fs::remove_file(&path);
                Ok(None)
            }
        }
    }
}
