use std::cmp::Ordering;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub(crate) struct SemVer {
    major: u64,
    minor: u64,
    patch: u64,
}

pub(crate) fn parse(value: &str) -> Option<SemVer> {
    if value.is_empty() || value.trim() != value {
        return None;
    }

    let (without_build, build) = match value.split_once('+') {
        Some((core, build)) => (core, Some(build)),
        None => (value, None),
    };

    if let Some(build) = build
        && !is_identifier(build)
    {
        return None;
    }

    let (core, prerelease) = match without_build.split_once('-') {
        Some((core, prerelease)) => (core, Some(prerelease)),
        None => (without_build, None),
    };

    if let Some(prerelease) = prerelease
        && !is_identifier(prerelease)
    {
        return None;
    }

    let mut parts = core.split('.');
    let major = parse_number(parts.next()?)?;
    let minor = parse_number(parts.next()?)?;
    let patch = parse_number(parts.next()?)?;
    if parts.next().is_some() {
        return None;
    }

    Some(SemVer {
        major,
        minor,
        patch,
    })
}

pub(crate) fn compare_core(left: &str, right: &str) -> Option<Ordering> {
    let left = parse(left)?;
    let right = parse(right)?;
    Some((left.major, left.minor, left.patch).cmp(&(right.major, right.minor, right.patch)))
}

fn parse_number(value: &str) -> Option<u64> {
    if value.is_empty() || !value.bytes().all(|byte| byte.is_ascii_digit()) {
        return None;
    }
    value.parse().ok()
}

fn is_identifier(value: &str) -> bool {
    !value.is_empty()
        && value
            .bytes()
            .all(|byte| byte.is_ascii_alphanumeric() || matches!(byte, b'.' | b'-'))
}
