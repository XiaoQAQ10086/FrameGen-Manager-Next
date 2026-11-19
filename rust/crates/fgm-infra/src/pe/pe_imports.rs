use std::collections::HashSet;
use std::fs::File;
use std::io::{Read, Seek, SeekFrom};
use std::path::Path;

use fgm_domain::error::{AppError, ErrorCode};

const MAX_DESCRIPTORS: usize = 4096;
const MAX_MODULE_NAME: usize = 512;
const MAX_SECTIONS: usize = 96;
const PE32_MAGIC: u16 = 0x10b;
const PE32_PLUS_MAGIC: u16 = 0x20b;

/// Minimal PE import-table reader for API detection.
///
/// It intentionally reads only what the library scanner needs and never maps
/// or executes the image.
pub struct PeImports;

impl PeImports {
    pub fn read_file(path: &Path) -> Result<Vec<String>, AppError> {
        let mut file = File::open(path).map_err(|error| io_error(path, error))?;
        Self::read(&mut file)
    }

    pub fn read<R: Read + Seek>(reader: &mut R) -> Result<Vec<String>, AppError> {
        let file_len = reader.seek(SeekFrom::End(0)).map_err(io_error_message)?;

        let mut dos = [0_u8; 64];
        read_at(reader, file_len, 0, &mut dos)?;
        if &dos[0..2] != b"MZ" {
            return Err(invalid("missing MZ header"));
        }

        let pe_offset = u32::from_le_bytes([dos[0x3c], dos[0x3d], dos[0x3e], dos[0x3f]]) as u64;
        let mut coff = [0_u8; 24];
        read_at(reader, file_len, pe_offset, &mut coff)?;
        if &coff[0..4] != b"PE\0\0" {
            return Err(invalid("missing PE signature"));
        }

        let section_count = u16::from_le_bytes([coff[6], coff[7]]) as usize;
        let optional_size = u16::from_le_bytes([coff[20], coff[21]]) as usize;
        if section_count == 0 || section_count > MAX_SECTIONS || optional_size == 0 {
            return Err(invalid("invalid COFF header"));
        }

        let optional_offset = pe_offset + 24;
        let mut optional = vec![0_u8; optional_size];
        read_at(reader, file_len, optional_offset, &mut optional)?;
        if optional.len() < 2 {
            return Err(invalid("optional header is truncated"));
        }

        let magic = u16::from_le_bytes([optional[0], optional[1]]);
        let directories_offset = match magic {
            PE32_MAGIC => 96,
            PE32_PLUS_MAGIC => 112,
            _ => return Err(invalid("unsupported optional header magic")),
        };

        let import_directory = directory(&optional, directories_offset, 1);
        let delay_directory = directory(&optional, directories_offset, 13);

        let sections = read_sections(
            reader,
            file_len,
            optional_offset + optional_size as u64,
            section_count,
        )?;

        let mut names = Vec::new();
        let mut seen = HashSet::new();

        if let Some((rva, size)) = import_directory {
            read_descriptors(
                reader, file_len, &sections, rva, size, 20, 12, &mut names, &mut seen,
            )?;
        }

        if let Some((rva, size)) = delay_directory {
            read_descriptors(
                reader, file_len, &sections, rva, size, 32, 4, &mut names, &mut seen,
            )?;
        }

        Ok(names)
    }
}

#[derive(Clone, Copy)]
struct Section {
    virtual_address: u32,
    size: u32,
    raw_pointer: u32,
}

impl Section {
    fn contains(self, rva: u32) -> Option<u64> {
        let delta = rva.checked_sub(self.virtual_address)?;
        if delta >= self.size {
            return None;
        }
        Some(u64::from(self.raw_pointer) + u64::from(delta))
    }
}

fn directory(optional: &[u8], offset: usize, index: usize) -> Option<(u32, u32)> {
    let start = offset.checked_add(index.checked_mul(8)?)?;
    let end = start.checked_add(8)?;
    let bytes = optional.get(start..end)?;
    let rva = u32::from_le_bytes([bytes[0], bytes[1], bytes[2], bytes[3]]);
    let size = u32::from_le_bytes([bytes[4], bytes[5], bytes[6], bytes[7]]);
    Some((rva, size))
}

