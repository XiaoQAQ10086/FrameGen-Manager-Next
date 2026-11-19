use std::io::Read;

use flate2::read::DeflateDecoder;

const EOCD: [u8; 4] = [0x50, 0x4b, 0x05, 0x06];
const CENTRAL: [u8; 4] = [0x50, 0x4b, 0x01, 0x02];
const LOCAL: [u8; 4] = [0x50, 0x4b, 0x03, 0x04];
const MAX_EOCD_SEARCH: usize = 65_557;

/// Extracts one entry from a ZIP appended to an executable.
pub(crate) fn extract_entry(data: &[u8], entry_name: &str) -> Option<Vec<u8>> {
    let eocd = find_eocd(data)?;
    let count = read_u16(data, eocd + 10)? as usize;
    let central_size = read_u32(data, eocd + 12)? as usize;
    let central_offset = read_u32(data, eocd + 16)? as usize;
    if count == 0 || count == u16::MAX as usize || central_offset == u32::MAX as usize {
        return None;
    }

    let computed_start = eocd.checked_sub(central_size + central_offset)?;
    for zip_start in [computed_start, 0] {
        if let Some(bytes) = extract_with_start(data, zip_start, entry_name, count, central_offset)
        {
            return Some(bytes);
        }
    }
    None
}

fn extract_with_start(
    data: &[u8],
    zip_start: usize,
    entry_name: &str,
    count: usize,
    central_offset: usize,
) -> Option<Vec<u8>> {
    let mut position = zip_start.checked_add(central_offset)?;
    for _ in 0..count {
        if data.get(position..position + 4)? != CENTRAL {
            return None;
        }
        let method = read_u16(data, position + 10)?;
        let compressed_size = read_u32(data, position + 20)? as usize;
        let uncompressed_size = read_u32(data, position + 24)? as usize;
        let name_length = read_u16(data, position + 28)? as usize;
        let extra_length = read_u16(data, position + 30)? as usize;
        let comment_length = read_u16(data, position + 32)? as usize;
        let local_offset = read_u32(data, position + 42)? as usize;
        let name = data.get(position + 46..position + 46 + name_length)?;
        let matches = std::str::from_utf8(name)
            .ok()
            .is_some_and(|name| basename(name).eq_ignore_ascii_case(entry_name));

        if matches {
            return read_entry(
                data,
                zip_start.checked_add(local_offset)?,
                method,
                compressed_size,
                uncompressed_size,
            );
        }

        position = position.checked_add(46 + name_length + extra_length + comment_length)?;
    }
    None
}

fn read_entry(
    data: &[u8],
    local: usize,
    method: u16,
    compressed_size: usize,
    uncompressed_size: usize,
) -> Option<Vec<u8>> {
    if data.get(local..local + 4)? != LOCAL {
        return None;
    }
    let name_length = read_u16(data, local + 26)? as usize;
    let extra_length = read_u16(data, local + 28)? as usize;
    let start = local.checked_add(30 + name_length + extra_length)?;
    let compressed = data.get(start..start + compressed_size)?;

    let bytes = match method {
        0 => compressed.to_vec(),
        8 => {
            let mut output = Vec::with_capacity(uncompressed_size);
            DeflateDecoder::new(compressed)
                .read_to_end(&mut output)
                .ok()?;
            output
        }
        _ => return None,
    };

    (bytes.len() == uncompressed_size).then_some(bytes)
}

fn find_eocd(data: &[u8]) -> Option<usize> {
    let start = data.len().saturating_sub(MAX_EOCD_SEARCH);
    (start..data.len().saturating_sub(3))
        .rev()
        .find(|index| data.get(*index..*index + 4) == Some(&EOCD))
}

fn basename(name: &str) -> &str {
    name.rsplit(['\\', '/']).next().unwrap_or(name)
}

fn read_u16(data: &[u8], offset: usize) -> Option<u16> {
    Some(u16::from_le_bytes([
        *data.get(offset)?,
        *data.get(offset + 1)?,
    ]))
}

