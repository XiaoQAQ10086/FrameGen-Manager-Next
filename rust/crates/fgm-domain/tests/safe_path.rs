use fgm_domain::error::ErrorCode;
use fgm_domain::safety::SafePath;

#[test]
fn accepts_relative_paths_and_normalizes_separators() {
    assert_eq!(
        SafePath::normalize_relative("runtime/nvngx.dll").unwrap(),
        "runtime/nvngx.dll"
    );
    assert_eq!(
        SafePath::normalize_relative(r"runtime\nvngx.dll").unwrap(),
        "runtime/nvngx.dll"
    );
    assert_eq!(
        SafePath::normalize_relative("游戏/版本.dll").unwrap(),
        "游戏/版本.dll"
    );
}

#[test]
fn rejects_empty_and_whitespace() {
    for value in ["", "   ", "\t"] {
        assert_eq!(
            SafePath::normalize_relative(value).unwrap_err().code,
            ErrorCode::InvalidPath,
            "{value:?}"
        );
    }
}

#[test]
fn rejects_leading_or_trailing_whitespace() {
    for value in [" foo", "foo ", "foo\t"] {
        assert_eq!(
            SafePath::normalize_relative(value).unwrap_err().code,
            ErrorCode::InvalidPath,
            "{value:?}"
        );
    }
}

#[test]
fn rejects_rooted_and_unc_paths() {
    for value in ["/abs", "\\\\server\\share", "//server/share"] {
        assert_eq!(
            SafePath::normalize_relative(value).unwrap_err().code,
            ErrorCode::UnsafePath,
            "{value:?}"
        );
    }
}

#[test]
fn rejects_drive_letters_and_ads() {
    for value in ["C:foo", "C:/foo", "file.txt:stream"] {
        assert_eq!(
            SafePath::normalize_relative(value).unwrap_err().code,
            ErrorCode::UnsafePath,
            "{value:?}"
        );
    }
}

#[test]
fn rejects_invalid_windows_characters() {
    for value in ["a|b", "a<b", "a>b", "a\"b", "a?b", "a*b"] {
        assert_eq!(
            SafePath::normalize_relative(value).unwrap_err().code,
            ErrorCode::InvalidPath,
            "{value:?}"
        );
    }
}

#[test]
fn rejects_control_characters() {
    assert_eq!(
        SafePath::normalize_relative("line\nbreak")
            .unwrap_err()
            .code,
        ErrorCode::InvalidPath
    );
}

#[test]
fn rejects_empty_dot_and_parent_segments() {
    for value in ["a//b", "a/./b", "a/../b", ".", "..", "a/.."] {
        assert_eq!(
            SafePath::normalize_relative(value).unwrap_err().code,
            ErrorCode::UnsafePath,
            "{value:?}"
        );
    }
}

#[test]
fn rejects_overlong_segments() {
    let segment = "a".repeat(SafePath::MAX_SEGMENT_LENGTH + 1);
    assert_eq!(
        SafePath::normalize_relative(&segment).unwrap_err().code,
        ErrorCode::InvalidPath
    );
}

#[test]
fn rejects_unsafe_trailing_dots_and_spaces() {
    for value in ["foo.", "foo ", "foo/ bar", "foo/bar."] {
        assert_eq!(
            SafePath::normalize_relative(value).unwrap_err().code,
            ErrorCode::InvalidPath,
            "{value:?}"
        );
    }
}

#[test]
fn rejects_reserved_device_names() {
    for value in [
        "CON", "con.txt", "COM1.log", "LPT9", "CONIN$", "CONOUT$", "nul",
    ] {
        assert_eq!(
            SafePath::normalize_relative(value).unwrap_err().code,
            ErrorCode::InvalidPath,
            "{value:?}"
        );
    }
}

#[test]
fn rejects_overlong_total_path() {
    let value = ["a".repeat(80), "b".repeat(80), "c".repeat(80)].join("/");
    assert!(value.len() > SafePath::MAX_RELATIVE_PATH_LENGTH);
    assert_eq!(
        SafePath::normalize_relative(&value).unwrap_err().code,
        ErrorCode::InvalidPath
    );
}
