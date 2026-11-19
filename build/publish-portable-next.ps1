#!/usr/bin/env pwsh
[CmdletBinding()]
param(
    [string]$OutputDirectory = "",
    [string]$Flutter = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $root "artifacts/portable-next"
}
if ([string]::IsNullOrWhiteSpace($Flutter)) {
    $Flutter = if ($env:FGM_FLUTTER) { $env:FGM_FLUTTER } else { "flutter" }
}

$flutterCommand = Get-Command $Flutter -ErrorAction SilentlyContinue
if ($null -eq $flutterCommand) {
    throw "Flutter not found. Pass -Flutter or set FGM_FLUTTER."
}

$appName = "FrameGen-Manager-Next"
$stage = Join-Path $OutputDirectory $appName
$zipPath = Join-Path $OutputDirectory "$appName-portable.zip"

Write-Host "==> building Rust core" -ForegroundColor Cyan
Push-Location (Join-Path $root "rust")
try {
    & cargo build --release -p fgm-ffi -p fgm-cli
    if ($LASTEXITCODE -ne 0) { throw "cargo build failed" }
}
finally { Pop-Location }

Write-Host "==> building Flutter app" -ForegroundColor Cyan
Push-Location (Join-Path $root "ui")
try {
    & $Flutter build windows --release
    if ($LASTEXITCODE -ne 0) { throw "flutter build failed" }
}
finally { Pop-Location }

Write-Host "==> staging portable build" -ForegroundColor Cyan
if (Test-Path $OutputDirectory) {
    Remove-Item $OutputDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $stage -Force | Out-Null

$release = Join-Path $root "ui/build/windows/x64/runner/Release"
Copy-Item (Join-Path $release "*") $stage -Recurse -Force
Copy-Item (Join-Path $root "rust/target/release/fgm-next.exe") $stage -Force

$assetsPacks = Join-Path $root "assets/packs"
$packs = if (Test-Path $assetsPacks) { $assetsPacks } else { Join-Path $root "packs" }
if (Test-Path $packs) {
    Copy-Item $packs (Join-Path $stage "packs") -Recurse -Force
}

$licenses = Join-Path $root "licenses"
if (Test-Path $licenses) {
    Copy-Item $licenses (Join-Path $stage "licenses") -Recurse -Force
}

New-Item -ItemType File -Path (Join-Path $stage "portable.flag") -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $stage "data") -Force | Out-Null

Write-Host "==> creating zip" -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zipPath -CompressionLevel Optimal -Force

Write-Host ""
Write-Host "Portable folder: $stage" -ForegroundColor Green
Write-Host "Portable zip:    $zipPath" -ForegroundColor Green
