use std::path::{Path, PathBuf};

use fgm_domain::error::{AppError, ErrorCode};

use crate::nvapi::{NvApiProfile, NvApiSession};

use super::sm_profile_record::SmProfileRecord;
use super::sm_profile_store::SmProfileStore;
use super::sm_profile_target::SmProfileTarget;
use super::smooth_motion_change::SmoothMotionChange;
use super::smooth_motion_state::SmoothMotionState;

const PROFILE_PREFIX: &str = "FrameGen Manager - ";

/// Reads and writes the Smooth Motion driver setting for a game executable.
pub struct SmoothMotionService;

impl SmoothMotionService {
    pub fn inspect(executable: &Path) -> SmoothMotionState {
        let session = match NvApiSession::open() {
            Ok(session) => session,
            Err(error) => {
                return SmoothMotionState {
                    detail: Some(format!("{:?}", error.code)),
                    ..SmoothMotionState::default()
                };
            }
        };
        inspect_with(&session, executable)
    }

    pub fn inspect_many(executables: &[std::path::PathBuf]) -> Vec<SmoothMotionState> {
        let session = match NvApiSession::open() {
            Ok(session) => session,
            Err(error) => {
                return executables
                    .iter()
                    .map(|_| SmoothMotionState {
                        detail: Some(format!("{:?}", error.code)),
                        ..SmoothMotionState::default()
                    })
                    .collect();
            }
        };
        executables
            .iter()
            .map(|executable| inspect_with(&session, executable))
            .collect()
    }

    pub fn set(
        game: &str,
        executable: &Path,
        enabled: bool,
        data_root: &Path,
    ) -> Result<SmoothMotionChange, AppError> {
        let session = NvApiSession::open()?;
        let targets = ensure_targets(&session, game, executable)?;
        if targets.is_empty() {
            return Err(AppError::new(ErrorCode::NvApiCallFailed)
                .with("reason", "no usable driver profile could be created"));
        }

        let target_directory = parent(executable)?;
        let store = SmProfileStore::new(data_root.join("backups").join("sm-profiles"));
        let existing = store.load(&target_directory)?;
        let current_names = sorted_names(&targets);
        let recorded_names = existing.as_ref().map(recorded_names);

        if existing.is_none() || recorded_names.as_ref() != Some(&current_names) {
            let mut recorded = Vec::with_capacity(targets.len());
            for (profile, name) in &targets {
                let old = session.read_smooth_motion(*profile)?;
                recorded.push(SmProfileTarget {
                    profile: name.clone(),
                    ours: name.starts_with(PROFILE_PREFIX),
                    had: old.is_some(),
                    value: old,
                });
            }
            store.save(&SmProfileRecord::new(
                game,
                executable.display().to_string(),
                target_directory.display().to_string(),
                recorded,
            ))?;
        }

        let value = u32::from(enabled);
        for (profile, _) in &targets {
            session.set_dword(*profile, crate::nvapi::SMOOTH_MOTION_SETTING_ID, value)?;
        }
        session.save()?;
        drop(session);

        verify_linked(executable, &current_names, value)?;
        Ok(SmoothMotionChange {
            ok: true,
            enabled,
            profile_names: current_names,
            detail: String::new(),
        })
    }

    pub fn restore(
        game: &str,
        executable: &Path,
        data_root: &Path,
    ) -> Result<SmoothMotionChange, AppError> {
        let target_directory = parent(executable)?;
        let store = SmProfileStore::new(data_root.join("backups").join("sm-profiles"));
        let Some(record) = store.load(&target_directory)? else {
            return Ok(SmoothMotionChange {
                ok: true,
                enabled: false,
                profile_names: Vec::new(),
                detail: "no Smooth Motion record".to_string(),
            });
        };

        let session = NvApiSession::open()?;
        for target in &record.targets {
            let Some(profile) = session.find_profile(&target.profile)? else {
                continue;
            };
            if target.had {
                session.set_dword(
                    profile,
                    crate::nvapi::SMOOTH_MOTION_SETTING_ID,
                    target.value.unwrap_or(0),
                )?;
            } else {
                session.delete_setting(profile, crate::nvapi::SMOOTH_MOTION_SETTING_ID)?;
            }
        }
        session.save()?;
        drop(session);

        verify_restored(executable, &record)?;
        store.delete(&target_directory)?;
        Ok(SmoothMotionChange {
            ok: true,
            enabled: record
                .targets
                .first()
                .and_then(|target| target.value)
                .is_some_and(|value| value != 0),
            profile_names: record
                .profile_names()
                .into_iter()
                .map(str::to_string)
                .collect(),
            detail: format!("restored {game}"),
        })
    }
}

