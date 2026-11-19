#!/usr/bin/env pwsh
[CmdletBinding()]
param(
    [string]$Flutter = "",
    [string]$InnoCompiler = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($InnoCompiler)) {
    $command = Get-Command iscc -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        $InnoCompiler = $command.Source
    } else {
        $programFilesX86 = [Environment]::GetFolderPath("ProgramFilesX86")
        $InnoCompiler = Join-Path $programFilesX86 "Inno Setup 6/ISCC.exe"
    }
}
if (-not (Test-Path $InnoCompiler)) {
    throw "ISCC.exe not found. Pass -InnoCompiler or install Inno Setup 6."
}

& (Join-Path $PSScriptRoot "publish-portable-next.ps1") -Flutter $Flutter
if ($LASTEXITCODE -ne 0) {
    throw "portable build failed"
}

$releases = Join-Path $root "artifacts/installer-next/releases"
if (Test-Path $releases) {
    Remove-Item $releases -Recurse -Force
}

Write-Host "==> compiling installer" -ForegroundColor Cyan
& $InnoCompiler (Join-Path $PSScriptRoot "installer-next.iss")
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compile failed"
}

$installer = Get-ChildItem $releases -Filter "*.exe" | Select-Object -First 1
if ($null -eq $installer) {
    throw "installer output not found in $releases"
}
Write-Host ""
Write-Host "Installer: $($installer.FullName)" -ForegroundColor Green
