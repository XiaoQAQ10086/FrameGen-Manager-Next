#!/usr/bin/env pwsh
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$rust = Join-Path $root 'rust'
$ui = Join-Path $root 'ui'

$flutter = if ($env:FGM_FLUTTER) { $env:FGM_FLUTTER } else { 'flutter' }
$flutterCommand = Get-Command $flutter
if ($null -eq $flutterCommand) {
    throw "Flutter not found. Set FGM_FLUTTER to the flutter executable."
}
$dart = Join-Path (Split-Path -Parent $flutterCommand.Source) 'dart.bat'
if (-not (Test-Path $dart)) {
    throw "dart.bat not found next to Flutter: $dart"
}

function Invoke-Step {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][scriptblock]$Action
    )

    Write-Host ""
    Write-Host "==> $Name" -ForegroundColor Cyan
    & $Action
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE"
    }
}

function Assert-Toolchains {
    $pinned = Get-Content (Join-Path $ui '.flutter-version')
    $channel = ($pinned | Where-Object { $_ -like 'channel=*' }) -replace '^channel=', ''
    $revision = ($pinned | Where-Object { $_ -like 'framework=*' }) -replace '^framework=', ''

    $info = & $flutter --version --machine | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0) {
        throw 'flutter --version failed'
    }
    if ($info.channel -ne $channel) {
        throw "Flutter channel is '$($info.channel)', expected '$channel'."
    }
    if ($info.frameworkRevision -ne $revision) {
        throw "Flutter revision is '$($info.frameworkRevision)', expected '$revision'. Update ui/.flutter-version after upgrading."
    }

    Write-Host "Flutter $($info.frameworkVersion) ($($info.frameworkRevision), $($info.channel))"
    & cargo --version
    if ($LASTEXITCODE -ne 0) {
        throw 'cargo is not available'
    }
}

function Assert-FileLimits {
    $roots = @(
        (Join-Path $rust 'crates'),
        (Join-Path $ui 'lib'),
        (Join-Path $ui 'test'),
        (Join-Path $ui 'tool')
    )
    $files = foreach ($path in $roots) {
        if (Test-Path $path) {
            Get-ChildItem $path -Recurse -File |
                Where-Object { $_.Extension -in '.rs', '.dart' }
        }
    }

    $warnings = 0
    foreach ($file in $files) {
        if ($file.Name -like 'app_localizations*') {
            continue
        }
        $lineCount = (Get-Content -LiteralPath $file.FullName).Count
        if ($lineCount -gt 500) {
            throw "$($file.FullName) has $lineCount lines (hard limit 500)."
        }
        if ($lineCount -gt 400) {
            Write-Warning "$($file.FullName) has $lineCount lines (soft limit 400)."
            $warnings++
        }
    }
    Write-Host "File limits OK ($warnings soft warnings)."
}

Push-Location $rust
try {
    Assert-Toolchains

    Invoke-Step 'rustfmt' { cargo fmt --all -- --check }
    Invoke-Step 'clippy' {
        cargo clippy --workspace --all-targets --all-features -- -D warnings
    }
    Invoke-Step 'rust tests' { cargo test --workspace }
    Invoke-Step 'rust release build' { cargo build --workspace --release }
}
finally {
    Pop-Location
}

Push-Location $ui
try {
    Invoke-Step 'flutter pub get' { & $flutter pub get }
    Invoke-Step 'flutter gen-l10n' { & $flutter gen-l10n }
    Invoke-Step 'flutter analyze' { & $flutter analyze }
    Invoke-Step 'flutter test' { & $flutter test }
    Invoke-Step 'ffi smoke' {
        $env:FGM_FFI_DLL = Join-Path $rust 'target/release/fgm_ffi.dll'
        & $dart run tool/ffi_smoke.dart
    }
    Invoke-Step 'flutter windows release' {
        & $flutter build windows --release
    }
}
finally {
    Pop-Location
}

Push-Location $root
try {
    Assert-FileLimits
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host 'Rust + Flutter gate passed.' -ForegroundColor Green
