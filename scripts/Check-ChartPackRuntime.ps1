param([Parameter(Mandatory=$true)][string]$Sandbox,[Parameter(Mandatory=$true)][string]$ChartPackage,[ValidateSet('MelonLoader','BepInEx')][string]$Loader='MelonLoader')
$ErrorActionPreference='Stop'
$taskRepo=Split-Path $PSScriptRoot -Parent
$taskRoot=(Resolve-Path -LiteralPath $Sandbox).Path
if(-not(Test-Path -LiteralPath (Join-Path $taskRoot '.vfx-addon-sandbox.json'))){throw 'Only marked physical test copies are allowed.'}
$taskExe=Join-Path $taskRoot $(if($Loader-eq 'BepInEx'){'infalsus.exe'}else{'VfxAddonTest.exe'})
if(Get-Process -ErrorAction SilentlyContinue|Where-Object {$_.Path-eq $taskExe}){throw 'Owned test copy is already running.'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$taskZip=[IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $ChartPackage).Path)
try {
    $taskNames=@($taskZip.Entries|Where-Object {$_.Name.Length-gt 0}|ForEach-Object {$_.FullName.Replace([char]92,[char]47)})
    if($taskNames.Count-gt 128-or @($taskNames|Where-Object {$_-match '(^/|(^|/)[.][.](/|$)|:)'}).Count-gt 0){throw 'Unsafe test ZIP.'}
    $taskTop=@($taskNames|ForEach-Object {($_-split '/')[0]}|Select-Object -Unique)
    if($taskTop.Count-ne 1-or $taskTop[0]-notlike 'Song_pack_fixture*'){throw 'Only synthetic fixture song packages are accepted.'}
}finally{$taskZip.Dispose()}
$taskArchive=Join-Path $taskRoot ('chartpack-checks-'+[DateTime]::Now.ToString('yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,4))
New-Item -ItemType Directory -Path $taskArchive|Out-Null
foreach($taskRelative in @('addon-checks.json',('Charts/'+$taskTop[0]),'UserData/InFalsusPresentation/Addons/Fixture','UserData/InFalsusPresentation/Addons/Duplicate','UserData/InFalsusPresentation/StoredFixture','UserData/InFalsusPresentation/StoredDuplicate')) {
    $taskPath=Join-Path $taskRoot $taskRelative
    if(Test-Path -LiteralPath $taskPath){
        $taskFrom=(Resolve-Path -LiteralPath $taskPath).Path
        if(-not $taskFrom.StartsWith($taskRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or -not $taskArchive.StartsWith($taskRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Archive escaped sandbox.'}
        Move-Item -LiteralPath $taskFrom -Destination $taskArchive
    }
}
foreach($taskRelative in @('MelonLoader/Latest.log','BepInEx/LogOutput.log','vfx-check-player.log')){if(Test-Path -LiteralPath (Join-Path $taskRoot $taskRelative)){Copy-Item -LiteralPath (Join-Path $taskRoot $taskRelative) -Destination $taskArchive}}
New-Item -ItemType Directory -Path (Join-Path $taskRoot 'Charts') -Force|Out-Null
[IO.Compression.ZipFile]::ExtractToDirectory((Resolve-Path -LiteralPath $ChartPackage).Path,(Join-Path $taskRoot 'Charts'))
$taskNativeBin=Join-Path $taskRepo 'src/GameRuntime/bin/Release/net6.0'
$taskPlugin=if($Loader-eq 'MelonLoader'){Join-Path $taskRoot 'Mods'}else{Join-Path $taskRoot 'BepInEx/plugins'}
$taskLibs=if($Loader-eq 'MelonLoader'){Join-Path $taskRoot 'UserLibs'}else{Join-Path $taskPlugin 'InFalsusPresentation'}
New-Item -ItemType Directory -Path $taskPlugin,$taskLibs -Force|Out-Null
foreach($taskName in @('InFalsus.Presentation.Game.dll','InFalsus.Presentation.Core.dll','InFalsus.Vfx.Addon.dll')){Copy-Item -LiteralPath (Join-Path $taskNativeBin $taskName) -Destination $taskLibs -Force}
Copy-Item -LiteralPath (Join-Path $taskRepo ('src/ChartLoader/bin/'+$Loader+'/Release/net6.0/InFalsusChartLoader.dll')) -Destination $taskPlugin -Force
if($Loader-eq 'MelonLoader') {
    Copy-Item -LiteralPath (Join-Path $taskRepo 'src/GameLoader.Melon/bin/Release/net6.0/InFalsus.Presentation.Melon.dll') -Destination $taskPlugin -Force
    Copy-Item -LiteralPath (Join-Path $taskRepo 'tests/Game.Runtime.Checks/bin/Release/net6.0/InFalsus.Vfx.GameChecks.dll') -Destination $taskPlugin -Force
}else {
    Copy-Item -LiteralPath (Join-Path $taskRepo 'src/GameLoader.BepInEx/bin/Release/net6.0/InFalsus.Presentation.BepInEx.dll') -Destination $taskLibs -Force
    Copy-Item -LiteralPath (Join-Path $taskRepo 'tests/BepInEx.Runtime.Checks/bin/Release/net6.0/InFalsus.Vfx.BepChecks.dll') -Destination $taskPlugin -Force
}
# The native-profile defaults must work without a developer-maintained catalog/profile file.
$taskData=Join-Path $taskRoot 'UserData/InFalsusPresentation'
foreach($taskName in @('profiles.json','catalog.json')) {
    $taskPath=Join-Path $taskData $taskName
    if(Test-Path -LiteralPath $taskPath){$taskFrom=(Resolve-Path -LiteralPath $taskPath).Path;if(-not $taskFrom.StartsWith($taskRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Profile archive escaped sandbox'};Move-Item -LiteralPath $taskFrom -Destination $taskArchive}
}
$taskArgs=@('-batchmode','-force-d3d11','-screen-width','1280','-screen-height','720','-noaudio','-vfx-addon-check','-vfx-chart-package',$taskTop[0],'-logFile',('"'+(Join-Path $taskRoot 'vfx-check-player.log')+'"'))
$taskProcess=Start-Process -FilePath $taskExe -ArgumentList $taskArgs -WorkingDirectory $taskRoot -WindowStyle Hidden -PassThru
Write-Output ('Owned '+$Loader+' chart-package check PID '+$taskProcess.Id)
$taskReport=Join-Path $taskRoot 'addon-checks.json';$taskDeadline=[DateTime]::UtcNow.AddMinutes(5)
while(-not $taskProcess.WaitForExit(1000)) {
    if(Test-Path -LiteralPath $taskReport){try{$taskState=Get-Content -Raw -LiteralPath $taskReport|ConvertFrom-Json;if(-not $taskState.ok){Stop-Process -Id $taskProcess.Id;break}}catch{}}
    if([DateTime]::UtcNow-gt $taskDeadline){Stop-Process -Id $taskProcess.Id;throw 'Owned chart-package check timeout.'}
}
if(-not(Test-Path -LiteralPath $taskReport)){throw 'No receipt; inspect owned logs.'}
$taskReceipt=Get-Content -Raw -LiteralPath $taskReport|ConvertFrom-Json
if(-not $taskReceipt.ok){throw ('Chart-package checks failed: '+$taskReceipt.error)}
[pscustomobject]@{ok=$true;loader=$Loader;count=$taskReceipt.checks.Count;receipt=$taskReport;gameplayStarted=$taskReceipt.gameplayStarted}|ConvertTo-Json
