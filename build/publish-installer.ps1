[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$Runtime = 'win-x64',

    [string]$OutputRoot = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$env:MSBUILDDISABLENODEREUSE = '1'
$env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'

$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $root 'artifacts\installer'
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
$staging = Join-Path $OutputRoot "staging-$version"
$releases = Join-Path $OutputRoot 'releases'
$appProject = Join-Path $root 'src\FrameGenManagerNext.App\FrameGenManagerNext.App.csproj'
$cliProject = Join-Path $root 'src\FrameGenManagerNext.Cli\FrameGenManagerNext.Cli.csproj'
$elevatedProject = Join-Path $root 'src\FrameGenManagerNext.Elevated\FrameGenManagerNext.Elevated.csproj'
$icon = Join-Path $root 'src\FrameGenManagerNext.App\Assets\app.ico'

if (Test-Path -LiteralPath $staging) {
    Remove-Item -LiteralPath $staging -Recurse -Force
}

New-Item -ItemType Directory -Path $staging -Force | Out-Null
New-Item -ItemType Directory -Path $releases -Force | Out-Null

function Invoke-Publish {
    param([string]$Project)

    Write-Host "> dotnet publish $Project" -ForegroundColor Cyan
    & $dotnet publish $Project `
        -c $Configuration `
        -r $Runtime `
        --self-contained true `
        -p:PublishSingleFile=false `
        -p:PublishTrimmed=false `
        -p:PublishReadyToRun=true `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -o $staging

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }
}

Invoke-Publish -Project $appProject
Invoke-Publish -Project $cliProject
Invoke-Publish -Project $elevatedProject

$packsSource = Join-Path $root 'packs'
if (Test-Path -LiteralPath $packsSource) {
    Copy-Item -LiteralPath $packsSource -Destination (Join-Path $staging 'packs') -Recurse -Force
}

$packIds = @('framegen.sm86', 'smooth-motion.rtx30', 'smooth-motion.rtx20', 'dlss5.reshade-gitc')
foreach ($packId in $packIds) {
    $packPayload = Join-Path $root "assets\packs\$packId"
    if (-not (Test-Path -LiteralPath $packPayload)) {
        throw "Pack payload not found for $packId. Run build\generate-packs.ps1 first."
    }

    $packTarget = Join-Path $staging "packs\$packId"
    New-Item -ItemType Directory -Path $packTarget -Force | Out-Null
    Copy-Item -Path (Join-Path $packPayload '*') -Destination $packTarget -Recurse -Force
}

# The installed app must use LocalAppData, not portable mode.
Remove-Item -LiteralPath (Join-Path $staging 'portable.flag') -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath (Join-Path $staging 'data') -Recurse -Force -ErrorAction SilentlyContinue

Write-Host '> dotnet tool restore' -ForegroundColor Cyan
& $dotnet tool restore
if ($LASTEXITCODE -ne 0) {
    throw "dotnet tool restore failed with exit code $LASTEXITCODE"
}

Write-Host '> dotnet vpk pack' -ForegroundColor Cyan
& $dotnet vpk pack `
    --packId FrameGenManagerNext `
    --packVersion $version `
    --packDir $staging `
    --runtime $Runtime `
    --mainExe FrameGenManager-Next.exe `
    --packTitle 'FrameGen Manager-Next' `
    --packAuthors 'FrameGen Manager-Next' `
    --icon $icon `
    --outputDir $releases `
    --channel win `
    --instLocation PerUser `
    --shortcuts 'Desktop,StartMenuRoot'

if ($LASTEXITCODE -ne 0) {
    throw "vpk pack failed with exit code $LASTEXITCODE"
}

Write-Host ''
Write-Host "Installer output: $releases" -ForegroundColor Green
Get-ChildItem -LiteralPath $releases | Select-Object Name, Length | Format-Table -AutoSize
