param([string]$UnityEditor='D:\unity\editor\2022.3.8f1c1')
$ErrorActionPreference='Stop'
$taskMod=Split-Path $PSScriptRoot -Parent
$taskRoot=[IO.Path]::GetFullPath((Join-Path $taskMod '..\feature_work\key-opacity'))
$taskProject=Join-Path $taskRoot 'ShaderProject'
New-Item -ItemType Directory "$taskProject\Assets\Editor","$taskProject\Packages","$taskProject\ProjectSettings" -Force | Out-Null
Copy-Item "$taskMod\Source\Native\Shaders\NativeKeyGamma.shader" "$taskProject\Assets\NativeKey.shader" -Force
Copy-Item "$taskMod\Source\Native\Shaders\NativeKeyGamma.shader.meta" "$taskProject\Assets\NativeKey.shader.meta" -Force
Copy-Item "$PSScriptRoot\KeyShaderBuild.cs" "$taskProject\Assets\Editor\BuildKey.cs" -Force
if(!(Test-Path "$taskProject\Packages\manifest.json")){[IO.File]::WriteAllText("$taskProject\Packages\manifest.json",'{"dependencies":{}}')}
$taskProcess=Start-Process "$UnityEditor\Editor\Unity.exe" -ArgumentList "-batchmode -quit -noUpm -projectPath `"$taskProject`" -executeMethod BuildKey.Build -logFile `"$taskRoot\shader-build.log`"" -WindowStyle Hidden -Wait -PassThru
if($taskProcess.ExitCode -ne 0){throw 'Shader build failed; inspect shader-build.log'}
Get-FileHash "$taskRoot\Payload\key-gamma.bundle"
