namespace FrameGenManagerNext.Core.Features.Framegen;

/// <summary>
/// One file recorded by the old Rust/Electron tool. Only read; never written back.
/// </summary>
public sealed record LegacyDeployedFile(string RelativePath, string DeployedSha256);
