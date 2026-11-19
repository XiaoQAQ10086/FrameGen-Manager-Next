use std::ffi::OsString;
use std::path::{Path, PathBuf};

use fgm_domain::error::{AppError, ErrorCode};

/// Single source of truth for writable application directories.
pub struct AppPaths {
    root: PathBuf,
    portable: bool,
}

impl AppPaths {
    pub const DATA_DIR_ENV: &'static str = "FGM_DATA_DIR";
    pub const PORTABLE_MARKER: &'static str = "portable.flag";
    pub const PORTABLE_DATA_DIR: &'static str = "userdata";
    pub const APP_DATA_DIR: &'static str = "FrameGen-Manager-Next";

    pub fn new(root: impl Into<PathBuf>, portable: bool) -> Result<Self, AppError> {
        let root = root.into();
        if root.as_os_str().is_empty() {
            return Err(AppError::new(ErrorCode::InvalidPath).with("reason", "root is empty"));
        }

        let root = std::path::absolute(&root).map_err(|error| {
            AppError::new(ErrorCode::InvalidPath)
                .with("path", root.display().to_string())
                .with("reason", error.to_string())
        })?;

        Ok(Self { root, portable })
    }

    /// Resolves in this order:
    /// 1. `FGM_DATA_DIR` override
    /// 2. `portable.flag` next to the executable -> `<app>/data`
    /// 3. `%LOCALAPPDATA%\FrameGen-Manager-Next`
    pub fn create_default(application_directory: Option<&Path>) -> Result<Self, AppError> {
        let application_directory = match application_directory {
            Some(path) => path.to_path_buf(),
            None => std::env::current_exe()
                .map_err(|error| {
                    AppError::new(ErrorCode::Internal).with("reason", error.to_string())
                })?
                .parent()
                .ok_or_else(|| {
                    AppError::new(ErrorCode::InvalidPath)
                        .with("reason", "executable has no parent directory")
                })?
                .to_path_buf(),
        };

        Self::resolve_default(
            std::env::var_os(Self::DATA_DIR_ENV),
            &application_directory,
            std::env::var_os("LOCALAPPDATA").map(PathBuf::from),
        )
    }

    fn resolve_default(
        override_root: Option<OsString>,
        application_directory: &Path,
        local_app_data: Option<PathBuf>,
    ) -> Result<Self, AppError> {
        if let Some(root) = override_root.filter(|value| !value.is_empty()) {
            return Self::new(PathBuf::from(root), false);
        }

        if application_directory.join(Self::PORTABLE_MARKER).is_file() {
            let root = application_directory.join(Self::PORTABLE_DATA_DIR);
            Self::migrate_portable_state(application_directory, &root);
            return Self::new(root, true);
        }

        let local_app_data = local_app_data.ok_or_else(|| {
            AppError::new(ErrorCode::InvalidPath).with("reason", "LOCALAPPDATA is not set")
        })?;
        Self::new(local_app_data.join(Self::APP_DATA_DIR), false)
    }

    /// Older portable builds kept state inside Flutter's `data` asset folder.
    /// Move the known state directories into `userdata` once, leaving the
    /// engine's own files untouched.
    fn migrate_portable_state(application_directory: &Path, target: &Path) {
        let legacy = application_directory.join("data");
        if legacy == target || !legacy.is_dir() {
            return;
        }
        for name in ["config", "library", "logs", "state", "cache", "backups"] {
            let from = legacy.join(name);
            let to = target.join(name);
            if from.is_dir() && !to.exists() {
                if std::fs::create_dir_all(target).is_err() {
                    return;
                }
                let _ = std::fs::rename(&from, &to);
            }
        }
    }

    pub fn root(&self) -> &Path {
        &self.root
    }

    pub fn is_portable(&self) -> bool {
        self.portable
    }

    pub fn logs(&self) -> PathBuf {
        self.root.join("logs")
    }

    pub fn config(&self) -> PathBuf {
        self.root.join("config")
    }

    pub fn library(&self) -> PathBuf {
        self.root.join("library")
    }

    pub fn packs(&self) -> PathBuf {
        self.root.join("packs")
    }

    pub fn backups(&self) -> PathBuf {
        self.root.join("backups")
    }

    pub fn cache(&self) -> PathBuf {
        self.root.join("cache")
    }

    pub fn state(&self) -> PathBuf {
        self.root.join("state")
    }

