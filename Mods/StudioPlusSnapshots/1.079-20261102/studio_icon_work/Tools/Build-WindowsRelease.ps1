param(
    [Parameter(Mandatory=$true)][string]$Editor,
    [Parameter(Mandatory=$true)][string]$OutputRoot,
    [string]$Plugin,
    [string]$BuildDate=(Get-Date -Format "yyyyMMdd"),
    [string]$BuildLabel
)
$ErrorActionPreference='Stop'
$taskNode=Split-Path $PSScriptRoot -Parent
$taskPolicy=Get-Content -LiteralPath (Join-Path $taskNode 'distribution.json') -Raw | ConvertFrom-Json
$taskEditor=(Resolve-Path -LiteralPath $Editor).Path.TrimEnd('\')
if (!$Plugin) { $Plugin=Join-Path $taskNode 'Source\InfStudioVisualUI.dll' }
$taskPlugin=(Resolve-Path -LiteralPath $Plugin).Path
function Hash([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash }
if ([Reflection.AssemblyName]::GetAssemblyName($taskPlugin).Version.ToString() -ne $taskPolicy.assemblyVersion) { throw 'Unexpected plugin version' }
if ((Hash (Join-Path $taskEditor 'InFalsusStudio_Data\Managed\Assembly-CSharp.dll')) -ne $taskPolicy.editorCoreSha256) { throw 'Editor core does not match this source node' }
if ($taskPolicy.runtimeAssets) { foreach($taskAsset in $taskPolicy.runtimeAssets.PSObject.Properties) { if ((Hash (Join-Path $taskEditor $taskAsset.Name)) -ne $taskAsset.Value) { throw ('Runtime asset mismatch: '+$taskAsset.Name) } } }
if ($taskPolicy.additionalPlugins) { foreach($taskAsset in $taskPolicy.additionalPlugins.PSObject.Properties) { if ((Hash (Join-Path $taskEditor $taskAsset.Name)) -ne $taskAsset.Value) { throw ('Additional plugin mismatch: '+$taskAsset.Name) } } }
$taskOutput=[IO.Path]::GetFullPath($OutputRoot).TrimEnd('\')
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
if ($BuildDate -notmatch '^\d{8}$') { throw 'BuildDate must be yyyyMMdd' }
$taskBuild=if($BuildLabel){if($BuildLabel -notmatch '^\d{4}(\d{4})?$'){throw 'BuildLabel must be four or eight digits'};$BuildLabel}else{$BuildDate}
$taskName='InFalsusStudioPlus_'+$taskPolicy.displayVersion+'_build'+$taskBuild
$taskZip=Join-Path $taskOutput ($taskName+'.zip')
if (Test-Path -LiteralPath $taskZip) { throw 'Release ZIP already exists; choose a new destination' }
$taskStaging=Join-Path ([IO.Path]::GetTempPath()) ('infstudio-release-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskStaging | Out-Null
$taskFolder=Join-Path $taskStaging $taskName
New-Item -ItemType Directory -Path $taskFolder | Out-Null
New-Item -ItemType Directory -Path (Join-Path $taskFolder 'Background') | Out-Null
function CopyReleaseFile([string]$Source,[string]$Relative) {
    $taskDest=[IO.Path]::GetFullPath((Join-Path $taskFolder $Relative))
    if (!$taskDest.StartsWith($taskFolder+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Release path escapes output' }
    New-Item -ItemType Directory -Path (Split-Path $taskDest -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $Source -Destination $taskDest
}
function CopyRuntimeTree([string]$Relative) {
    $taskSource=Join-Path $taskEditor $Relative
    if (!(Test-Path -LiteralPath $taskSource -PathType Container)) { throw "Missing runtime directory: $Relative" }
    foreach($taskFile in Get-ChildItem -LiteralPath $taskSource -File -Recurse) {
        $taskRelative=$taskFile.FullName.Substring($taskEditor.Length+1)
        if ($taskRelative -match '(?i)(^|\\)(Logs?|cache|ScriptCache|Backups|[^\\]*DoNotShip[^\\]*)(\\|$)' -or $taskFile.Extension -in @('.log','.pdb','.mdb','.dmp')) { continue }
        CopyReleaseFile $taskFile.FullName $taskRelative
    }
}
foreach($taskFile in @('InFalsusStudio.exe','UnityPlayer.dll','UnityCrashHandler64.exe','winhttp.dll','doorstop_config.ini','.doorstop_version')) { CopyReleaseFile (Join-Path $taskEditor $taskFile) $taskFile }
foreach($taskTree in @('InFalsusStudio_Data','MonoBleedingEdge','BepInEx\core')) { CopyRuntimeTree $taskTree }
foreach($taskTree in @('D3D12','BepInEx\patchers')) { if(Test-Path -LiteralPath (Join-Path $taskEditor $taskTree)) { CopyRuntimeTree $taskTree } }
foreach($taskTree in @('Assets','Effects','Icons','Presentation')) { CopyRuntimeTree ('BepInEx\plugins\InfStudioVisualUI\'+$taskTree) }
CopyReleaseFile $taskPlugin 'BepInEx\plugins\InfStudioVisualUI\InfStudioVisualUI.dll'
if ($taskPolicy.additionalPlugins) { foreach($taskAsset in $taskPolicy.additionalPlugins.PSObject.Properties) { CopyReleaseFile (Join-Path $taskEditor $taskAsset.Name) $taskAsset.Name } }
# Fresh loader defaults only; never copy the source editor's personal config.
$taskConfig=Join-Path $taskFolder 'BepInEx\config'
New-Item -ItemType Directory -Path $taskConfig -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $taskConfig 'BepInEx.cfg'),"[Logging.Console]`r`nEnabled = false`r`n`r`n[Logging.Disk]`r`nEnabled = false`r`n",[Text.UTF8Encoding]::new($false))
foreach($taskDoc in Get-ChildItem -LiteralPath (Join-Path $taskNode 'ExternalTools\Docs') -File -Filter '*.md') { CopyReleaseFile $taskDoc.FullName ('ExternalTools\'+$taskDoc.Name) }
$taskToolsRoot=Join-Path $taskNode 'ExternalTools\Default'
$taskToolDirs=@(Get-ChildItem -LiteralPath $taskToolsRoot -Directory | ForEach-Object Name)
if (@(Compare-Object @($taskPolicy.externalTools) $taskToolDirs).Count) { throw 'External tool source folders differ from distribution allowlist' }
foreach($taskId in $taskPolicy.externalTools) {
    $taskTool=Join-Path $taskToolsRoot $taskId
    $taskManifest=Get-Content -LiteralPath (Join-Path $taskTool 'tool.json') -Raw | ConvertFrom-Json
    if($taskManifest.id -ne $taskId) { throw 'Tool manifest ID does not match folder' }
    foreach($taskFile in Get-ChildItem -LiteralPath $taskTool -File -Recurse) {
        if($taskFile.Extension -notin @('.cs','.ps1','.json','.md') -or $taskFile.Name -eq 'pattern.json') { throw 'Unexpected tool payload' }
        CopyReleaseFile $taskFile.FullName ('ExternalTools\'+$taskId+'\'+$taskFile.FullName.Substring($taskTool.Length+1))
    }
}
$taskReadme=@"
InFalsus Studio+ $($taskPolicy.displayVersion) / build $taskBuild

解压整个目录后运行 InFalsusStudio.exe。保留全部同级文件和目录。
打开歌曲文件夹后显式保存；此包不包含个人谱面、历史、音量或快捷键设置。
支持点立得、多选复制／镜像、中键时间／位置拖动及自定义快捷键。
优化复杂谱面预览与时序面板，修复 Hold 选中变暗。
包含 v1.0.7 中英文更新公告、补全的英文界面及可开关的鼠标拖尾特效。
平滑播放显示始终启用；用户偏好存放于 AppData\Roaming\infstudio。
仅附带：紫条雨、渐变 Timing、按时序切分 SkyArea。用户可自行安装其他外置工具。
可选插件面向 InFalsus Studio Windows Mono 1.0.10。时间组/noinput/noclip 等为非官方扩展，
保存不会自动转换为官方格式；未进行目标游戏内判定/播放验收。
Forked from Arthas-0/In-Falsus-Studio. https://github.com/xandp7716-lab/infstudioplus
本包仅在本机交付，未上传 GitHub。不要覆盖已有编辑器目录或个人配置，建议解压后独立运行。
"@
[IO.File]::WriteAllText((Join-Path $taskFolder 'README.txt'),$taskReadme,[Text.UTF8Encoding]::new($false))
$taskFiles=@(Get-ChildItem -LiteralPath $taskFolder -File -Recurse | Sort-Object FullName | ForEach-Object {
    [ordered]@{path=$_.FullName.Substring($taskFolder.Length+1).Replace('\','/');bytes=$_.Length;sha256=(Hash $_.FullName)}
})
$taskManifest=[ordered]@{project=$taskPolicy.project;displayVersion=$taskPolicy.displayVersion;buildDate=$BuildDate;buildLabel=$taskBuild;assemblyVersion=$taskPolicy.assemblyVersion;editorCoreSha256=$taskPolicy.editorCoreSha256;externalTools=@($taskPolicy.externalTools);files=$taskFiles}
[IO.File]::WriteAllText((Join-Path $taskFolder 'release-manifest.json'),($taskManifest | ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($taskFolder,$taskZip,[IO.Compression.CompressionLevel]::Optimal,$true)
$taskSha=Hash $taskZip
[pscustomobject]@{zip=$taskZip;sha256=$taskSha;files=$taskFiles.Count;bytes=(Get-Item -LiteralPath $taskZip).Length;externalTools=@($taskPolicy.externalTools)} | ConvertTo-Json -Depth 4
