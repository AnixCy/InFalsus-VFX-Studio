param([Parameter(Mandatory=$true)][string]$GameReferenceRoot,[Parameter(Mandatory=$true)][string]$BepInExRoot,[Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$taskRepo=Split-Path $PSScriptRoot -Parent
if(Test-Path -LiteralPath $OutputDirectory){throw 'Use a new package output directory.'}
foreach($taskReference in @($GameReferenceRoot,$BepInExRoot)){
    $taskReferenceFull=(Resolve-Path -LiteralPath $taskReference).Path.TrimEnd([IO.Path]::DirectorySeparatorChar)
    $taskOutputFull=[IO.Path]::GetFullPath($OutputDirectory)
    if($taskOutputFull-eq $taskReferenceFull-or $taskOutputFull.StartsWith($taskReferenceFull+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Package output must be outside reference installations.'}
}
$taskCommon=@('-c','Release','--nologo','--ignore-failed-sources','-m:1','-nr:false','-p:UseSharedCompilation=false','-p:NuGetAudit=false',('-p:GameReferenceRoot='+[IO.Path]::GetFullPath($GameReferenceRoot)))
foreach($taskProject in @('src/GameLoader.Melon/InFalsus.Presentation.Melon.csproj','src/GameLoader.BepInEx/InFalsus.Presentation.BepInEx.csproj')){
    & dotnet build (Join-Path $taskRepo $taskProject) @taskCommon ('-p:BepInExRoot='+[IO.Path]::GetFullPath($BepInExRoot))
    if($LASTEXITCODE-ne 0){throw ('Player build failed: '+$taskProject)}
}
foreach($taskLoader in @('MelonLoader','BepInEx')){
    $taskLoaderDir=if($taskLoader-eq 'MelonLoader'){Join-Path $GameReferenceRoot 'MelonLoader/net6'}else{Join-Path $BepInExRoot 'BepInEx/core'}
    & dotnet build (Join-Path $taskRepo 'src/ChartLoader/InFalsusChartLoader.csproj') @taskCommon ('-p:Loader='+$taskLoader) ('-p:LoaderDir='+[IO.Path]::GetFullPath($taskLoaderDir))
    if($LASTEXITCODE-ne 0){throw ('ChartLoader build failed: '+$taskLoader)}
}
& (Join-Path $PSScriptRoot 'Build-PlayerPackage.ps1') -OutputDirectory $OutputDirectory
