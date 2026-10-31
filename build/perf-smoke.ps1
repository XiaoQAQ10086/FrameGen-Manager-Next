[CmdletBinding()]
param(
    [string]$Exe = '',
    [string]$DataDir = '',
    [int]$MaxStartupMs = 2500,
    [double]$MaxHostMB = 220,
    [double]$MaxWebView2MB = 450
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($Exe)) {
    $Exe = Get-ChildItem -Path (Join-Path $root 'artifacts\portable') -Filter 'FrameGenManager-Next.exe' -Recurse |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1 -ExpandProperty FullName
}

if ([string]::IsNullOrWhiteSpace($DataDir)) {
    $DataDir = Join-Path (Split-Path -Parent $Exe) 'data'
}

if (-not (Test-Path -LiteralPath $Exe)) {
    throw "Executable not found: $Exe"
}

$env:FGM_DATA_DIR = $DataDir
$process = Start-Process -FilePath $Exe -PassThru
Start-Sleep -Seconds 12

$log = Get-ChildItem -LiteralPath (Join-Path $DataDir 'logs') -Filter 'app-*.log' |
    Sort-Object LastWriteTime |
    Select-Object -Last 1

$startupMs = $null
if ($null -ne $log) {
    $match = Select-String -LiteralPath $log.FullName -Pattern 'startup\.uiReady elapsed=(\d+)ms' |
        Select-Object -Last 1
    if ($null -ne $match) {
        $startupMs = [int]$match.Matches[0].Groups[1].Value
    }
}

$hostProcess = Get-Process -Name 'FrameGenManager-Next' -ErrorAction Stop
$hostMB = [math]::Round($hostProcess.WorkingSet64 / 1MB, 1)

$all = Get-CimInstance Win32_Process
$children = @{}
foreach ($item in $all) {
    if (-not $children.ContainsKey($item.ParentProcessId)) {
        $children[$item.ParentProcessId] = @()
    }
    $children[$item.ParentProcessId] += $item
}

$descendants = New-Object System.Collections.Generic.List[object]
$queue = New-Object System.Collections.Generic.Queue[uint32]
$queue.Enqueue([uint32]$hostProcess.Id)
while ($queue.Count -gt 0) {
    $parent = $queue.Dequeue()
    if (-not $children.ContainsKey($parent)) { continue }
    foreach ($child in $children[$parent]) {
        $descendants.Add($child)
        $queue.Enqueue([uint32]$child.ProcessId)
    }
}

$webviewMB = 0.0
foreach ($item in $descendants) {
    if ($item.Name -ne 'msedgewebview2.exe') { continue }
    $processInfo = Get-Process -Id $item.ProcessId -ErrorAction SilentlyContinue
    if ($null -ne $processInfo) {
        $webviewMB += $processInfo.WorkingSet64 / 1MB
    }
}
$webviewMB = [math]::Round($webviewMB, 1)

Stop-Process -Name 'FrameGenManager-Next' -Force -ErrorAction SilentlyContinue

Write-Host "startup.uiReady=$startupMs ms"
Write-Host "hostWorkingSet=$hostMB MB"
Write-Host "webviewWorkingSet=$webviewMB MB"

$failed = $false
if ($null -ne $startupMs -and $startupMs -gt $MaxStartupMs) {
    Write-Warning "Startup $startupMs ms exceeds $MaxStartupMs ms"
    $failed = $true
}
if ($hostMB -gt $MaxHostMB) {
    Write-Warning "Host memory $hostMB MB exceeds $MaxHostMB MB"
    $failed = $true
}
if ($webviewMB -gt $MaxWebView2MB) {
    Write-Warning "WebView2 memory $webviewMB MB exceeds $MaxWebView2MB MB"
    $failed = $true
}

if ($failed) {
    exit 1
}

Write-Host 'Performance smoke passed.'
exit 0
