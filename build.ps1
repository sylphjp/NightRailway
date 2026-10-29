$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$source = Join-Path $projectRoot 'NightRailwayScreensaver.cs'
$output = Join-Path $projectRoot 'NightRailway.scr'

if (-not (Test-Path -LiteralPath $compiler)) {
    throw "C# compiler not found: $compiler"
}

& $compiler /nologo /target:winexe /platform:anycpu /optimize+ `
    /reference:System.Windows.Forms.dll `
    /reference:System.Drawing.dll `
    "/out:$output" `
    $source

if ($LASTEXITCODE -ne 0) {
    throw "Compiler exited with code $LASTEXITCODE"
}

Write-Host "Built: $output"
