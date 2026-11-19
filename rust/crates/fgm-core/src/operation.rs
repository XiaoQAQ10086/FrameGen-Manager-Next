use fgm_domain::error::{AppError, ErrorCode};

#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub struct OperationId(pub u64);

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum OperationPhase {
    Planned,
    BackingUp,
    Writing,
    Verifying,
    Installed,
    RolledBack,
    Partial,
}

impl OperationPhase {
    /// Stable numeric id shared with the C ABI and persisted operation journals.
    pub const fn wire_id(self) -> u32 {
        match self {
            Self::Planned => 0,
            Self::BackingUp => 1,
            Self::Writing => 2,
            Self::Verifying => 3,
            Self::Installed => 4,
            Self::RolledBack => 5,
            Self::Partial => 6,
        }
    }

    /// Parses a wire id, rejecting values this build does not understand.
    pub fn from_wire(value: u32) -> Result<Self, AppError> {
        match value {
            0 => Ok(Self::Planned),
            1 => Ok(Self::BackingUp),
            2 => Ok(Self::Writing),
            3 => Ok(Self::Verifying),
            4 => Ok(Self::Installed),
            5 => Ok(Self::RolledBack),
            6 => Ok(Self::Partial),
            other => {
                Err(AppError::new(ErrorCode::InvalidArgument).with("phase", other.to_string()))
            }
        }
    }

    /// Stable snake_case key used in logs and structured progress events.
    pub const fn key(self) -> &'static str {
        match self {
            Self::Planned => "planned",
            Self::BackingUp => "backingUp",
            Self::Writing => "writing",
            Self::Verifying => "verifying",
            Self::Installed => "installed",
            Self::RolledBack => "rolledBack",
            Self::Partial => "partial",
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn operation_ids_are_value_objects() {
        assert_eq!(OperationId(7), OperationId(7));
        assert_ne!(OperationId(7), OperationId(8));
    }

    #[test]
    fn wire_ids_round_trip() {
        for phase in [
            OperationPhase::Planned,
            OperationPhase::BackingUp,
            OperationPhase::Writing,
            OperationPhase::Verifying,
            OperationPhase::Installed,
            OperationPhase::RolledBack,
            OperationPhase::Partial,
        ] {
            assert_eq!(OperationPhase::from_wire(phase.wire_id()).unwrap(), phase);
        }
    }

    #[test]
    fn unknown_wire_id_reports_invalid_argument() {
        let error = OperationPhase::from_wire(99).unwrap_err();

        assert_eq!(error.code, ErrorCode::InvalidArgument);
        assert_eq!(error.context[0], ("phase".to_string(), "99".to_string()));
    }
}
