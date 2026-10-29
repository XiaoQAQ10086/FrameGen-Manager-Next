namespace FrameGenManagerNext.Domain.Errors;

/// <summary>
/// Stable error identifiers. User-facing text belongs in the UI layer.
/// </summary>
public enum ErrorCode
{
    Unknown = 0,
    InvalidArgument,
    InvalidPath,
    UnsafePath,
    ProtectedPath,
    FileNotFound,
    FileLocked,
    AccessDenied,
    GpuUnsupported,
    GpuNotNeeded,
    DriverTooOld,
    NvApiUnavailable,
    NvApiCallFailed,
    GameRunning,
    AssetMissing,
    PackNotFound,
    PackInvalid,
    PackHashMismatch,
    PackRequiresNewerApp,
    ManifestMissing,
    ManifestCorrupt,
    BackupCorrupt,
    Conflict,
    AlreadyExists,
    NetworkError,
    DownloadFailed,
    InstallerFailed,
    ConfigCorrupt,
    OperationCancelled,
    InternalError,
}
