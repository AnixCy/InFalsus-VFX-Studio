param([Parameter(Mandatory=$true)][string]$GameReferenceRoot,[string]$OutputDirectory)
$ErrorActionPreference='Stop'
$taskRepo=Split-Path $PSScriptRoot -Parent
if(-not $OutputDirectory){$OutputDirectory=Join-Path $taskRepo 'local/build/game-player'}
$taskOut=[IO.Path]::GetFullPath($OutputDirectory)
$taskReferences=(Resolve-Path -LiteralPath $GameReferenceRoot).Path.TrimEnd('\')
if($taskOut-eq $taskReferences-or $taskOut.StartsWith($taskReferences+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Build output must not be inside the reference game.'}
$taskOwner=Join-Path $taskOut '.vfx-player-build'
if((Test-Path -LiteralPath $taskOut)-and -not(Test-Path -LiteralPath $taskOwner)){throw 'Existing output is not owned by this builder.'}
& dotnet build (Join-Path $taskRepo 'src/GameLoader.Melon/InFalsus.Presentation.Melon.csproj') -c Release --nologo --ignore-failed-sources -m:1 -nr:false -p:UseSharedCompilation=false -p:NuGetAudit=false ('-p:GameReferenceRoot='+$taskReferences)
if($LASTEXITCODE-ne 0){throw 'Player runtime build failed.'}
New-Item -ItemType Directory -Path (Join-Path $taskOut 'Mods'),(Join-Path $taskOut 'UserLibs') -Force|Out-Null
[IO.File]::WriteAllText($taskOwner,'InFalsus-VFX-Studio player build v1')
$taskBin=Join-Path $taskRepo 'src/GameLoader.Melon/bin/Release/net6.0'
foreach($taskName in @('InFalsus.Presentation.Core.dll','InFalsus.Vfx.Addon.dll','InFalsus.Presentation.Game.dll')){Copy-Item -LiteralPath (Join-Path $taskBin $taskName) -Destination (Join-Path $taskOut ('UserLibs/'+$taskName)) -Force}
Copy-Item -LiteralPath (Join-Path $taskBin 'InFalsus.Presentation.Melon.dll') -Destination (Join-Path $taskOut 'Mods/InFalsus.Presentation.Melon.dll') -Force
Get-ChildItem -LiteralPath $taskOut -Recurse -File|Where-Object Extension -eq '.dll'|ForEach-Object{
    [pscustomobject]@{name=$_.FullName.Substring($taskOut.Length+1);bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $taskOut 'build-manifest.json') -Encoding utf8
Write-Output $taskOut
