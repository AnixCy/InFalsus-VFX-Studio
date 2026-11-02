param([string]$UnityEditor='D:\unity\editor\2022.3.8f1c1')
$ErrorActionPreference='Stop'
$taskProject=Join-Path $PSScriptRoot '..\feature_work\native-dots-text\ShaderProject'
$taskLog=Join-Path $PSScriptRoot '..\feature_work\spectrum-decoupled-1102\shader-build.log'
$taskArguments='-batchmode -nographics -noUpm -quit -projectPath "'+[IO.Path]::GetFullPath($taskProject)+'" -executeMethod BuildSpectrogramDecoupled.Build -logFile "'+[IO.Path]::GetFullPath($taskLog)+'"'
$taskProcess=Start-Process -FilePath (Join-Path $UnityEditor 'Editor\Unity.exe') -ArgumentList $taskArguments -WindowStyle Hidden -PassThru
if(!$taskProcess.WaitForExit(60000)){Write-Output ('Shader build still running: '+$taskProcess.Id);return}
if($taskProcess.ExitCode -ne 0){throw ('Shader build failed. See '+$taskLog)}
