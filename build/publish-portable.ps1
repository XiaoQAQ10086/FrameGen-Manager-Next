[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$Runtime = 'win-x64',

    [string]$OutputRoot = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $root 'artifacts\portable'
}

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

$props = [xml](Get-Content -LiteralPath (Join-Path $root 'Directory.Build.props'))
$versionNode = $props.SelectSingleNode('//Version')
if ($null -eq $versionNode -or [string]::IsNullOrWhiteSpace($versionNode.InnerText)) {
    throw 'Could not read Version from Directory.Build.props.'
}

$version = $versionNode.InnerText.Trim()

$appProject = Join-Path $root 'src\FrameGenManagerNext.App\FrameGenManagerNext.App.csproj'
$cliProject = Join-Path $root 'src\FrameGenManagerNext.Cli\FrameGenManagerNext.Cli.csproj'
$elevatedProject = Join-Path $root 'src\FrameGenManagerNext.Elevated\FrameGenManagerNext.Elevated.csproj'
$folderName = "FrameGen-Manager-Next-$version-$Runtime"
$destination = Join-Path $OutputRoot $folderName

New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null
if (Test-Path -LiteralPath $destination) {
    Remove-Item -LiteralPath $destination -Recurse -Force
}

New-Item -ItemType Directory -Path $destination -Force | Out-Null

function Invoke-Publish {
    param([string]$Project)

    Write-Host "> dotnet publish $Project" -ForegroundColor Cyan
    & $dotnet publish $Project `
        -c $Configuration `
        -r $Runtime `
        --self-contained true `
        -p:PublishSingleFile=false `
        -p:PublishTrimmed=false `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -o $destination

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }
}

Invoke-Publish -Project $appProject
Invoke-Publish -Project $cliProject
Invoke-Publish -Project $elevatedProject

Set-Content -LiteralPath (Join-Path $destination 'portable.flag') -Value 'FrameGen Manager-Next portable build' -Encoding ascii
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'portable\README.txt') -Destination (Join-Path $destination 'README.txt')

$packsSource = Join-Path $root 'packs'
if (Test-Path -LiteralPath $packsSource) {
    Copy-Item -LiteralPath $packsSource -Destination (Join-Path $destination 'packs') -Recurse -Force
}

$packIds = @('framegen.sm86', 'smooth-motion.rtx30', 'smooth-motion.rtx20', 'dlss5.reshade-gitc')
foreach ($packId in $packIds) {
    $packPayload = Join-Path $root "assets\packs\$packId"
    if (-not (Test-Path -LiteralPath $packPayload)) {
        throw "Pack payload not found for $packId. Run build\generate-packs.ps1 first."
    }

    $packTarget = Join-Path $destination "packs\$packId"
    New-Item -ItemType Directory -Path $packTarget -Force | Out-Null
    Copy-Item -Path (Join-Path $packPayload '*') -Destination $packTarget -Recurse -Force
}

Write-Host ''
Write-Host "Portable folder: $destination" -ForegroundColor Green
