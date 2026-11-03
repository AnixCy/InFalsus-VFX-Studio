param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$taskRepo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskOut=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $taskOut){throw 'Use a new output directory; existing results are preserved.'}
Push-Location $taskRepo
try{
    dotnet build tests/Workspace.Checks/Workspace.Checks.csproj -c Release --nologo -m:1 -nr:false -p:UseSharedCompilation=false
    if($LASTEXITCODE -ne 0){throw 'Creator workspace checks build failed.'}
    dotnet tests/Workspace.Checks/bin/Release/net9.0/Workspace.Checks.dll $taskOut
    if($LASTEXITCODE -ne 0){throw 'Creator workspace checks failed; inspect the preserved fixtures.'}
}finally{Pop-Location}
