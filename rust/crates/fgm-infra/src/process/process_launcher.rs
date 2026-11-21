use std::path::Path;
use std::process::Command;

use fgm_domain::error::{AppError, ErrorCode};

use super::process_runner::spawn_error;

const CREATE_NEW_PROCESS_GROUP: u32 = 0x0000_0200;

/// Starts a detached child process or opens a URL with the shell handler.
pub struct ProcessLauncher;

impl ProcessLauncher {
    pub fn launch_detached(
        executable: &Path,
        working_directory: Option<&Path>,
    ) -> Result<(), AppError> {
        Self::launch_detached_with_args(executable, working_directory, &[])
    }

    pub fn launch_detached_with_args(
        executable: &Path,
        working_directory: Option<&Path>,
        args: &[&str],
    ) -> Result<(), AppError> {
        if !executable.is_file() {
            return Err(AppError::new(ErrorCode::FileNotFound)
                .with("path", executable.display().to_string())
                .with("reason", "the executable does not exist"));
        }

        let mut command = Command::new(executable);
        command.args(args);
        command
            .stdin(std::process::Stdio::null())
            .stdout(std::process::Stdio::null())
            .stderr(std::process::Stdio::null());
        if let Some(directory) = working_directory {
            command.current_dir(directory);
        }
        #[cfg(windows)]
        {
            use std::os::windows::process::CommandExt;
            command.creation_flags(CREATE_NEW_PROCESS_GROUP);
        }
        command
            .spawn()
            .map(|_| ())
            .map_err(|error| spawn_error(executable, error))
    }

    /// Opens a folder in Explorer.
    pub fn open_folder(path: &Path) -> Result<(), AppError> {
        if !path.exists() {
            return Err(
                AppError::new(ErrorCode::FileNotFound).with("path", path.display().to_string())
            );
        }
        Command::new("explorer.exe")
            .arg(path)
            .stdin(std::process::Stdio::null())
            .stdout(std::process::Stdio::null())
            .stderr(std::process::Stdio::null())
            .spawn()
            .map(|_| ())
            .map_err(|error| spawn_error(path, error))
    }

    /// Opens an `ms-settings:` page.
    pub fn open_settings_page(uri: &str) -> Result<(), AppError> {
        if !uri.starts_with("ms-settings:")
            || uri.chars().any(char::is_control)
            || uri.contains('"')
            || uri.contains(' ')
        {
            return Err(AppError::new(ErrorCode::InvalidArgument).with("url", uri.to_string()));
        }
        Command::new("cmd.exe")
            .args(["/C", "start", ""])
            .arg(uri)
            .stdin(std::process::Stdio::null())
            .stdout(std::process::Stdio::null())
            .stderr(std::process::Stdio::null())
            .spawn()
            .map(|_| ())
            .map_err(|error| {
                AppError::new(ErrorCode::Internal)
                    .with("url", uri.to_string())
                    .with("reason", error.to_string())
            })
    }

    /// Requests a restart with a short delay so the UI can show a toast first.
    pub fn restart_computer() -> Result<(), AppError> {
        Command::new("shutdown.exe")
            .args(["/r", "/t", "5", "/c", "FrameGen Manager-Next"])
            .stdin(std::process::Stdio::null())
            .stdout(std::process::Stdio::null())
            .stderr(std::process::Stdio::null())
            .spawn()
            .map(|_| ())
            .map_err(|error| AppError::new(ErrorCode::Internal).with("reason", error.to_string()))
    }

    /// Opens a registered protocol handler such as `steam://`.
    pub fn open_protocol(url: &str) -> Result<(), AppError> {
        let trimmed = url.trim();
        let allowed = ["steam://", "com.epicgames.launcher://"];
        if !allowed
            .iter()
            .any(|prefix| trimmed.to_lowercase().starts_with(prefix))
            || trimmed.chars().any(char::is_control)
            || trimmed.contains('"')
            || trimmed.contains(' ')
        {
            return Err(AppError::new(ErrorCode::InvalidArgument).with("url", trimmed.to_string()));
        }

        Command::new("cmd.exe")
            .args(["/C", "start", ""])
            .arg(trimmed)
            .stdin(std::process::Stdio::null())
            .stdout(std::process::Stdio::null())
            .stderr(std::process::Stdio::null())
            .spawn()
            .map(|_| ())
            .map_err(|error| {
                AppError::new(ErrorCode::Internal)
                    .with("url", trimmed.to_string())
                    .with("reason", error.to_string())
            })
    }

    pub fn open_url(url: &str) -> Result<(), AppError> {
        let trimmed = url.trim();
        let lower = trimmed.to_lowercase();
        if !(lower.starts_with("https://") || lower.starts_with("http://"))
            || trimmed.chars().any(char::is_control)
            || trimmed.contains('"')
        {
            return Err(AppError::new(ErrorCode::InvalidArgument).with("url", trimmed.to_string()));
        }

        let mut command = Command::new("rundll32.exe");
        command
            .arg("url.dll,FileProtocolHandler")
            .arg(trimmed)
            .stdin(std::process::Stdio::null())
            .stdout(std::process::Stdio::null())
            .stderr(std::process::Stdio::null())
            .spawn()
            .map(|_| ())
            .map_err(|error| {
                AppError::new(ErrorCode::Internal)
                    .with("url", trimmed.to_string())
                    .with("reason", error.to_string())
            })
    }
}