    /// Downloaded installers are disposable and stay outside the data directory.
    pub fn update_staging() -> PathBuf {
        std::env::temp_dir()
            .join(Self::APP_DATA_DIR)
            .join("updates")
    }

    pub fn ensure_created(&self) -> Result<(), AppError> {
        let directories = [
            self.root.clone(),
            self.logs(),
            self.config(),
            self.library(),
            self.packs(),
            self.backups(),
            self.cache(),
            self.state(),
        ];

        for directory in directories {
            std::fs::create_dir_all(&directory).map_err(|error| io_error(&directory, error))?;
        }
        Ok(())
    }
}

fn io_error(path: &Path, error: std::io::Error) -> AppError {
    let code = match error.kind() {
        std::io::ErrorKind::PermissionDenied => ErrorCode::AccessDenied,
        _ => ErrorCode::Internal,
    };
    AppError::new(code)
        .with("path", path.display().to_string())
        .with("reason", error.to_string())
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::time::{SystemTime, UNIX_EPOCH};

    #[test]
    fn override_root_wins_and_is_not_portable() {
        let dir = TestDir::new("paths-override");
        std::fs::write(dir.path().join(AppPaths::PORTABLE_MARKER), b"").unwrap();
        let override_root = dir.path().join("custom");

        let paths = AppPaths::resolve_default(
            Some(override_root.clone().into_os_string()),
            dir.path(),
            Some(dir.path().join("local")),
        )
        .unwrap();

        assert_eq!(paths.root(), override_root.as_path());
        assert!(!paths.is_portable());
    }

    #[test]
    fn portable_marker_selects_userdata_subdirectory() {
        let dir = TestDir::new("paths-portable");
        std::fs::write(dir.path().join(AppPaths::PORTABLE_MARKER), b"").unwrap();

        let paths =
            AppPaths::resolve_default(None, dir.path(), Some(dir.path().join("local"))).unwrap();

        assert_eq!(
            paths.root(),
            dir.path().join(AppPaths::PORTABLE_DATA_DIR).as_path()
        );
        assert!(paths.is_portable());
    }

    #[test]
    fn portable_state_moves_out_of_the_asset_folder() {
        let dir = TestDir::new("paths-portable-migrate");
        std::fs::write(dir.path().join(AppPaths::PORTABLE_MARKER), b"").unwrap();
        let legacy = dir.path().join("data");
        std::fs::create_dir_all(legacy.join("library")).unwrap();
        std::fs::create_dir_all(legacy.join("flutter_assets")).unwrap();
        std::fs::write(legacy.join("library").join("library.json"), b"{}").unwrap();

        let paths =
            AppPaths::resolve_default(None, dir.path(), Some(dir.path().join("local"))).unwrap();

        assert_eq!(
            paths.root(),
            dir.path().join(AppPaths::PORTABLE_DATA_DIR).as_path()
        );
        assert!(paths.root().join("library").join("library.json").is_file());
        assert!(legacy.join("flutter_assets").is_dir());
    }

    #[test]
    fn local_app_data_is_the_default() {
        let dir = TestDir::new("paths-local");
        let local = dir.path().join("local");

        let paths = AppPaths::resolve_default(None, dir.path(), Some(local.clone())).unwrap();

        assert_eq!(paths.root(), local.join(AppPaths::APP_DATA_DIR).as_path());
        assert!(!paths.is_portable());
    }

    #[test]
    fn ensure_created_builds_all_directories() {
        let dir = TestDir::new("paths-create");
        let paths = AppPaths::new(dir.path().join("data"), false).unwrap();

        paths.ensure_created().unwrap();

        for directory in [
            paths.logs(),
            paths.config(),
            paths.library(),
            paths.packs(),
            paths.backups(),
            paths.cache(),
            paths.state(),
        ] {
            assert!(directory.is_dir(), "{}", directory.display());
        }
    }

    struct TestDir(PathBuf);

    impl TestDir {
        fn new(name: &str) -> Self {
            let nanos = SystemTime::now()
                .duration_since(UNIX_EPOCH)
                .unwrap()
                .as_nanos();
            let path =
                std::env::temp_dir().join(format!("fgm-{name}-{}-{nanos}", std::process::id()));
            std::fs::create_dir_all(&path).unwrap();
            Self(path)
        }

        fn path(&self) -> &Path {
            &self.0
        }
    }

    impl Drop for TestDir {
        fn drop(&mut self) {
            let _ = std::fs::remove_dir_all(&self.0);
        }
    }
}
