[CmdletBinding()]
param(
    [string]$OldRoot = 'D:\AI Work\FrameGen Manager\dlssg-manager',
    [string]$RepoRoot = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = Split-Path -Parent $PSScriptRoot
}

$fgRoot = Join-Path $OldRoot 'apps\electron\bundled-assets'
$smRoot = Join-Path $OldRoot 'bundled-sm'
$dlss5Root = Join-Path $OldRoot 'apps\electron\bundled-dlss5'

foreach ($path in @($fgRoot, $smRoot, $dlss5Root)) {
    if (-not (Test-Path -LiteralPath $path -PathType Container)) {
        throw "Asset root not found: $path"
    }
}

$manifestRoot = Join-Path $RepoRoot 'packs'
$payloadRoot = Join-Path $RepoRoot 'assets\packs'
New-Item -ItemType Directory -Path $manifestRoot -Force | Out-Null
New-Item -ItemType Directory -Path $payloadRoot -Force | Out-Null

function ConvertTo-PackPath {
    param([string]$Path)
    return $Path.Replace('\', '/')
}

function New-PackWorkspace {
    param([string]$Id)

    $manifestDir = Join-Path $manifestRoot $Id
    $payloadDir = Join-Path $payloadRoot $Id

    if (Test-Path -LiteralPath $manifestDir) {
        Remove-Item -LiteralPath $manifestDir -Recurse -Force
    }

    if (Test-Path -LiteralPath $payloadDir) {
        Remove-Item -LiteralPath $payloadDir -Recurse -Force
    }

    New-Item -ItemType Directory -Path $manifestDir -Force | Out-Null
    New-Item -ItemType Directory -Path $payloadDir -Force | Out-Null

    return [pscustomobject]@{
        ManifestDir = $manifestDir
        PayloadDir = $payloadDir
    }
}

function Get-FileFingerprint {
    param([string]$Path)

    $hash = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    $size = (Get-Item -LiteralPath $Path).Length
    return [pscustomobject]@{
        Sha256 = $hash
        Size = [long]$size
    }
}

function Copy-PayloadOnly {
    param(
        [string]$SourceRoot,
        [string]$SourceRelative,
        [string]$PayloadDir
    )

    $sourcePath = Join-Path $SourceRoot $SourceRelative.Replace('/', '\')
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
        throw "Missing asset: $sourcePath"
    }

    $payloadPath = Join-Path $PayloadDir $SourceRelative.Replace('/', '\')
    $parent = Split-Path -Parent $payloadPath
    if ($parent) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }

    Copy-Item -LiteralPath $sourcePath -Destination $payloadPath -Force
    return Get-FileFingerprint -Path $payloadPath
}

function Copy-PackFile {
    param(
        [string]$SourceRoot,
        [string]$SourceRelative,
        [string]$TargetRelative,
        [string]$Role,
        [string]$PayloadDir,
        [System.Collections.Generic.List[object]]$Files,
        [bool]$ProxySlot = $false,
        [bool]$Required = $true
    )

    $fingerprint = Copy-PayloadOnly -SourceRoot $SourceRoot -SourceRelative $SourceRelative -PayloadDir $PayloadDir
    $Files.Add([ordered]@{
        source = ConvertTo-PackPath $SourceRelative
        target = ConvertTo-PackPath $TargetRelative
        role = $Role
        required = $Required
        proxySlot = $ProxySlot
        sha256 = $fingerprint.Sha256
        size = $fingerprint.Size
    })
}

function Add-PackTree {
    param(
        [string]$SourceRoot,
        [string]$SourceRelativeRoot,
        [scriptblock]$TargetResolver,
        [scriptblock]$RoleResolver,
        [string]$PayloadDir,
        [System.Collections.Generic.List[object]]$Files
    )

    $absoluteRoot = Join-Path $SourceRoot $SourceRelativeRoot.Replace('/', '\')
    if (-not (Test-Path -LiteralPath $absoluteRoot -PathType Container)) {
        throw "Asset tree not found: $absoluteRoot"
    }

    $rootFull = [IO.Path]::GetFullPath($absoluteRoot).TrimEnd('\')
    $rootPrefix = $rootFull + '\'
    $treeFiles = Get-ChildItem -LiteralPath $absoluteRoot -Recurse -File | Sort-Object FullName

    foreach ($file in $treeFiles) {
        $full = [IO.Path]::GetFullPath($file.FullName)
        if (-not $full.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Unexpected file outside asset tree: $full"
        }

        $relativeWithin = $full.Substring($rootPrefix.Length).Replace('\', '/')
        $sourceRelative = (ConvertTo-PackPath $SourceRelativeRoot).TrimEnd('/') + '/' + $relativeWithin
        $targetRelative = & $TargetResolver $sourceRelative
        $role = & $RoleResolver $sourceRelative

        Copy-PackFile `
            -SourceRoot $SourceRoot `
            -SourceRelative $sourceRelative `
            -TargetRelative $targetRelative `
            -Role $role `
            -PayloadDir $PayloadDir `
            -Files $Files
    }
}

function New-IniBlock {
    param(
        [string]$SourceRoot,
        [string]$TemplateSourceRelative,
        [string]$TargetRelative,
        [string]$PayloadDir,
        [object[]]$StockValues
    )

    $fingerprint = Copy-PayloadOnly -SourceRoot $SourceRoot -SourceRelative $TemplateSourceRelative -PayloadDir $PayloadDir

    $stock = @()
    foreach ($item in $StockValues) {
        $stock += [ordered]@{
            section = $item.Section
            key = $item.Key
            value = $item.Value
        }
    }

    return [ordered]@{
        target = ConvertTo-PackPath $TargetRelative
        template = ConvertTo-PackPath $TemplateSourceRelative
        mode = 'merge'
        templateSha256 = $fingerprint.Sha256
        templateSize = $fingerprint.Size
        stockValues = $stock
    }
}

function Write-PackManifest {
    param(
        [string]$Id,
        [string]$Version,
        [string]$DisplayName,
        [string[]]$GpuRoutes,
        [System.Collections.Generic.List[object]]$Files,
        [string[]]$OwnedDirectories = @(),
        [object]$Ini,
        [object[]]$Steps,
        [string]$ManifestDir,
        [string]$PayloadDir
    )

    $pack = [ordered]@{
        schemaVersion = 1
        id = $Id
        version = $Version
        displayName = $DisplayName
        minAppVersion = '2.0.0'
        gpuRoutes = @($GpuRoutes)
        files = @($Files)
        steps = @($Steps)
        trust = 'bundled'
    }

    if ($null -ne $OwnedDirectories -and $OwnedDirectories.Count -gt 0) {
        $pack['ownedDirectories'] = @($OwnedDirectories)
    }

    if ($null -ne $Ini) {
        $pack['ini'] = $Ini
    }

    $json = $pack | ConvertTo-Json -Depth 20
    $manifestPath = Join-Path $ManifestDir 'pack.json'
    [IO.File]::WriteAllText($manifestPath, $json, [Text.UTF8Encoding]::new($false))
    Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $PayloadDir 'pack.json') -Force

    $totalBytes = 0
    foreach ($file in $Files) {
        $totalBytes += [long]$file['size']
    }

    Write-Host ("  {0} {1}: {2} files, {3:N1} MB" -f $Id, $Version, $Files.Count, ($totalBytes / 1MB))
}

$frameGenStock = @(
    [pscustomobject]@{ Section = 'General'; Key = 'Enabled'; Value = '1' },
    [pscustomobject]@{ Section = 'FrameGeneration'; Key = 'Engine'; Value = 'DLSS' },
    [pscustomobject]@{ Section = 'FrameGeneration'; Key = 'MaxGeneratedFrames'; Value = '3' },
    [pscustomobject]@{ Section = 'DLSSG'; Key = 'Optimized'; Value = '1' },
    [pscustomobject]@{ Section = 'DLSSG'; Key = 'Preset'; Value = 'A' },
    [pscustomobject]@{ Section = 'XeSSFG'; Key = 'FlowResolution'; Value = 'Middle' },
    [pscustomobject]@{ Section = 'XeSSFG'; Key = 'UseHudless'; Value = 'Auto' },
    [pscustomobject]@{ Section = 'Logging'; Key = 'Level'; Value = '1' },
    [pscustomobject]@{ Section = 'Logging'; Key = 'Directory'; Value = 'dlssg_sm86\logs' },
    [pscustomobject]@{ Section = 'Runtime'; Key = 'Mode'; Value = 'Bundled' },
    [pscustomobject]@{ Section = 'SuperResolution'; Key = 'Mode'; Value = 'Off' }
)

Write-Host 'Generating FrameGen pack...' -ForegroundColor Cyan
$workspace = New-PackWorkspace -Id 'framegen.sm86'
$files = [System.Collections.Generic.List[object]]::new()

foreach ($proxy in @('version.dll', 'winmm.dll', 'dbghelp.dll', 'dinput8.dll', 'dxgi.dll', 'd3d12.dll')) {
    Copy-PackFile `
        -SourceRoot $fgRoot `
        -SourceRelative $proxy `
        -TargetRelative $proxy `
        -Role 'proxy' `
        -ProxySlot $true `
        -PayloadDir $workspace.PayloadDir `
        -Files $files
}

Add-PackTree `
    -SourceRoot $fgRoot `
    -SourceRelativeRoot 'dlssg_sm86' `
    -TargetResolver { param($rel) $rel } `
    -RoleResolver { param($rel) if ($rel -like 'dlssg_sm86/sr/*') { 'sr' } else { 'runtime' } } `
    -PayloadDir $workspace.PayloadDir `
    -Files $files

$ini = New-IniBlock `
    -SourceRoot $fgRoot `
    -TemplateSourceRelative 'dlssg_sm86.ini' `
    -TargetRelative 'dlssg_sm86.ini' `
    -PayloadDir $workspace.PayloadDir `
    -StockValues $frameGenStock

Write-PackManifest `
    -Id 'framegen.sm86' `
    -Version '0.4.1' `
    -DisplayName 'FrameGen SM86 (assets 0.4.1, runtime 310.9.16)' `
    -GpuRoutes @('Sm86', 'Sm75') `
    -OwnedDirectories @('dlssg_sm86') `
    -Files $files `
    -Ini $ini `
    -Steps @([ordered]@{ type = 'copyFiles' }, [ordered]@{ type = 'writeIni' }) `
    -ManifestDir $workspace.ManifestDir `
    -PayloadDir $workspace.PayloadDir

Write-Host 'Generating Smooth Motion RTX 30 pack...' -ForegroundColor Cyan
$workspace = New-PackWorkspace -Id 'smooth-motion.rtx30'
$files = [System.Collections.Generic.List[object]]::new()
Copy-PackFile `
    -SourceRoot $smRoot `
    -SourceRelative 'rtx30/version.dll' `
    -TargetRelative 'version.dll' `
    -Role 'runtime' `
    -PayloadDir $workspace.PayloadDir `
    -Files $files

Write-PackManifest `
    -Id 'smooth-motion.rtx30' `
    -Version '1.0.0' `
    -DisplayName 'Smooth Motion RTX 30 (SmoothMotion-next, sm8792+)' `
    -GpuRoutes @('Sm86') `
    -Files $files `
    -Ini $null `
    -Steps @([ordered]@{ type = 'copyFiles' }) `
    -ManifestDir $workspace.ManifestDir `
    -PayloadDir $workspace.PayloadDir

Write-Host 'Generating Smooth Motion RTX 20 pack...' -ForegroundColor Cyan
$workspace = New-PackWorkspace -Id 'smooth-motion.rtx20'
$files = [System.Collections.Generic.List[object]]::new()
foreach ($item in @(
    [pscustomobject]@{ Source = 'rtx20/version.dll'; Target = 'version.dll'; Role = 'runtime' },
    [pscustomobject]@{ Source = 'rtx20/normal.asi'; Target = 'normal.asi'; Role = 'runtime' },
    [pscustomobject]@{ Source = 'rtx20/runtime-manifest.json'; Target = 'runtime-manifest.json'; Role = 'config' }
)) {
    Copy-PackFile `
        -SourceRoot $smRoot `
        -SourceRelative $item.Source `
        -TargetRelative $item.Target `
        -Role $item.Role `
        -PayloadDir $workspace.PayloadDir `
        -Files $files
}

Write-PackManifest `
    -Id 'smooth-motion.rtx20' `
    -Version '2.8.5' `
    -DisplayName 'Smooth Motion RTX 20 (2.8.5 experimental)' `
    -GpuRoutes @('Sm75') `
    -Files $files `
    -Ini $null `
    -Steps @([ordered]@{ type = 'copyFiles' }) `
    -ManifestDir $workspace.ManifestDir `
    -PayloadDir $workspace.PayloadDir

Write-Host 'Generating DLSS5 pack...' -ForegroundColor Cyan
$workspace = New-PackWorkspace -Id 'dlss5.reshade-gitc'
$files = [System.Collections.Generic.List[object]]::new()
foreach ($item in @(
    [pscustomobject]@{ Source = 'gitc-uplift.addon64'; Target = 'gitc-uplift.addon64'; Role = 'addon' },
    [pscustomobject]@{ Source = 'gitc-uplift.addon32'; Target = 'gitc-uplift.addon32'; Role = 'addon' },
    [pscustomobject]@{ Source = 'gitc-uplift-helper64.exe'; Target = 'gitc-uplift-helper64.exe'; Role = 'other' },
    [pscustomobject]@{ Source = 'Uplift.fx'; Target = 'reshade-shaders/Shaders/GITC-Uplift/Uplift.fx'; Role = 'shader' },
    [pscustomobject]@{ Source = 'UpliftMask.fx'; Target = 'reshade-shaders/Shaders/GITC-Uplift/UpliftMask.fx'; Role = 'shader' },
    [pscustomobject]@{ Source = 'nvngx_dlssnr.dll'; Target = 'GITC-Uplift/nvngx_dlssnr.dll'; Role = 'model' }
)) {
    Copy-PackFile `
        -SourceRoot $dlss5Root `
        -SourceRelative $item.Source `
        -TargetRelative $item.Target `
        -Role $item.Role `
        -PayloadDir $workspace.PayloadDir `
        -Files $files
}

Add-PackTree `
    -SourceRoot $dlss5Root `
    -SourceRelativeRoot 'effects' `
    -TargetResolver { param($rel) 'reshade-shaders/' + $rel.Substring('effects/'.Length) } `
    -RoleResolver {
        param($rel)
        if ($rel -like 'effects/Shaders/*') { 'shader' }
        elseif ($rel -like 'effects/Textures/*') { 'texture' }
        else { 'other' }
    } `
    -PayloadDir $workspace.PayloadDir `
    -Files $files

$installerRelative = 'ReShade_Setup_6.8.0_Addon.exe'
$installer = Copy-PayloadOnly `
    -SourceRoot $dlss5Root `
    -SourceRelative $installerRelative `
    -PayloadDir $workspace.PayloadDir

Write-PackManifest `
    -Id 'dlss5.reshade-gitc' `
    -Version '6.8.0' `
    -DisplayName 'DLSS5 ReShade 6.8.0 + GITC Uplift' `
    -GpuRoutes @('Sm86', 'Sm75') `
    -Files $files `
    -Ini $null `
    -Steps @(
        [ordered]@{ type = 'copyFiles' },
        [ordered]@{
            type = 'runInstaller'
            arguments = [ordered]@{
                source = $installerRelative
                sha256 = $installer.Sha256
                arguments = '--headless --api {api}'
            }
        }
    ) `
    -ManifestDir $workspace.ManifestDir `
    -PayloadDir $workspace.PayloadDir

Write-Host ''
Write-Host "Pack manifests: $manifestRoot" -ForegroundColor Green
Write-Host "Pack payloads:  $payloadRoot" -ForegroundColor Green
Write-Host 'Validate a pack with: fgm-next pack validate <pack-dir>' -ForegroundColor Green
