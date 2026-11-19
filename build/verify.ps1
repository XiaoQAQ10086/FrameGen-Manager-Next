[CmdletBinding()]
param(
    # Kept for callers that still pass the legacy configuration argument.
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'verify-rust-flutter.ps1')
if ($LASTEXITCODE -ne 0) {
    throw "Rust + Flutter verification failed with exit code $LASTEXITCODE"
}
