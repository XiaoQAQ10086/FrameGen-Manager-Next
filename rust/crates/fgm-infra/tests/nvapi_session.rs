use fgm_domain::error::ErrorCode;
use fgm_infra::nvapi::NvApiSession;

#[test]
fn opens_a_driver_session_and_reads_the_base_profile() {
    let session = match NvApiSession::open() {
        Ok(session) => session,
        Err(error) if error.code == ErrorCode::NvApiUnavailable => return,
        Err(error) => panic!("NVAPI open failed: {:?} {:?}", error.code, error.context),
    };

    let base = session.base_profile().expect("base profile");
    let value = session.read_smooth_motion(base).expect("read setting");
    println!("base Smooth Motion setting: {value:?}");
}

#[test]
fn looks_up_profiles_for_the_current_executable() {
    let session = match NvApiSession::open() {
        Ok(session) => session,
        Err(error) if error.code == ErrorCode::NvApiUnavailable => return,
        Err(error) => panic!("NVAPI open failed: {:?} {:?}", error.code, error.context),
    };
    let current = std::env::current_exe().unwrap();

    let direct = session
        .find_profile_for_exe(&current)
        .expect("direct lookup");
    let all = session
        .find_profiles_for_exe(&current)
        .expect("enumerated lookup");
    println!("direct={direct:?}, enumerated={}", all.len());
}

#[test]
fn missing_profile_returns_none() {
    let session = match NvApiSession::open() {
        Ok(session) => session,
        Err(error) if error.code == ErrorCode::NvApiUnavailable => return,
        Err(error) => panic!("NVAPI open failed: {:?} {:?}", error.code, error.context),
    };

    let profile = session
        .find_profile("FrameGen Manager - definitely missing")
        .expect("find profile");

    assert!(profile.is_none());
}
