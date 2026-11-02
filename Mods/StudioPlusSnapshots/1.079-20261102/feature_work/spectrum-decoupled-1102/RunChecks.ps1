$ErrorActionPreference='Stop'
$taskProject='E:\RhythmGame\InFalsusStudio+'
$taskPlayer=Join-Path $taskProject 'feature_work\skyarea-spc2\Player'
$taskManaged=Join-Path $taskPlayer 'InFalsusStudio_Data\Managed';$taskCore=Join-Path $taskPlayer 'BepInEx\core'
$taskRefs=Get-ChildItem -LiteralPath $taskManaged -File | Where-Object {$_.Name -match '^(UnityEngine|Unity\.InputSystem|Unity\.RenderPipelines|Unity\.Collections|netstandard|Assembly-CSharp)'} | ForEach-Object {'/r:'+ $_.FullName}
$taskRefs+=Get-ChildItem -LiteralPath $taskCore -Filter '*.dll' -File | ForEach-Object {'/r:'+ $_.FullName}
$taskRefs+='/r:'+(Join-Path $taskProject 'studio_icon_work\Source\InfStudioVisualUI.dll');$taskRefs+='/r:'+(Join-Path $taskProject 'studio_spectrogram\InfStudioSpectrogram.dll')
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:library ('/out:'+(Join-Path $taskPlayer 'BepInEx\plugins\SpectrogramChecks.dll')) $taskRefs (Join-Path $PSScriptRoot 'Checks.cs')
if($LASTEXITCODE -ne 0){throw 'Check compilation failed'}
$taskMain=Join-Path $taskProject 'studio_icon_work\Source\InfStudioVisualUI.dll';$taskSpectrum=Join-Path $taskProject 'studio_spectrogram\InfStudioSpectrogram.dll';$taskBundle=Join-Path $PSScriptRoot 'Payload\spectrogram.bundle'
Copy-Item -LiteralPath $taskMain -Destination (Join-Path $taskPlayer 'BepInEx\plugins\InfStudioVisualUI\InfStudioVisualUI.dll') -Force
$taskFolder=Join-Path $taskPlayer 'BepInEx\plugins\InfStudioSpectrogram';New-Item -ItemType Directory -Path $taskFolder -Force | Out-Null
Copy-Item -LiteralPath $taskSpectrum -Destination $taskFolder -Force
Copy-Item -LiteralPath $taskBundle -Destination $taskFolder -Force
$env:INFSTUDIO_CONFIG_ROOT=Join-Path $PSScriptRoot ('profile-'+(Get-Date -Format 'yyyyMMdd-HHmmss'));New-Item -ItemType Directory -Path $env:INFSTUDIO_CONFIG_ROOT -Force | Out-Null;Copy-Item -LiteralPath "$env:APPDATA\infstudio\camera.json" -Destination $env:INFSTUDIO_CONFIG_ROOT; $env:INFSTUDIO_CONFIG_ROOT | Set-Content (Join-Path $PSScriptRoot 'profile-path.txt')
$taskArguments='-spectrum-decoupled-checks -screen-width 1600 -screen-height 900 -screen-fullscreen 0 -logFile "'+(Join-Path $PSScriptRoot 'checks.log')+'"'
$taskProcess=Start-Process -FilePath (Join-Path $taskPlayer 'InFalsusStudio.exe') -WorkingDirectory $taskPlayer -ArgumentList $taskArguments -WindowStyle Hidden -PassThru
$taskProcess.Id | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'checks.pid');Write-Output ('Isolated check PID '+$taskProcess.Id)
