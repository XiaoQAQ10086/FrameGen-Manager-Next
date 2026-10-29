[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $root 'FrameGenManagerNext.slnx'

$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
if ($null -ne $dotnetCommand) {
    $dotnet = $dotnetCommand.Source
}
else {
    $dotnet = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Microsoft\dotnet\dotnet.exe'
    if (-not (Test-Path -LiteralPath $dotnet)) {
        throw 'dotnet was not found. Install the .NET 10 SDK and reopen the terminal.'
    }

    $env:DOTNET_ROOT = Split-Path -Parent $dotnet
    $env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
}

function Invoke-Dotnet {
    param([string[]]$Arguments)

    Write-Host "> dotnet $($Arguments -join ' ')" -ForegroundColor Cyan
    & $dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet command failed with exit code $LASTEXITCODE"
    }
}

Invoke-Dotnet -Arguments @('restore', $solution)
Invoke-Dotnet -Arguments @('format', $solution, '--verify-no-changes', '--no-restore')
Invoke-Dotnet -Arguments @('build', $solution, '-c', $Configuration, '--no-restore')
Invoke-Dotnet -Arguments @('test', $solution, '-c', $Configuration, '--no-build')

& (Join-Path $PSScriptRoot 'check-quality.ps1') -Root $root
if ($LASTEXITCODE -ne 0) {
    throw 'Quality checks failed.'
}

Write-Host 'Verification passed.' -ForegroundColor Green
