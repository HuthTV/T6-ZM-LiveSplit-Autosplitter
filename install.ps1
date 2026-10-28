# Copies build\Components into LiveSplit's Components folder. Close LiveSplit first.
param([string]$LiveSplitDir = (Join-Path $env:USERPROFILE 'Desktop\LS'))
$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot 'build\Components'
$target = Join-Path $LiveSplitDir 'Components'
if (-not (Test-Path (Join-Path $LiveSplitDir 'LiveSplit.exe'))) { throw "LiveSplit.exe not found in $LiveSplitDir" }
if (Get-Process LiveSplit -ErrorAction SilentlyContinue) { throw 'Close LiveSplit before installing.' }
$files = 'LiveSplit.T6ZM.dll', 'x86\t6zm_reader.dll', 'x64\t6zm_reader.dll'
foreach ($file in $files) {
    $from = Join-Path $source $file
    if (-not (Test-Path $from)) { throw "Missing $from; run build.bat first." }
    $to = Join-Path $target $file
    New-Item -ItemType Directory -Force (Split-Path $to) | Out-Null
    Copy-Item $from $to -Force
    '{0}  {1}' -f (Get-FileHash $to -Algorithm SHA256).Hash.Substring(0, 16), $to
}
