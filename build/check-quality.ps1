[CmdletBinding()]
param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$violations = [System.Collections.Generic.List[string]]::new()

function Get-SourceFiles {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return @()
    }

    return Get-ChildItem -LiteralPath $Path -Recurse -Filter '*.cs' -File |
        Where-Object {
            $_.FullName -notmatch '\\(bin|obj)\\' -and
            $_.Name -notin @('AssemblyInfo.cs') -and
            $_.Name -notlike '*.g.cs'
        }
}

$sourceFiles = @(
    Get-SourceFiles -Path (Join-Path $Root 'src')
    Get-SourceFiles -Path (Join-Path $Root 'tests')
)

foreach ($file in $sourceFiles) {
    $lines = @(Get-Content -LiteralPath $file.FullName)
    if ($lines.Count -gt 500) {
        $violations.Add("FILE-SIZE: $($file.FullName) has $($lines.Count) lines (hard limit 500)")
    }
}

$strictFiles = @(
    Get-SourceFiles -Path (Join-Path $Root 'src\FrameGenNext.Domain')
    Get-SourceFiles -Path (Join-Path $Root 'src\FrameGenNext.Core')
)

foreach ($file in $strictFiles) {
    $content = Get-Content -LiteralPath $file.FullName -Raw

    if ($content -match '\b(DllImport|LibraryImport|DllImportAttribute)\b') {
        $violations.Add("P-INVOKE: $($file.FullName) must live in Infrastructure")
    }

    if ($content -match 'PresentationFramework|PresentationCore|WindowsBase|System\.Windows\.Forms') {
        $violations.Add("UI-DEPENDENCY: $($file.FullName) must not reference UI frameworks")
    }

    if ($content -match '\bFrameGenNext\.Infrastructure\b') {
        $violations.Add("LAYERING: $($file.FullName) must not reference Infrastructure")
    }

    if ($content -match '"[^"\r\n]*[\u4e00-\u9fff][^"\r\n]*"') {
        $violations.Add("LOCALIZED-STRING: $($file.FullName) contains user-facing Chinese text")
    }
}

foreach ($file in $sourceFiles) {
    $content = Get-Content -LiteralPath $file.FullName -Raw

    if ($content -match 'D:\\AI Work|C:\\Users\\[A-Za-z0-9_.-]+') {
        $violations.Add("HARDCODED-PATH: $($file.FullName)")
    }
}

if ($violations.Count -gt 0) {
    Write-Host 'Quality checks failed:' -ForegroundColor Red
    $violations | Sort-Object -Unique | ForEach-Object { Write-Host "  $_" }
    exit 1
}

Write-Host "Quality checks passed: $($sourceFiles.Count) C# files checked." -ForegroundColor Green
