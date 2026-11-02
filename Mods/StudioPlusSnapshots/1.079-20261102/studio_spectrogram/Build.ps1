param([string]$Editor='E:\RhythmGame\Infstudio\InFalsusStudio_1.0.10',[string]$UnityEditor='D:\unity\editor\2022.3.8f1c1')
$ErrorActionPreference='Stop'
$taskManaged=Join-Path $Editor 'InFalsusStudio_Data\Managed';$taskCore=Join-Path $Editor 'BepInEx\core'
$taskNames=@('Assembly-CSharp.dll','UnityEngine.dll','UnityEngine.CoreModule.dll','UnityEngine.AudioModule.dll','UnityEngine.IMGUIModule.dll','UnityEngine.AssetBundleModule.dll','UnityEngine.TextRenderingModule.dll','netstandard.dll')
$taskRefs=$taskNames|ForEach-Object {'/r:'+(Join-Path $taskManaged $_)}
$taskRefs+=@('BepInEx.Core.dll','BepInEx.Unity.Mono.dll','0Harmony.dll')|ForEach-Object {'/r:'+(Join-Path $taskCore $_)}
$taskRefs+='/r:'+(Join-Path $Editor 'BepInEx\plugins\InfStudioVisualUI\InfStudioVisualUI.dll')
$taskRefs+=@('mscorlib.dll','System.dll','System.Core.dll')|ForEach-Object {'/r:'+(Join-Path "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319" $_)}
$taskSources=Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Source') -Filter '*.cs' -File | ForEach-Object {$_.FullName}
if($taskSources.Count -ne 7){throw 'Expected 7 independent spectrogram source files'}
$taskOutput=Join-Path $PSScriptRoot 'InfStudioSpectrogram.dll'
& (Join-Path $UnityEditor 'Editor\Data\NetCoreRuntime\dotnet.exe') (Join-Path $UnityEditor 'Editor\Data\DotNetSdkRoslyn\csc.dll') /nologo /nostdlib+ /target:library ('/out:'+$taskOutput) $taskRefs $taskSources
if($LASTEXITCODE -ne 0){throw 'Spectrogram compilation failed'}
Write-Output ('Built '+$taskOutput)
