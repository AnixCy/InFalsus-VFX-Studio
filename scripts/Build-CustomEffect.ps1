param(
    [Parameter(Mandatory=$true)][string]$ShaderPath,
    [Parameter(Mandatory=$true)][string]$DescriptorPath,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [string]$UnityEditor=$env:UNITY_EDITOR,
    [string]$WorkDirectory,
    [switch]$ReuseProject,
    [switch]$OpenOutput
)
$ErrorActionPreference='Stop'
$taskTemplate=Join-Path (Split-Path $PSScriptRoot -Parent) 'templates/custom-effect'
$taskDefinition=Get-Content -Raw -LiteralPath $DescriptorPath -Encoding UTF8 | ConvertFrom-Json
$taskPreference=Join-Path (Split-Path $PSScriptRoot -Parent) 'local/custom-toolchain.json'
if(-not $UnityEditor -and (Test-Path -LiteralPath $taskPreference)){
    $taskSaved=Get-Content -Raw -LiteralPath $taskPreference -Encoding UTF8|ConvertFrom-Json
    if(Test-Path -LiteralPath $taskSaved.UnityEditor){$UnityEditor=$taskSaved.UnityEditor}
}
if(-not $UnityEditor){
    Add-Type -AssemblyName System.Windows.Forms
    $taskPicker=New-Object System.Windows.Forms.OpenFileDialog
    $taskPicker.Title='Select Unity '+$taskDefinition.UnityVersion+' (Unity.exe)';$taskPicker.Filter='Unity Editor|Unity.exe'
    try{if($taskPicker.ShowDialog() -ne 'OK'){throw 'Canceled.'};$UnityEditor=$taskPicker.FileName}finally{$taskPicker.Dispose()}
}
$taskActual=[Diagnostics.FileVersionInfo]::GetVersionInfo($UnityEditor).ProductVersion
if(-not $taskActual.StartsWith($taskDefinition.UnityVersion,[StringComparison]::Ordinal)){throw 'Unity version does not match the effect descriptor.'}
New-Item -ItemType Directory -Path (Split-Path $taskPreference -Parent) -Force|Out-Null
@{UnityEditor=$UnityEditor}|ConvertTo-Json|Set-Content -LiteralPath $taskPreference -Encoding UTF8
$taskOutput=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $taskOutput){throw 'Use a new output directory; existing output is preserved.'}
if(-not $WorkDirectory){$WorkDirectory=Join-Path ([IO.Path]::GetTempPath()) ('VfxEffect-'+[Guid]::NewGuid().ToString('N'))}
$taskWork=[IO.Path]::GetFullPath($WorkDirectory)
$taskShader=(Resolve-Path -LiteralPath $ShaderPath).Path
$taskMarker=Join-Path $taskWork 'vfx-owned-project.json'
if(Test-Path -LiteralPath $taskWork){
    if(-not $ReuseProject -or -not(Test-Path -LiteralPath $taskMarker)){throw 'Only a generated VFX work project may be reused.'}
    $taskOwned=Get-Content -Raw -LiteralPath $taskMarker -Encoding UTF8|ConvertFrom-Json
    if($taskOwned.Shader -ne $taskShader -or $taskOwned.Unity -ne $taskDefinition.UnityVersion){throw 'The generated cache belongs to another source or Unity version.'}
}
foreach($taskFolder in @('Assets/Custom','Assets/Editor','Packages','ProjectSettings')){New-Item -ItemType Directory -Path (Join-Path $taskWork $taskFolder) -Force|Out-Null}
New-Item -ItemType Directory -Path $taskOutput|Out-Null
Copy-Item -LiteralPath $taskShader -Destination (Join-Path $taskWork 'Assets/Custom/Effect.shader') -Force
@{Shader=$taskShader;Unity=$taskDefinition.UnityVersion}|ConvertTo-Json|Set-Content -LiteralPath $taskMarker -Encoding UTF8
# Local include files are copied as authoring input; no user folder is moved or deleted.
$taskSource=Split-Path $taskShader -Parent
$taskCustom=Join-Path $taskWork 'Assets/Custom'
# The owned cache mirrors source includes; removed includes must not survive a rebuild.
Get-ChildItem -LiteralPath $taskCustom -File | Where-Object {$_.Extension -in '.hlsl','.cginc'} | ForEach-Object {
    if(-not(Test-Path -LiteralPath (Join-Path $taskSource $_.Name))){Remove-Item -LiteralPath $_.FullName}
}
Get-ChildItem -LiteralPath $taskSource -File | Where-Object {$_.Extension -in '.hlsl','.cginc'} | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $taskWork ('Assets/Custom/'+$_.Name)) -Force
}
Copy-Item -LiteralPath (Join-Path $taskTemplate 'BuildCustomEffect.cs') -Destination (Join-Path $taskWork 'Assets/Editor/BuildCustomEffect.cs') -Force
[IO.File]::WriteAllText((Join-Path $taskWork 'Packages/manifest.json'),'{"dependencies":{"com.unity.modules.assetbundle":"1.0.0"}}',[Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $taskWork 'ProjectSettings/ProjectVersion.txt'),('m_EditorVersion: '+$taskDefinition.UnityVersion),[Text.UTF8Encoding]::new($false))
$taskPriorOutput=$env:VFX_CUSTOM_OUTPUT;$taskPriorEntry=$env:VFX_CUSTOM_ENTRY;$taskPriorShader=$env:VFX_CUSTOM_SHADER
try {
    $env:VFX_CUSTOM_OUTPUT=$taskOutput;$env:VFX_CUSTOM_ENTRY=$taskDefinition.Effect.Material;$env:VFX_CUSTOM_SHADER='Assets/Custom/Effect.shader'
    $taskArguments=@('-batchmode','-quit','-nographics','-buildTarget','Win64','-projectPath',('"'+$taskWork+'"'),'-executeMethod','BuildCustomEffect.Build','-logFile',('"'+(Join-Path $taskOutput 'build.log')+'"'))
    $taskProcess=Start-Process -FilePath $UnityEditor -ArgumentList $taskArguments -WindowStyle Hidden -PassThru
    $taskProcess.WaitForExit()
    if($taskProcess.ExitCode -ne 0 -or -not(Test-Path -LiteralPath (Join-Path $taskOutput 'build-ok.txt'))){throw 'Custom shader build failed; see build.log.'}
    $taskDefinition.Effect.Bundle='effect.bundle'
    $taskDefinition | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath (Join-Path $taskOutput 'effect.json') -Encoding UTF8
    Write-Output $taskOutput
    if($OpenOutput){Start-Process -FilePath explorer.exe -ArgumentList ('"'+$taskOutput+'"')}
}finally{$env:VFX_CUSTOM_OUTPUT=$taskPriorOutput;$env:VFX_CUSTOM_ENTRY=$taskPriorEntry;$env:VFX_CUSTOM_SHADER=$taskPriorShader}
