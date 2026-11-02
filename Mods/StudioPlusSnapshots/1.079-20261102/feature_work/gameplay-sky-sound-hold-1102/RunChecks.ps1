param([switch]$Baseline)
$ErrorActionPreference='Stop'
$taskRoot=$PSScriptRoot
$taskPlayer='E:\RhythmGame\InFalsusStudio+\feature_work\gameplay\Player'
$taskDll=if($Baseline){Join-Path $taskRoot 'baseline\InfStudioVisualUI.dll'}else{'E:\RhythmGame\InFalsusStudio+\studio_icon_work\Source\InfStudioVisualUI.dll'}
$taskManaged=Join-Path $taskPlayer 'InFalsusStudio_Data\Managed'
$taskCore=Join-Path $taskPlayer 'BepInEx\core'
$taskRefs=Get-ChildItem -LiteralPath $taskManaged -File | Where-Object {$_.Name -match '^(UnityEngine|Unity\.InputSystem|Unity\.RenderPipelines|Unity\.Collections|netstandard|Assembly-CSharp)'} | ForEach-Object {'/r:'+ $_.FullName}
$taskRefs+=Get-ChildItem -LiteralPath $taskCore -Filter '*.dll' -File | ForEach-Object {'/r:'+ $_.FullName}
$taskRefs+='/r:'+$taskDll
$taskDefines=if($Baseline){'/define:BASELINE'}else{'/define:CANDIDATE'}
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:library $taskDefines ('/out:'+(Join-Path $taskPlayer 'BepInEx\plugins\GameplayChecks.dll')) $taskRefs (Join-Path $taskRoot 'Checks.cs')
if($LASTEXITCODE -ne 0){throw 'Check compilation failed'}
Copy-Item -LiteralPath $taskDll -Destination (Join-Path $taskPlayer 'BepInEx\plugins\InfStudioVisualUI\InfStudioVisualUI.dll') -Force
$env:INFSTUDIO_CONFIG_ROOT=Join-Path $taskRoot 'profile'
$taskLog=if($Baseline){'baseline-checks.log'}else{'checks.log'}
$taskProc=Start-Process -FilePath (Join-Path $taskPlayer 'InFalsusStudio.exe') -WorkingDirectory $taskPlayer -ArgumentList @('-sky-sound-hold-checks','-screen-width','1280','-screen-height','720','-screen-fullscreen','0','-logFile',(Join-Path $taskRoot $taskLog)) -WindowStyle Hidden -PassThru
$taskProc.Id | Set-Content -LiteralPath (Join-Path $taskRoot 'checks.pid')
Write-Output ('Isolated checks PID '+$taskProc.Id)