fn read_u32(data: &[u8], offset: usize) -> Option<u32> {
    Some(u32::from_le_bytes([
        *data.get(offset)?,
        *data.get(offset + 1)?,
        *data.get(offset + 2)?,
        *data.get(offset + 3)?,
    ]))
}

#[cfg(test)]
mod tests {
    use super::*;
    use flate2::Compression;
    use flate2::write::DeflateEncoder;
    use std::io::Write;

    #[test]
    fn extracts_a_stored_entry_from_an_appended_zip() {
        let data = build_zip("ReShade64.dll", b"module", false);

        assert_eq!(
            extract_entry(&data, "ReShade64.dll").as_deref(),
            Some(b"module".as_slice())
        );
        assert!(extract_entry(&data, "ReShade32.dll").is_none());
    }

    #[test]
    fn extracts_a_deflated_entry() {
        let data = build_zip("ReShade32.dll", b"deflated module", true);

        assert_eq!(
            extract_entry(&data, "ReShade32.dll").as_deref(),
            Some(b"deflated module".as_slice())
        );
    }

    fn build_zip(name: &str, content: &[u8], deflate: bool) -> Vec<u8> {
        let payload = if deflate {
            let mut encoder = DeflateEncoder::new(Vec::new(), Compression::default());
            encoder.write_all(content).unwrap();
            encoder.finish().unwrap()
        } else {
            content.to_vec()
        };
        let method = if deflate { 8_u16 } else { 0_u16 };
        let mut data = vec![0_u8; 512];
        let local_offset = 0_u32;

        data.extend_from_slice(&LOCAL);
        data.extend_from_slice(&20_u16.to_le_bytes());
        data.extend_from_slice(&0_u16.to_le_bytes());
        data.extend_from_slice(&method.to_le_bytes());
        data.extend_from_slice(&0_u16.to_le_bytes());
        data.extend_from_slice(&0_u16.to_le_bytes());
        data.extend_from_slice(&0_u32.to_le_bytes());
        data.extend_from_slice(&(payload.len() as u32).to_le_bytes());
        data.extend_from_slice(&(content.len() as u32).to_le_bytes());
        data.extend_from_slice(&(name.len() as u16).to_le_bytes());
        data.extend_from_slice(&0_u16.to_le_bytes());
        data.extend_from_slice(name.as_bytes());
        data.extend_from_slice(&payload);

        let central_offset = (data.len() - 512) as u32;
        data.extend_from_slice(&CENTRAL);
        data.extend_from_slice(&20_u16.to_le_bytes());
        data.extend_from_slice(&20_u16.to_le_bytes());
        data.extend_from_slice(&0_u16.to_le_bytes());
        data.extend_from_slice(&method.to_le_bytes());
        data.extend_from_slice(&0_u16.to_le_bytes());
        data.extend_from_slice(&0_u16.to_le_bytes());
        data.extend_from_slice(&0_u32.to_le_bytes());
        data.extend_from_slice(&(payload.len() as u32).to_le_bytes());
        data.extend_from_slice(&(content.len() as u32).to_le_bytes());
        data.extend_from_slice(&(name.len() as u16).to_le_bytes());
        data.extend_from_slice(&0_u16.to_le_bytes());
        data.extend_from_slice(&0_u16.to_le_bytes());
        data.extend_from_slice(&0_u16.to_le_bytes());
        data.extend_from_slice(&0_u16.to_le_bytes());
        data.extend_from_slice(&0_u32.to_le_bytes());
        data.extend_from_slice(&local_offset.to_le_bytes());
        data.extend_from_slice(name.as_bytes());

        let central_size = (data.len() - 512 - central_offset as usize) as u32;
        data.extend_from_slice(&EOCD);
        data.extend_from_slice(&0_u16.to_le_bytes());
        data.extend_from_slice(&0_u16.to_le_bytes());
        data.extend_from_slice(&1_u16.to_le_bytes());
        data.extend_from_slice(&1_u16.to_le_bytes());
        data.extend_from_slice(&central_size.to_le_bytes());
        data.extend_from_slice(&central_offset.to_le_bytes());
        data.extend_from_slice(&0_u16.to_le_bytes());
        data
    }
}
