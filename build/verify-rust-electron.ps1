#!/usr/bin/env pwsh
[CmdletBinding()]
param(
    [switch]$SkipPackage
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

function Invoke-Step {
    param([string]$Name, [scriptblock]$Action)
    Write-Host "==> $Name" -ForegroundColor Cyan
    & $Action
    if ($LASTEXITCODE -ne 0) { throw "$Name failed with exit code $LASTEXITCODE" }
}

Push-Location (Join-Path $root "rust")
try {
    Invoke-Step "cargo fmt" { cargo fmt --all -- --check }
    Invoke-Step "cargo clippy" { cargo clippy --workspace --all-targets --all-features -- -D warnings }
    Invoke-Step "cargo test" { cargo test --workspace }
    Invoke-Step "cargo build fgm-node" { cargo build --release -p fgm-node }
}
finally { Pop-Location }

Push-Location (Join-Path $root "ui-electron")
try {
    if (-not (Test-Path "node_modules")) {
        Invoke-Step "npm ci" { npm ci --no-audit --no-fund }
    }
    Invoke-Step "npm run check" { npm run check }
    Invoke-Step "npm run build:native" { npm run build:native }
    Invoke-Step "npm test" { npm test }
    if (-not $SkipPackage) {
        Invoke-Step "npm run package" { npm run package }
    }
}
finally { Pop-Location }

Write-Host ""
Write-Host "File limits OK (Electron tree)" -ForegroundColor Green
Write-Host "Rust + Electron gate passed." -ForegroundColor Green
