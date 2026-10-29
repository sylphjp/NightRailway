param(
    [ValidateRange(60, 86400)]
    [int]$TimeoutSeconds = 600
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceSaver = Join-Path $projectRoot 'NightRailway.scr'
$installRoot = Join-Path $env:LOCALAPPDATA 'NightRailwayScreensaver'

if (-not (Test-Path -LiteralPath $sourceSaver)) {
    & (Join-Path $projectRoot 'build.ps1')
}

New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
Copy-Item -LiteralPath $sourceSaver -Destination $installRoot -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'index.html') -Destination $installRoot -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE') -Destination $installRoot -Force

$installedSaver = Join-Path $installRoot 'NightRailway.scr'
$desktopKey = 'HKCU:\Control Panel\Desktop'
Set-ItemProperty -Path $desktopKey -Name 'SCRNSAVE.EXE' -Value $installedSaver
Set-ItemProperty -Path $desktopKey -Name 'ScreenSaveActive' -Value '1'
Set-ItemProperty -Path $desktopKey -Name 'ScreenSaveTimeOut' -Value ([string]$TimeoutSeconds)

Write-Host "Installed: $installedSaver"
Write-Host "Timeout: $TimeoutSeconds seconds"
Write-Host 'Open Windows Screen Saver Settings to preview or change the wait time.'
