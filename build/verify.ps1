[CmdletBinding()]
param(
    # Kept for callers that still pass the legacy configuration argument.
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'verify-rust-electron.ps1')
if ($LASTEXITCODE -ne 0) {
    throw "Rust + Electron verification failed with exit code $LASTEXITCODE"
}
