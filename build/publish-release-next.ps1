#!/usr/bin/env pwsh
[CmdletBinding()]
param(
    [string]$Tag = "",
    [string]$Notes = "",
    [string]$InnoCompiler = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

& (Join-Path $PSScriptRoot "publish-electron.ps1") -InnoCompiler $InnoCompiler
if ($LASTEXITCODE -ne 0) {
    throw "installer build failed"
}

$cargo = Get-Content (Join-Path $root "rust/Cargo.toml") -Raw
$versionMatch = [regex]::Match($cargo, '(?m)^version = "([^"]+)"')
if (-not $versionMatch.Success) {
    throw "workspace version not found in rust/Cargo.toml"
}
$version = $versionMatch.Groups[1].Value
if ([string]::IsNullOrWhiteSpace($Tag)) {
    $Tag = "v$version"
}

$installer = Get-ChildItem (Join-Path $root "artifacts/installer-electron/releases") -Filter "*.exe" |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1
if ($null -eq $installer) {
    throw "installer output not found"
}

$hash = (Get-FileHash -Path $installer.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
$repository = "XiaoQAQ10086/FrameGen-Manager-Next"
$download = "https://github.com/$repository/releases/download/$Tag/$($installer.Name)"

$manifest = [ordered]@{
    version = $version
    notes = $Notes
    pageUrl = "https://github.com/$repository/releases/tag/$Tag"
    installer = [ordered]@{
        name = $installer.Name
        size = $installer.Length
        sha256 = $hash
        urls = @($download)
    }
}

$outputDir = Join-Path $root "artifacts/release-next"
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
$outputFile = Join-Path $outputDir "update.json"
$manifest | ConvertTo-Json -Depth 5 | Set-Content -Path $outputFile -Encoding utf8

Write-Host ""
Write-Host "Installer: $($installer.FullName)" -ForegroundColor Green
Write-Host "Manifest:  $outputFile" -ForegroundColor Green
Write-Host "SHA-256:   $hash"
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Create GitHub release $Tag and upload $($installer.Name)."
Write-Host "  2. Commit update.json to main so installed clients see version $version."
