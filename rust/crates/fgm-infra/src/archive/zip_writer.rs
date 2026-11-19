/// Minimal ZIP writer (store method) for diagnostics bundles.
pub struct ZipWriter {
    data: Vec<u8>,
    entries: Vec<Entry>,
}

impl Default for ZipWriter {
    fn default() -> Self {
        Self::new()
    }
}

struct Entry {
    name: String,
    crc: u32,
    size: u32,
    offset: u32,
}

impl ZipWriter {
    pub fn new() -> Self {
        Self {
            data: Vec::new(),
            entries: Vec::new(),
        }
    }

    pub fn add_file(&mut self, name: &str, contents: &[u8]) {
        let name = name.replace('\\', "/");
        let crc = crc32(contents);
        let offset = self.data.len() as u32;
        let size = contents.len() as u32;
        push_u32(&mut self.data, 0x0403_4b50);
        push_u16(&mut self.data, 20);
        push_u16(&mut self.data, 0);
        push_u16(&mut self.data, 0);
        push_u16(&mut self.data, 0);
        push_u16(&mut self.data, 0);
        push_u32(&mut self.data, crc);
        push_u32(&mut self.data, size);
        push_u32(&mut self.data, size);
        push_u16(&mut self.data, name.len() as u16);
        push_u16(&mut self.data, 0);
        self.data.extend_from_slice(name.as_bytes());
        self.data.extend_from_slice(contents);
        self.entries.push(Entry {
            name,
            crc,
            size,
            offset,
        });
    }

    pub fn finish(self) -> Vec<u8> {
        let central_offset = self.data.len() as u32;
        let mut output = self.data;
        for entry in &self.entries {
            push_u32(&mut output, 0x0201_4b50);
            push_u16(&mut output, 20);
            push_u16(&mut output, 20);
            push_u16(&mut output, 0);
            push_u16(&mut output, 0);
            push_u16(&mut output, 0);
            push_u16(&mut output, 0);
            push_u32(&mut output, entry.crc);
            push_u32(&mut output, entry.size);
            push_u32(&mut output, entry.size);
            push_u16(&mut output, entry.name.len() as u16);
            push_u16(&mut output, 0);
            push_u16(&mut output, 0);
            push_u16(&mut output, 0);
            push_u16(&mut output, 0);
            push_u32(&mut output, 0);
            push_u32(&mut output, entry.offset);
            output.extend_from_slice(entry.name.as_bytes());
        }
        let central_size = output.len() as u32 - central_offset;
        push_u32(&mut output, 0x0605_4b50);
        push_u16(&mut output, 0);
        push_u16(&mut output, 0);
        push_u16(&mut output, self.entries.len() as u16);
        push_u16(&mut output, self.entries.len() as u16);
        push_u32(&mut output, central_size);
        push_u32(&mut output, central_offset);
        push_u16(&mut output, 0);
        output
    }
}

fn push_u16(output: &mut Vec<u8>, value: u16) {
    output.extend_from_slice(&value.to_le_bytes());
}

fn push_u32(output: &mut Vec<u8>, value: u32) {
    output.extend_from_slice(&value.to_le_bytes());
}

fn crc32(data: &[u8]) -> u32 {
    let mut crc = 0xFFFF_FFFF_u32;
    for byte in data {
        crc ^= u32::from(*byte);
        for _ in 0..8 {
            let mask = (crc & 1).wrapping_neg();
            crc = (crc >> 1) ^ (0xEDB8_8320 & mask);
        }
    }
    !crc
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn writes_a_zip_with_local_and_central_headers() {
        let mut writer = ZipWriter::new();
        writer.add_file("logs/fgm.log", b"hello");
        let bytes = writer.finish();
        assert_eq!(&bytes[..4], &[0x50, 0x4b, 0x03, 0x04]);
        let eocd = &bytes[bytes.len() - 22..];
        assert_eq!(&eocd[..4], &[0x50, 0x4b, 0x05, 0x06]);
        assert!(bytes.windows(5).any(|window| window == b"hello"));
    }
}
