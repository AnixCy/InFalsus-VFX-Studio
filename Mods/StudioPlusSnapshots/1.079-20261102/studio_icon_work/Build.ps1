param(
    [Parameter(Mandatory=$true)][string]$Editor,
    [Parameter(Mandatory=$true)][string]$UnityEditor
)
$ErrorActionPreference = 'Stop'

$taskManaged = Join-Path $Editor 'InFalsusStudio_Data\Managed'
$taskCore = Join-Path $Editor 'BepInEx\core'
$taskCompilerRoot = Join-Path $UnityEditor 'Editor\Data'
$taskDotnet = Join-Path $taskCompilerRoot 'NetCoreRuntime\dotnet.exe'
$taskCsc = Join-Path $taskCompilerRoot 'DotNetSdkRoslyn\csc.dll'
foreach ($taskPath in @($taskManaged, $taskCore, $taskDotnet, $taskCsc)) {
    if (-not (Test-Path -LiteralPath $taskPath)) { throw "Required path not found: $taskPath" }
}

$taskNames = @(
    'Unity.Collections.dll', 'UnityEngine.TextCoreFontEngineModule.dll', 'UnityEngine.TextCoreTextEngineModule.dll', 'Assembly-CSharp.dll', 'UnityEngine.CoreModule.dll', 'UnityEngine.AssetBundleModule.dll',
    'UnityEngine.IMGUIModule.dll', 'UnityEngine.TextRenderingModule.dll',
    'UnityEngine.InputLegacyModule.dll', 'Unity.InputSystem.dll', 'UnityEngine.AudioModule.dll',
    'UnityEngine.ScreenCaptureModule.dll', 'UnityEngine.JSONSerializeModule.dll',
    'UnityEngine.ImageConversionModule.dll', 'UnityEngine.dll', 'netstandard.dll',
    'Unity.RenderPipelines.Core.Runtime.dll', 'Unity.RenderPipelines.Universal.Runtime.dll',
    'Unity.RenderPipelines.GPUDriven.Runtime.dll'
)
$taskRefs = $taskNames | ForEach-Object { '/reference:' + (Join-Path $taskManaged $_) }
$taskRefs += @('BepInEx.Core.dll', 'BepInEx.Unity.Mono.dll', '0Harmony.dll') |
    ForEach-Object { '/reference:' + (Join-Path $taskCore $_) }
$taskRefs += @('mscorlib.dll', 'System.dll', 'System.Core.dll') |
    ForEach-Object { '/reference:' + (Join-Path "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319" $_) }
$taskSourceRoot = Join-Path $PSScriptRoot 'Source'
$taskSources = Get-ChildItem -LiteralPath $taskSourceRoot -Filter '*.cs' -File -Recurse |
    ForEach-Object { $_.FullName }
if ($taskSources.Count -ne 160) { throw "Expected 160 C# source files; found $($taskSources.Count). Review the source snapshot before building." }
$taskRefs += ('/resource:' + (Join-Path $taskSourceRoot 'BrandAssets\about-mascot.png') + ',infstudioplus.about.png')
$taskRefs += ('/resource:' + (Join-Path $PSScriptRoot '..\feature_work\native-dots-text\Payload\native-key-text.bundle') + ',infstudioplus.keytext.bundle')
$taskRefs += ('/resource:' + (Join-Path $PSScriptRoot '..\feature_work\native-dots-text\Payload\native-key-sdf.png') + ',infstudioplus.keytext.png')
$taskOutput = Join-Path $taskSourceRoot 'InfStudioVisualUI.dll'
& $taskDotnet $taskCsc /nologo /noconfig /nostdlib+ /target:library ('/out:' + $taskOutput) $taskRefs $taskSources
if ($LASTEXITCODE -ne 0) { throw 'Integrated mod compilation failed.' }
Write-Host "Built $taskOutput"

