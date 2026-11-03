param([Parameter(Mandatory=$true)][string]$Sandbox,[Parameter(Mandatory=$true)][string]$Fixture,[switch]$ArchivePrevious,[switch]$IncludePlayerLoader)
$ErrorActionPreference='Stop'
$taskRepo=Split-Path $PSScriptRoot -Parent
$taskRoot=(Resolve-Path -LiteralPath $Sandbox).Path
if(-not(Test-Path -LiteralPath (Join-Path $taskRoot '.vfx-addon-sandbox.json'))){throw 'Only an owned isolated copy may run these checks.'}
$taskExe=Join-Path $taskRoot 'VfxAddonTest.exe'
if(Get-Process VfxAddonTest -ErrorAction SilentlyContinue|Where-Object {$_.Path-eq $taskExe}){throw 'Owned test process is already running.'}
$taskReport=Join-Path $taskRoot 'addon-checks.json'
$taskFixture=Join-Path $taskRoot 'UserData/InFalsusPresentation/Addons/Fixture'
if((Test-Path -LiteralPath $taskReport)-or(Test-Path -LiteralPath $taskFixture)){
    if(-not $ArchivePrevious){throw 'A previous attempt exists; use -ArchivePrevious to preserve it before running again.'}
    $taskArchive=Join-Path $taskRoot ('checks-'+[DateTime]::Now.ToString('yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,6))
    New-Item -ItemType Directory -Path $taskArchive|Out-Null
    foreach($taskRelative in @('addon-checks.json','UserData/InFalsusPresentation/Addons/Fixture','UserData/InFalsusPresentation/Addons/Duplicate','UserData/InFalsusPresentation/StoredFixture','UserData/InFalsusPresentation/StoredDuplicate')){
        $taskPrevious=Join-Path $taskRoot $taskRelative
        if(Test-Path -LiteralPath $taskPrevious){
            $taskResolved=(Resolve-Path -LiteralPath $taskPrevious).Path
            if(-not $taskResolved.StartsWith($taskRoot+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Archive source escapes sandbox.'}
            if(-not [IO.Path]::GetFullPath($taskArchive).StartsWith($taskRoot+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Archive destination escapes sandbox.'}
            Move-Item -LiteralPath $taskResolved -Destination $taskArchive
        }
    }
    foreach($taskRelative in @('MelonLoader/Latest.log','addon-check-player.log')){
        if(Test-Path -LiteralPath (Join-Path $taskRoot $taskRelative)){Copy-Item -LiteralPath (Join-Path $taskRoot $taskRelative) -Destination $taskArchive}
    }
}
Copy-Item -LiteralPath $Fixture -Destination $taskFixture -Recurse
foreach($taskName in @('InFalsus.Presentation.Game.dll','InFalsus.Presentation.Core.dll','InFalsus.Vfx.Addon.dll')) {
    Copy-Item -LiteralPath (Join-Path $taskRepo ('tests/Game.Runtime.Checks/bin/Release/net6.0/'+$taskName)) -Destination (Join-Path $taskRoot ('UserLibs/'+$taskName)) -Force
}
Copy-Item -LiteralPath (Join-Path $taskRepo 'tests/Game.Runtime.Checks/bin/Release/net6.0/InFalsus.Vfx.GameChecks.dll') -Destination (Join-Path $taskRoot 'Mods/InFalsus.Vfx.GameChecks.dll')
if($IncludePlayerLoader){Copy-Item -LiteralPath (Join-Path $taskRepo 'src/GameLoader.Melon/bin/Release/net6.0/InFalsus.Presentation.Melon.dll') -Destination (Join-Path $taskRoot 'Mods/InFalsus.Presentation.Melon.dll') -Force}
$taskArgs=@('-batchmode','-force-d3d11','-screen-width','1280','-screen-height','720','-noaudio','-vfx-addon-check','-logFile',('"'+(Join-Path $taskRoot 'addon-check-player.log')+'"'))
$taskProcess=Start-Process -FilePath $taskExe -ArgumentList $taskArgs -WorkingDirectory $taskRoot -WindowStyle Hidden -PassThru
Write-Output ('Owned IL2CPP check PID '+$taskProcess.Id)
if(-not $taskProcess.WaitForExit(125000)){Stop-Process -Id $taskProcess.Id;throw 'Owned runtime check exceeded timeout.'}
if(-not(Test-Path -LiteralPath $taskReport)){throw 'No runtime receipt; inspect the isolated log.'}
$taskReceipt=Get-Content -Raw -LiteralPath $taskReport|ConvertFrom-Json
if($taskProcess.ExitCode-ne 0-or -not $taskReceipt.ok){throw ('Isolated checks failed; see '+$taskReport)}
[pscustomobject]@{ok=$true;checks=$taskReceipt.checks.Count;receipt=$taskReport;visualInspection=$false;syntheticTransport=$taskReceipt.syntheticTransport}|ConvertTo-Json
