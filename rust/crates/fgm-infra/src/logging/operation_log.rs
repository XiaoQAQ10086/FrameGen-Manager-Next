use std::collections::BTreeMap;
use std::io::{Read, Seek, SeekFrom, Write};
use std::path::{Path, PathBuf};
use std::time::{SystemTime, UNIX_EPOCH};

use fgm_domain::error::{AppError, ErrorCode};

const MAX_BYTES: u64 = 5 * 1024 * 1024;
const KEEP_FILES: u32 = 3;
const TAIL_BYTES: u64 = 256 * 1024;

/// Append-only JSON-lines operation log. Messages are English diagnostics.
pub struct OperationLog {
    path: PathBuf,
}

impl OperationLog {
    pub fn new(path: impl Into<PathBuf>) -> Self {
        Self { path: path.into() }
    }

    pub fn append(
        &self,
        operation_id: u64,
        level: &str,
        message: &str,
        fields: &[(&str, &str)],
    ) -> Result<(), AppError> {
        let parent = self.path.parent().ok_or_else(|| {
            AppError::new(ErrorCode::InvalidPath).with("path", self.path.display().to_string())
        })?;
        std::fs::create_dir_all(parent).map_err(|error| io_error(parent, error))?;

        let mut field_map = BTreeMap::new();
        for (key, value) in fields {
            field_map.insert(*key, *value);
        }
        let line = serde_json::json!({
            "ts": SystemTime::now()
                .duration_since(UNIX_EPOCH)
                .map_or(0, |value| value.as_millis() as i64),
            "op": operation_id,
            "level": level,
            "message": message,
            "fields": field_map,
        })
        .to_string();

        self.rotate_if_needed()?;
        let mut file = std::fs::OpenOptions::new()
            .create(true)
            .append(true)
            .open(&self.path)
            .map_err(|error| io_error(&self.path, error))?;
        writeln!(file, "{line}").map_err(|error| io_error(&self.path, error))
    }

    fn rotate_if_needed(&self) -> Result<(), AppError> {
        let size = self
            .path
            .metadata()
            .map(|metadata| metadata.len())
            .unwrap_or(0);
        if size < MAX_BYTES {
            return Ok(());
        }
        let _ = std::fs::remove_file(self.rotated(KEEP_FILES));
        for index in (1..KEEP_FILES).rev() {
            let from = self.rotated(index);
            if from.is_file() {
                let _ = std::fs::rename(&from, self.rotated(index + 1));
            }
        }
        std::fs::rename(&self.path, self.rotated(1)).map_err(|error| io_error(&self.path, error))
    }

    fn rotated(&self, index: u32) -> PathBuf {
        PathBuf::from(format!("{}.{index}", self.path.display()))
    }

    pub fn tail(&self, max_lines: usize) -> Result<Vec<String>, AppError> {
        let mut file = match std::fs::File::open(&self.path) {
            Ok(file) => file,
            Err(error) if error.kind() == std::io::ErrorKind::NotFound => return Ok(Vec::new()),
            Err(error) => return Err(io_error(&self.path, error)),
        };
        let length = file
            .metadata()
            .map_err(|error| io_error(&self.path, error))?
            .len();
        let start = length.saturating_sub(TAIL_BYTES);
        file.seek(SeekFrom::Start(start))
            .map_err(|error| io_error(&self.path, error))?;
        let mut bytes = Vec::with_capacity((length - start) as usize);
        file.read_to_end(&mut bytes)
            .map_err(|error| io_error(&self.path, error))?;
        let text = String::from_utf8_lossy(&bytes);
        let text = if start > 0 {
            text.split_once('\n').map_or("", |(_, rest)| rest)
        } else {
            text.as_ref()
        };
        let mut lines: Vec<String> = text.lines().map(str::to_string).collect();
        if lines.len() > max_lines {
            lines.drain(0..lines.len() - max_lines);
        }
        Ok(lines)
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
    fn appends_and_tails_lines() {
        let nanos = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .unwrap()
            .as_nanos();
        let root = std::env::temp_dir().join(format!("fgm-log-{}-{nanos}", std::process::id()));
        let log = OperationLog::new(root.join("fgm.log"));

        log.append(1, "info", "deploy", &[("family", "framegen")])
            .unwrap();
        log.append(2, "warn", "conflict", &[]).unwrap();
        let lines = log.tail(1).unwrap();

        assert_eq!(lines.len(), 1);
        assert!(lines[0].contains("\"op\":2"));
        assert!(lines[0].contains("conflict"));
        let _ = std::fs::remove_dir_all(root);
    }
}

#[cfg(test)]
mod rotation_tests {
    use super::*;
    use std::time::{SystemTime, UNIX_EPOCH};

    #[test]
    fn rotates_after_the_size_limit() {
        let nanos = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .unwrap()
            .as_nanos();
        let root = std::env::temp_dir().join(format!("fgm-rotate-{nanos}"));
        std::fs::create_dir_all(&root).unwrap();
        let log = OperationLog::new(root.join("fgm.log"));
        std::fs::write(log.path.clone(), vec![b'x'; MAX_BYTES as usize]).unwrap();

        log.append(1, "info", "after rotation", &[]).unwrap();

        assert!(log.rotated(1).is_file());
        assert!(
            log.tail(10)
                .unwrap()
                .iter()
                .any(|line| line.contains("after rotation"))
        );
        let _ = std::fs::remove_dir_all(root);
    }
}
