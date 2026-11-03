param(
    [Parameter(Mandatory=$true)][string]$StudioPath,
    [Parameter(Mandatory=$true)][string]$UnityEditor,
    [string]$OutputDirectory,
    [switch]$SkipShader
)
$ErrorActionPreference = 'Stop'
$taskRepo = Split-Path $PSScriptRoot -Parent
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $taskRepo 'local/build/preview' }
$taskOutput = [IO.Path]::GetFullPath($OutputDirectory)
$taskStudio = [IO.Path]::GetFullPath($StudioPath)
$taskCore = Join-Path $taskStudio 'InFalsusStudio_Data/Managed/Assembly-CSharp.dll'
$taskExpected = (Get-Content -Raw -LiteralPath (Join-Path $taskRepo 'vfx-project.json') | ConvertFrom-Json).upstream.hostCoreSha256
if ((Get-FileHash -LiteralPath $taskCore -Algorithm SHA256).Hash -ne $taskExpected) { throw 'Studio core does not match the declared baseline.' }
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
if (-not $SkipShader) {
    $taskExpectedUnity = (Get-Content -LiteralPath (Join-Path $taskRepo 'src/ShaderAssets/ProjectSettings/ProjectVersion.txt') | Where-Object { $_ -match '^m_EditorVersion: ' }) -replace '^m_EditorVersion: ',''
    $taskActualUnity = [Diagnostics.FileVersionInfo]::GetVersionInfo($UnityEditor).ProductVersion
    if (-not $taskActualUnity.StartsWith($taskExpectedUnity,[StringComparison]::Ordinal)) {
        throw ('Shader resources require Unity '+$taskExpectedUnity+'; selected editor is '+$taskActualUnity)
    }
}
$taskProject = Join-Path $taskRepo 'src/StudioPlusPreview/InFalsus.Vfx.StudioPlus.csproj'
& dotnet build $taskProject -c Release --nologo --ignore-failed-sources -m:1 -p:BuildInParallel=false ('-p:StudioPath=' + $taskStudio)
if ($LASTEXITCODE -ne 0) { throw 'Preview plugin build failed.' }
$taskBin = Join-Path $taskRepo 'src/StudioPlusPreview/bin/Release/netstandard2.1'
foreach ($taskName in @('InFalsus.Vfx.StudioPlus.dll','InFalsus.Presentation.Core.dll','InFalsus.Presentation.Authoring.dll','InFalsus.Vfx.Addon.dll','InFalsus.Vfx.Addon.Export.dll')) {
    Copy-Item -LiteralPath (Join-Path $taskBin $taskName) -Destination $taskOutput -Force
}
if (-not $SkipShader) {
    $env:VFX_SHADER_OUTPUT = $taskOutput
    $taskAssetProject = Join-Path $taskRepo 'src/ShaderAssets'
    $taskArguments = @('-batchmode','-quit','-nographics','-buildTarget','Win64','-projectPath',('"'+$taskAssetProject+'"'),'-executeMethod','BuildPreviewBundle.Build','-logFile',('"'+(Join-Path $taskOutput 'shader-build.log')+'"'))
    $taskProcess = Start-Process -FilePath $UnityEditor -ArgumentList $taskArguments -WindowStyle Hidden -PassThru
    $taskProcess.WaitForExit()
    if ($taskProcess.ExitCode -ne 0) { throw 'Shader build failed; see shader-build.log.' }
}
if (-not (Test-Path -LiteralPath (Join-Path $taskOutput 'preview.bundle'))) { throw 'preview.bundle is missing.' }
Get-ChildItem -LiteralPath $taskOutput -File | Where-Object Extension -In '.dll','.bundle' | ForEach-Object {
    [pscustomobject]@{name=$_.Name;bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskOutput 'build-manifest.json') -Encoding utf8
Write-Output ('Built ' + $taskOutput)
