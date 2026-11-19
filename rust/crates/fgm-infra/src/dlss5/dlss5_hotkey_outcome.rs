/// Result of a hotkey write request.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum HotkeyOutcome {
    /// `ReShade.ini` was updated immediately.
    Applied,
    /// The game is running or the file is locked; retried from the queue.
    Queued,
}
