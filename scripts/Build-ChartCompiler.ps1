param([Parameter(Mandatory=$true)][string]$PythonEmbedArchive,[Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$taskExpected='91D828C2DA3A029B41699E918674A0CB379C02CF20DAB9C501306885F837402A'
if((Get-FileHash -LiteralPath $PythonEmbedArchive -Algorithm SHA256).Hash-ne $taskExpected){throw 'Expected the reviewed Python 3.13.9 x64 embeddable archive.'}
if(Test-Path -LiteralPath $OutputDirectory){throw 'Use a new compiler-tool output directory.'}
Expand-Archive -LiteralPath $PythonEmbedArchive -DestinationPath $OutputDirectory
$taskSource=Join-Path (Split-Path $PSScriptRoot -Parent) 'src/ChartCompiler'
foreach($taskName in @('compiler.py','icp_codec.py','icp_raw_codec.py','spc_v2_codec.py','spc_v2_timing_codec.py')){Copy-Item -LiteralPath (Join-Path $taskSource $taskName) -Destination $OutputDirectory}
Get-ChildItem -LiteralPath $OutputDirectory -File|ForEach-Object {[pscustomobject]@{name=$_.Name;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $OutputDirectory 'build-manifest.json') -Encoding utf8
Write-Output $OutputDirectory
