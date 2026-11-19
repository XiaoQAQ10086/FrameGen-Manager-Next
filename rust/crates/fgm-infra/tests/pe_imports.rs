use std::io::Cursor;

use fgm_domain::error::ErrorCode;
use fgm_infra::pe::PeImports;

#[test]
fn reads_import_table_names() {
    let bytes = build_pe("KERNEL32.dll", Table::Import);

    let imports = PeImports::read(&mut Cursor::new(bytes)).unwrap();

    assert_eq!(imports, vec!["KERNEL32.dll"]);
}

#[test]
fn reads_delay_import_table_names() {
    let bytes = build_pe("d3d12.dll", Table::Delay);

    let imports = PeImports::read(&mut Cursor::new(bytes)).unwrap();

    assert_eq!(imports, vec!["d3d12.dll"]);
}

#[test]
fn pack_without_imports_returns_empty() {
    let mut bytes = build_pe("KERNEL32.dll", Table::Import);
    put_u32(&mut bytes, 0x58 + 96 + 8, 0);
    put_u32(&mut bytes, 0x58 + 96 + 12, 0);

    let imports = PeImports::read(&mut Cursor::new(bytes)).unwrap();

    assert!(imports.is_empty());
}

#[test]
fn rejects_non_pe_input() {
    let error = PeImports::read(&mut Cursor::new(b"not a pe".to_vec())).unwrap_err();
    assert_eq!(error.code, ErrorCode::InvalidPe);
}

#[test]
fn rejects_truncated_input() {
    let mut bytes = build_pe("KERNEL32.dll", Table::Import);
    bytes.truncate(0x100);

    let error = PeImports::read(&mut Cursor::new(bytes)).unwrap_err();
    assert_eq!(error.code, ErrorCode::InvalidPe);
}

#[derive(Clone, Copy)]
enum Table {
    Import,
    Delay,
}

fn build_pe(import_name: &str, table: Table) -> Vec<u8> {
    let mut bytes = vec![0_u8; 0x400];

    bytes[0..2].copy_from_slice(b"MZ");
    put_u32(&mut bytes, 0x3c, 0x40);
    bytes[0x40..0x44].copy_from_slice(b"PE\0\0");

    put_u16(&mut bytes, 0x44, 0x14c);
    put_u16(&mut bytes, 0x46, 1);
    put_u16(&mut bytes, 0x54, 0xe0);

    put_u16(&mut bytes, 0x58, 0x10b);

    let directory_offset = 0x58 + 96;
    let (index, descriptor_size, name_field) = match table {
        Table::Import => (1, 20, 12),
        Table::Delay => (13, 32, 4),
    };
    put_u32(&mut bytes, directory_offset + index * 8, 0x1000);
    put_u32(
        &mut bytes,
        directory_offset + index * 8 + 4,
        descriptor_size,
    );

    bytes[0x138..0x140].copy_from_slice(b".idata\0\0");
    put_u32(&mut bytes, 0x138 + 8, 0x200);
    put_u32(&mut bytes, 0x138 + 12, 0x1000);
    put_u32(&mut bytes, 0x138 + 16, 0x200);
    put_u32(&mut bytes, 0x138 + 20, 0x200);

    put_u32(&mut bytes, 0x200 + name_field, 0x1050);
    bytes[0x250..0x250 + import_name.len()].copy_from_slice(import_name.as_bytes());

    bytes
}

fn put_u16(bytes: &mut [u8], offset: usize, value: u16) {
    bytes[offset..offset + 2].copy_from_slice(&value.to_le_bytes());
}

fn put_u32(bytes: &mut [u8], offset: usize, value: u32) {
    bytes[offset..offset + 4].copy_from_slice(&value.to_le_bytes());
}
