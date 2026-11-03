param(
    [Parameter(Mandatory=$true)][string]$UnityEditor,
    [ValidateSet('Gamma','Linear')][string]$ColorSpace='Gamma',
    [string]$OutputDirectory,
    [string]$ShaderSource
)
$ErrorActionPreference='Stop'
$taskRepo=Split-Path $PSScriptRoot -Parent
$taskVariant=''
if($ShaderSource){
    $ShaderSource=(Resolve-Path -LiteralPath $ShaderSource).Path
    $taskVariant=(Get-FileHash -LiteralPath $ShaderSource -Algorithm SHA256).Hash.Substring(0,12).ToLowerInvariant()
}
if(-not $OutputDirectory){$OutputDirectory=Join-Path $taskRepo ('local/build/game-assets/'+$(if($taskVariant){$taskVariant+'/'}else{''})+$ColorSpace)}
$taskOutput=[IO.Path]::GetFullPath($OutputDirectory)
if(-not [Diagnostics.FileVersionInfo]::GetVersionInfo($UnityEditor).ProductVersion.StartsWith('6000.3.9f1')){throw 'Use Unity 6000.3.9f1 for this target.'}
$taskProject=Join-Path $taskRepo 'local/game-asset-project'
$taskOwner=Join-Path $taskProject '.vfx-game-assets-owner'
if((Test-Path -LiteralPath $taskProject)-and -not(Test-Path -LiteralPath $taskOwner)){throw 'Existing resource project is not owned by this builder.'}
New-Item -ItemType Directory -Path (Join-Path $taskProject 'Assets/Editor'),(Join-Path $taskProject 'Packages'),(Join-Path $taskProject 'ProjectSettings'),$taskOutput -Force|Out-Null
[IO.File]::WriteAllText($taskOwner,'InFalsus-VFX-Studio game assets v1')
[IO.File]::WriteAllText((Join-Path $taskProject 'ProjectSettings/ProjectVersion.txt'),"m_EditorVersion: 6000.3.9f1`n")
[IO.File]::WriteAllText((Join-Path $taskProject 'Packages/manifest.json'),'{"dependencies":{"com.unity.render-pipelines.universal":"17.3.0","com.unity.modules.assetbundle":"1.0.0","com.unity.modules.imageconversion":"1.0.0"}}')
if(-not $ShaderSource){$ShaderSource=Join-Path $taskRepo 'templates/game-addon/AddonBackground.shader'}
Copy-Item -LiteralPath $ShaderSource -Destination (Join-Path $taskProject 'Assets/AddonBackground.shader') -Force
Copy-Item -LiteralPath (Join-Path $taskRepo 'templates/game-addon/BuildAddonAssets.cs') -Destination (Join-Path $taskProject 'Assets/Editor/BuildAddonAssets.cs') -Force
$env:VFX_GAME_ASSET_OUTPUT=$taskOutput;$env:VFX_GAME_COLOR_SPACE=$ColorSpace
$env:VFX_GAME_BUNDLE_NAME=if($taskVariant){'vfx-bg-'+$taskVariant+'-'+$ColorSpace.ToLowerInvariant()+'.bundle'}else{'background.bundle'}
$taskArgs=@('-batchmode','-quit','-nographics','-buildTarget','Win64','-projectPath',('"'+$taskProject+'"'),'-executeMethod','BuildAddonAssets.Run','-logFile',('"'+(Join-Path $taskOutput 'build.log')+'"'))
$taskProcess=Start-Process -FilePath $UnityEditor -ArgumentList $taskArgs -WindowStyle Hidden -PassThru
Write-Output ('Unity asset build PID '+$taskProcess.Id+'; log '+(Join-Path $taskOutput 'build.log'))
$taskProcess.WaitForExit()
if($taskProcess.ExitCode-ne 0){throw 'Game addon resource build failed; see build.log.'}
if(-not(Test-Path -LiteralPath (Join-Path $taskOutput 'build-info.json'))){throw 'Game resource verification receipt missing.'}
Write-Output $taskOutput
