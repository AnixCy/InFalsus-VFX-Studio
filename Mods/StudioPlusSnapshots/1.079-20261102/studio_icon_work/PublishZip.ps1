$ErrorActionPreference='Stop'
$source=Join-Path $PSScriptRoot 'release\InFalsusStudioPlus_1.06_Windows_x64_build1.0.90.zip'
$destination='E:\RhythmGame\Infstudio\InFalsusStudioPlus_1.06_Windows_x64_build1.0.90.zip'
$backup='E:\RhythmGame\Infstudio\release-backup-icon-outline-'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff')
if (!(Test-Path -LiteralPath $source -PathType Leaf)) { throw 'Prepared ZIP missing' }
New-Item -ItemType Directory -Path $backup | Out-Null
if (Test-Path -LiteralPath $destination) { Copy-Item -LiteralPath $destination -Destination $backup }
$tmp=$destination+'.icon-update.tmp'
if (!(Test-Path -LiteralPath $tmp)) { Copy-Item -LiteralPath $source -Destination $tmp }
if ((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $tmp).Hash) { throw 'ZIP checksum mismatch' }
Move-Item -LiteralPath $tmp -Destination $destination -Force
if (Test-Path -LiteralPath ($destination+'.sha256')) { Move-Item -LiteralPath ($destination+'.sha256') -Destination $backup }
Write-Output "ZIP: $destination"
Write-Output "Backup: $backup"
