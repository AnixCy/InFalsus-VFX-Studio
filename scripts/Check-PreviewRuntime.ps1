param(
    [Parameter(Mandatory=$true)][string]$PreviewPath,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [string]$PluginDirectory
)
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath($PreviewPath)
$taskExe=Join-Path $taskRoot 'InFalsusStudio.exe'
$taskPlugin=Join-Path $taskRoot 'BepInEx/plugins/InFalsusVfx'
if(-not(Test-Path -LiteralPath (Join-Path $taskPlugin 'InFalsus.Vfx.StudioPlus.dll'))){throw 'Not a prepared VFX preview copy.'}
if(Get-Process -Name InFalsusStudio -ErrorAction SilentlyContinue|Where-Object {$_.Path -eq $taskExe}){throw 'Preview copy is already running.'}
if(Test-Path -LiteralPath $OutputDirectory){throw 'Use a new check output directory.'}
if($PluginDirectory){foreach($taskName in @('InFalsus.Vfx.StudioPlus.dll','InFalsus.Presentation.Core.dll','InFalsus.Presentation.Authoring.dll','preview.bundle')){Copy-Item -LiteralPath (Join-Path $PluginDirectory $taskName) -Destination $taskPlugin -Force}}
$taskOut=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $taskOut|Out-Null
$taskOldStudio=$env:INFSTUDIO_CONFIG_ROOT;$taskOldVfx=$env:VFX_WORK_ROOT
try{
    $env:INFSTUDIO_CONFIG_ROOT=Join-Path $taskOut 'UserData/studio'
    $env:VFX_WORK_ROOT=Join-Path $taskOut 'UserData/vfx'
    $taskArgs=@('-batchmode','-force-d3d11','-screen-width','1280','-screen-height','720','-vfx-self-check','-vfx-check-output',('"'+(Join-Path $taskOut 'checks.json')+'"'),'-logFile',('"'+(Join-Path $taskOut 'player.log')+'"'))
    $taskProcess=Start-Process -FilePath $taskExe -ArgumentList $taskArgs -WorkingDirectory $taskRoot -WindowStyle Hidden -PassThru
    if(-not $taskProcess.WaitForExit(45000)){Stop-Process -Id $taskProcess.Id;throw 'The owned check process exceeded 45 seconds.'}
    if($taskProcess.ExitCode -ne 0){throw ('Runtime checks failed with exit code '+$taskProcess.ExitCode+'; see player.log and checks.json.')}
    $taskReport=Get-Content -Raw -LiteralPath (Join-Path $taskOut 'checks.json')|ConvertFrom-Json
    if(-not $taskReport.ok){throw 'Runtime check report did not pass.'}
    $taskReport|ConvertTo-Json -Depth 8
}finally{$env:INFSTUDIO_CONFIG_ROOT=$taskOldStudio;$env:VFX_WORK_ROOT=$taskOldVfx}
