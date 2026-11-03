param(
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [string]$PreviewAssets
)
$ErrorActionPreference='Stop'
$taskRepo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskOut=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $taskOut){throw 'Use a new output directory; existing results are never overwritten.'}
$taskArguments=@($taskOut)
if($PreviewAssets){
    $taskAssets=[IO.Path]::GetFullPath($PreviewAssets)
    if(-not(Test-Path -LiteralPath (Join-Path $taskAssets 'preview.bundle'))){throw 'PreviewAssets must contain the existing preview.bundle.'}
    $taskArguments+=$taskAssets
}
Push-Location $taskRepo
try{
    dotnet build tests/AudioReactive.Checks/AudioReactive.Checks.csproj -c Release --nologo -m:1 -nr:false -p:UseSharedCompilation=false
    if($LASTEXITCODE -ne 0){throw 'Audio-reactive checks build failed.'}
    dotnet tests/AudioReactive.Checks/bin/Release/net9.0/AudioReactive.Checks.dll @taskArguments
    if($LASTEXITCODE -ne 0){throw 'Audio-reactive checks failed; inspect the preserved output directory.'}
}finally{Pop-Location}
