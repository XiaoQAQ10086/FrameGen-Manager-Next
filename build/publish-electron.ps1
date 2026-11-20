#!/usr/bin/env pwsh
[CmdletBinding()]
param(
    [string]$InnoCompiler = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$app = Join-Path $root "ui-electron"
$portableRoot = Join-Path $root "artifacts/portable-electron"
$portable = Join-Path $portableRoot "FrameGen-Manager-Next"

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

Push-Location $app
try {
    if (-not (Test-Path "node_modules")) {
        & npm ci --no-audit --no-fund
        if ($LASTEXITCODE -ne 0) { throw "npm ci failed" }
    }
    & npm run package
    if ($LASTEXITCODE -ne 0) { throw "electron package failed" }
}
finally { Pop-Location }

if (Test-Path $portableRoot) { Remove-Item $portableRoot -Recurse -Force }
New-Item -ItemType Directory -Path $portableRoot -Force | Out-Null
Copy-Item (Join-Path $app "dist/FrameGen-Manager-Next") $portable -Recurse -Force

Write-Host "==> creating portable zip" -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $portable "*") -DestinationPath (Join-Path $portableRoot "FrameGen-Manager-Next-portable.zip") -CompressionLevel Optimal -Force

$releases = Join-Path $root "artifacts/installer-electron/releases"
if (Test-Path $releases) { Remove-Item $releases -Recurse -Force }

Write-Host "==> compiling installer" -ForegroundColor Cyan
& $InnoCompiler (Join-Path $PSScriptRoot "installer-electron.iss")
if ($LASTEXITCODE -ne 0) { throw "Inno Setup compile failed" }

$installer = Get-ChildItem $releases -Filter "*.exe" | Select-Object -First 1
if ($null -eq $installer) { throw "installer output not found in $releases" }
Write-Host ""
Write-Host "Portable folder: $portable" -ForegroundColor Green
Write-Host "Portable zip:    $(Join-Path $portableRoot 'FrameGen-Manager-Next-portable.zip')" -ForegroundColor Green
Write-Host "Installer:       $($installer.FullName)" -ForegroundColor Green