fn read_sections<R: Read + Seek>(
    reader: &mut R,
    file_len: u64,
    offset: u64,
    count: usize,
) -> Result<Vec<Section>, AppError> {
    let mut sections = Vec::with_capacity(count);
    for index in 0..count {
        let mut header = [0_u8; 40];
        read_at(reader, file_len, offset + (index as u64) * 40, &mut header)?;

        let virtual_size = u32::from_le_bytes([header[8], header[9], header[10], header[11]]);
        let virtual_address = u32::from_le_bytes([header[12], header[13], header[14], header[15]]);
        let raw_size = u32::from_le_bytes([header[16], header[17], header[18], header[19]]);
        let raw_pointer = u32::from_le_bytes([header[20], header[21], header[22], header[23]]);

        sections.push(Section {
            virtual_address,
            size: virtual_size.max(raw_size),
            raw_pointer,
        });
    }
    Ok(sections)
}

#[allow(clippy::too_many_arguments)]
fn read_descriptors<R: Read + Seek>(
    reader: &mut R,
    file_len: u64,
    sections: &[Section],
    table_rva: u32,
    table_size: u32,
    descriptor_size: usize,
    name_field_offset: usize,
    names: &mut Vec<String>,
    seen: &mut HashSet<String>,
) -> Result<(), AppError> {
    if table_rva == 0 || table_size == 0 {
        return Ok(());
    }

    let Some(base) = rva_to_offset(sections, table_rva) else {
        return Ok(());
    };

    let mut descriptor = vec![0_u8; descriptor_size];
    for index in 0..MAX_DESCRIPTORS {
        let offset = base + (index as u64) * descriptor_size as u64;
        if read_at(reader, file_len, offset, &mut descriptor).is_err() {
            return Ok(());
        }
        if descriptor.iter().all(|byte| *byte == 0) {
            return Ok(());
        }

        let name_rva = u32::from_le_bytes([
            descriptor[name_field_offset],
            descriptor[name_field_offset + 1],
            descriptor[name_field_offset + 2],
            descriptor[name_field_offset + 3],
        ]);
        if name_rva == 0 {
            continue;
        }

        let Some(name_offset) = rva_to_offset(sections, name_rva) else {
            continue;
        };
        let Some(name) = read_ascii(reader, file_len, name_offset) else {
            continue;
        };

        let key = name.to_ascii_lowercase();
        if seen.insert(key) {
            names.push(name);
        }
    }

    Ok(())
}

fn rva_to_offset(sections: &[Section], rva: u32) -> Option<u64> {
    sections.iter().find_map(|section| section.contains(rva))
}

fn read_ascii<R: Read + Seek>(reader: &mut R, file_len: u64, offset: u64) -> Option<String> {
    if offset >= file_len {
        return None;
    }

    let available = usize::try_from(file_len - offset).ok()?;
    let length = available.min(MAX_MODULE_NAME);
    let mut bytes = vec![0_u8; length];
    read_at(reader, file_len, offset, &mut bytes).ok()?;

    let end = bytes
        .iter()
        .position(|byte| *byte == 0)
        .unwrap_or(bytes.len());
    if end == 0 {
        return None;
    }

    let name = String::from_utf8_lossy(&bytes[..end]).trim().to_string();
    (!name.is_empty()).then_some(name)
}

fn read_at<R: Read + Seek>(
    reader: &mut R,
    file_len: u64,
    offset: u64,
    buffer: &mut [u8],
) -> Result<(), AppError> {
    let end = offset
        .checked_add(buffer.len() as u64)
        .ok_or_else(|| invalid("offset overflow"))?;
    if end > file_len {
        return Err(invalid("read out of bounds"));
    }

    reader
        .seek(SeekFrom::Start(offset))
        .map_err(io_error_message)?;
    reader.read_exact(buffer).map_err(io_error_message)
}

fn invalid(reason: &str) -> AppError {
    AppError::new(ErrorCode::InvalidPe).with("reason", reason)
}

fn io_error(path: &Path, error: std::io::Error) -> AppError {
    let code = match error.kind() {
        std::io::ErrorKind::NotFound => ErrorCode::FileNotFound,
        std::io::ErrorKind::PermissionDenied => ErrorCode::AccessDenied,
        _ => ErrorCode::Internal,
    };
    AppError::new(code)
        .with("path", path.display().to_string())
        .with("reason", error.to_string())
}

fn io_error_message(error: std::io::Error) -> AppError {
    AppError::new(ErrorCode::Internal).with("reason", error.to_string())
}