fn inspect_with(session: &NvApiSession, executable: &Path) -> SmoothMotionState {
    match targets_for(session, executable) {
        Ok(targets) if !targets.is_empty() => {
            let enabled = session
                .read_smooth_motion(targets[0].0)
                .ok()
                .flatten()
                .map(|value| value != 0);
            SmoothMotionState {
                enabled,
                profile_names: targets.iter().map(|target| target.1.clone()).collect(),
                ours: targets[0].1.starts_with(PROFILE_PREFIX),
                detail: None,
            }
        }
        Ok(_) => SmoothMotionState {
            detail: Some("no driver profile is associated with this executable".to_string()),
            ..SmoothMotionState::default()
        },
        Err(error) => SmoothMotionState {
            detail: Some(first_reason(&error).to_string()),
            ..SmoothMotionState::default()
        },
    }
}

fn targets_for(
    session: &NvApiSession,
    executable: &Path,
) -> Result<Vec<(NvApiProfile, String)>, AppError> {
    let mut profiles = session.find_profiles_for_exe(executable)?;
    if profiles.is_empty()
        && let Some(profile) = session.find_profile_for_exe(executable)?
    {
        profiles.push(profile);
    }

    let mut targets = Vec::new();
    let mut seen = std::collections::HashSet::new();
    for profile in profiles {
        if session.is_base_profile(profile) {
            continue;
        }
        let Some(name) = session.profile_name(profile) else {
            continue;
        };
        if seen.insert(name.to_ascii_lowercase()) {
            targets.push((profile, name));
        }
    }
    Ok(targets)
}

fn ensure_targets(
    session: &NvApiSession,
    game: &str,
    executable: &Path,
) -> Result<Vec<(NvApiProfile, String)>, AppError> {
    let existing = targets_for(session, executable)?;
    if !existing.is_empty() {
        return Ok(existing);
    }

    let stem = executable.file_stem().map_or_else(
        || game.to_string(),
        |value| value.to_string_lossy().into_owned(),
    );
    let name = format!("{PROFILE_PREFIX}{stem}");
    let profile = session.create_profile(&name)?;
    session.create_application(profile, executable)?;

    let created = targets_for(session, executable)?;
    if created.is_empty() {
        Ok(vec![(profile, name)])
    } else {
        Ok(created)
    }
}

fn verify_linked(executable: &Path, names: &[String], expected: u32) -> Result<(), AppError> {
    let session = NvApiSession::open()?;
    for name in names {
        let Some(profile) = session.find_profile(name)? else {
            return Err(AppError::new(ErrorCode::NvApiCallFailed)
                .with("reason", format!("profile {name} disappeared")));
        };
        if session.read_smooth_motion(profile)? != Some(expected) {
            return Err(AppError::new(ErrorCode::NvApiCallFailed)
                .with("reason", format!("profile {name} read-back mismatch")));
        }
    }

    let linked = targets_for(&session, executable)?;
    if linked.is_empty()
        || linked.iter().all(|(profile, _)| {
            session.read_smooth_motion(*profile).ok().flatten() != Some(expected)
        })
    {
        return Err(AppError::new(ErrorCode::NvApiCallFailed).with(
            "reason",
            "the executable is not linked to the expected profile",
        ));
    }
    Ok(())
}

fn verify_restored(executable: &Path, record: &SmProfileRecord) -> Result<(), AppError> {
    let session = NvApiSession::open()?;
    for target in &record.targets {
        let Some(profile) = session.find_profile(&target.profile)? else {
            continue;
        };
        let value = session.read_smooth_motion(profile)?;
        if target.had {
            if value != target.value {
                return Err(AppError::new(ErrorCode::NvApiCallFailed).with(
                    "reason",
                    format!("profile {} was not restored", target.profile),
                ));
            }
        } else if value.is_some() {
            return Err(AppError::new(ErrorCode::NvApiCallFailed).with(
                "reason",
                format!("profile {} still has a setting", target.profile),
            ));
        }
    }
    let _ = executable;
    Ok(())
}

fn sorted_names(targets: &[(NvApiProfile, String)]) -> Vec<String> {
    let mut names: Vec<String> = targets.iter().map(|target| target.1.clone()).collect();
    names.sort();
    names
}

fn recorded_names(record: &SmProfileRecord) -> Vec<String> {
    let mut names: Vec<String> = record
        .profile_names()
        .into_iter()
        .map(str::to_string)
        .collect();
    names.sort();
    names
}

fn parent(executable: &Path) -> Result<PathBuf, AppError> {
    executable.parent().map(Path::to_path_buf).ok_or_else(|| {
        AppError::new(ErrorCode::InvalidPath).with("path", executable.display().to_string())
    })
}

fn first_reason(error: &AppError) -> &str {
    error
        .context
        .iter()
        .find(|(key, _)| key == "reason")
        .map_or("NVAPI call failed", |(_, value)| value.as_str())
}
