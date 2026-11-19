use std::io::Read;
use std::path::Path;

use fgm_domain::error::{AppError, ErrorCode};
use fgm_domain::safety::SafePath;

use crate::fs::AtomicFile;

const EOCD: [u8; 4] = [0x50, 0x4b, 0x05, 0x06];
const CENTRAL: [u8; 4] = [0x50, 0x4b, 0x01, 0x02];
const LOCAL: [u8; 4] = [0x50, 0x4b, 0x03, 0x04];
const MAX_EOCD_SEARCH: usize = 65_557;
const MAX_ENTRIES: usize = 20_000;
const MAX_UNCOMPRESSED: u64 = 4 * 1024 * 1024 * 1024;

/// Extracts a standard (non-ZIP64) archive into a directory with SafePath checks.
pub struct ZipArchive;

impl ZipArchive {
    pub fn extract_to(data: &[u8], destination: &Path) -> Result<usize, AppError> {
        let eocd = find_eocd(data).ok_or_else(|| invalid("ZIP end record not found"))?;
        let count = read_u16(data, eocd + 10)? as usize;
        let central_size = read_u32(data, eocd + 12)? as usize;
        let central_offset = read_u32(data, eocd + 16)? as usize;
        if count == 0 || count > MAX_ENTRIES || central_offset == u32::MAX as usize {
            return Err(invalid("unsupported or empty ZIP"));
        }
        let zip_start = eocd
            .checked_sub(central_size + central_offset)
            .ok_or_else(|| invalid("invalid ZIP central directory"))?;

        let mut position = zip_start
            .checked_add(central_offset)
            .ok_or_else(|| invalid("invalid ZIP central directory"))?;
        let mut extracted = 0;
        let mut total = 0_u64;

        for _ in 0..count {
            if data.get(position..position + 4) != Some(&CENTRAL) {
                return Err(invalid("invalid ZIP central entry"));
            }
            let method = read_u16(data, position + 10)?;
            let compressed_size = read_u32(data, position + 20)? as usize;
            let uncompressed_size = read_u32(data, position + 24)? as usize;
            let name_length = read_u16(data, position + 28)? as usize;
            let extra_length = read_u16(data, position + 30)? as usize;
            let comment_length = read_u16(data, position + 32)? as usize;
            let local_offset = read_u32(data, position + 42)? as usize;
            let name_bytes = data
                .get(position + 46..position + 46 + name_length)
                .ok_or_else(|| invalid("truncated ZIP entry name"))?;
            let name = std::str::from_utf8(name_bytes)
                .map_err(|_| invalid("ZIP entry name is not UTF-8"))?;

            position = position
                .checked_add(46 + name_length + extra_length + comment_length)
                .ok_or_else(|| invalid("ZIP central directory overflow"))?;

            if name.ends_with('/') {
                continue;
            }
            let relative = SafePath::normalize_relative(name)?;
            total = total
                .checked_add(uncompressed_size as u64)
                .ok_or_else(|| invalid("ZIP size overflow"))?;
            if total > MAX_UNCOMPRESSED {
                return Err(invalid("ZIP expands beyond the size limit"));
            }

            let bytes = read_entry(
                data,
                zip_start
                    .checked_add(local_offset)
                    .ok_or_else(|| invalid("invalid ZIP local offset"))?,
                method,
                compressed_size,
                uncompressed_size,
            )?;
            AtomicFile::write(&destination.join(relative), &bytes)?;
            extracted += 1;
        }

        Ok(extracted)
    }
}

fn read_entry(
    data: &[u8],
    local: usize,
    method: u16,
    compressed_size: usize,
    uncompressed_size: usize,
) -> Result<Vec<u8>, AppError> {
    if data.get(local..local + 4) != Some(&LOCAL) {
        return Err(invalid("invalid ZIP local header"));
    }
    let name_length = read_u16(data, local + 26)? as usize;
    let extra_length = read_u16(data, local + 28)? as usize;
    let start = local
        .checked_add(30 + name_length + extra_length)
        .ok_or_else(|| invalid("ZIP local header overflow"))?;
    let compressed = data
        .get(start..start + compressed_size)
        .ok_or_else(|| invalid("truncated ZIP payload"))?;

    let bytes = match method {
        0 => compressed.to_vec(),
        8 => {
            let mut output = Vec::with_capacity(uncompressed_size);
            flate2::read::DeflateDecoder::new(compressed)
                .read_to_end(&mut output)
                .map_err(|error| invalid(&error.to_string()))?;
            output
        }
        _ => return Err(invalid("unsupported ZIP compression method")),
    };
    if bytes.len() != uncompressed_size {
        return Err(invalid("ZIP entry size mismatch"));
    }
    Ok(bytes)
}

fn find_eocd(data: &[u8]) -> Option<usize> {
    let start = data.len().saturating_sub(MAX_EOCD_SEARCH);
    (start..data.len().saturating_sub(3))
        .rev()
        .find(|index| data.get(*index..*index + 4) == Some(&EOCD))
}

fn read_u16(data: &[u8], offset: usize) -> Result<u16, AppError> {
    Ok(u16::from_le_bytes([
        *data.get(offset).ok_or_else(|| invalid("truncated ZIP"))?,
        *data
            .get(offset + 1)
            .ok_or_else(|| invalid("truncated ZIP"))?,
    ]))
}

fn read_u32(data: &[u8], offset: usize) -> Result<u32, AppError> {
    Ok(u32::from_le_bytes([
        *data.get(offset).ok_or_else(|| invalid("truncated ZIP"))?,
        *data
            .get(offset + 1)
            .ok_or_else(|| invalid("truncated ZIP"))?,
        *data
            .get(offset + 2)
            .ok_or_else(|| invalid("truncated ZIP"))?,
        *data
            .get(offset + 3)
            .ok_or_else(|| invalid("truncated ZIP"))?,
    ]))
}

fn invalid(reason: &str) -> AppError {
    AppError::new(ErrorCode::PackInvalid).with("reason", reason)
}
