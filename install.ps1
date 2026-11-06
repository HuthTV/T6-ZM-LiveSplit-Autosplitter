# Copies the component (one file, the native reader is embedded) into LiveSplit's Components folder. Close LiveSplit first.
param([string]$LiveSplitDir = (Join-Path $env:USERPROFILE 'Desktop\LS'))
$ErrorActionPreference = 'Stop'
$from = Join-Path $PSScriptRoot 'build\Components\LiveSplit.T6ZM.dll'
$target = Join-Path $LiveSplitDir 'Components'
if (-not (Test-Path (Join-Path $LiveSplitDir 'LiveSplit.exe'))) { throw "LiveSplit.exe not found in $LiveSplitDir" }
if (Get-Process LiveSplit -ErrorAction SilentlyContinue) { throw 'Close LiveSplit before installing.' }
if (-not (Test-Path $from)) { throw "Missing $from; run build.bat first." }
$to = Join-Path $target 'LiveSplit.T6ZM.dll'
Copy-Item $from $to -Force
'{0}  {1}' -f (Get-FileHash $to -Algorithm SHA256).Hash.Substring(0, 16), $to
# Earlier builds installed the reader as separate files in x86\ and x64\. The component no longer uses them.
foreach ($arch in 'x86', 'x64') {
    $path = Join-Path $target "$arch\t6zm_reader.dll"
    if (Test-Path $path) { Remove-Item $path; "removed $path" }
    $folder = Join-Path $target $arch
    if ((Test-Path $folder) -and -not (Get-ChildItem $folder -Force)) { Remove-Item $folder; "removed empty $folder" }
}
