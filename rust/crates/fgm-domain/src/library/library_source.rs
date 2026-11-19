use serde::{Deserialize, Serialize};

#[derive(Clone, Copy, Debug, Deserialize, Serialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub enum LibrarySource {
    Manual,
    Steam,
    Epic,
    WeGame,
    Xbox,
    Loose,
}
