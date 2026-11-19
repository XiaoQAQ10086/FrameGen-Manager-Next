use std::path::PathBuf;

pub struct EpicLocator;

impl EpicLocator {
    pub fn manifests_directory() -> Option<PathBuf> {
        let program_data = std::env::var_os("ProgramData")?;
        let directory = PathBuf::from(program_data)
            .join("Epic")
            .join("EpicGamesLauncher")
            .join("Data")
            .join("Manifests");
        directory.is_dir().then_some(directory)
    }
}
