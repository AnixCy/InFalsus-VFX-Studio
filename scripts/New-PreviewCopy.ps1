param(
    [Parameter(Mandatory=$true)][string]$StudioPath,
    [Parameter(Mandatory=$true)][string]$Destination,
    [string]$PluginDirectory
)
$ErrorActionPreference='Stop'
$taskRepo=Split-Path $PSScriptRoot -Parent
if(-not $PluginDirectory){$PluginDirectory=Join-Path $taskRepo 'local/build/preview'}
$taskSource=(Resolve-Path -LiteralPath $StudioPath).Path.TrimEnd('\')
$taskDestination=[IO.Path]::GetFullPath($Destination).TrimEnd('\')
if(Test-Path -LiteralPath $taskDestination){throw 'Destination already exists; existing installations are never overwritten.'}
if($taskDestination.StartsWith($taskSource+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Destination cannot be inside the source installation.'}
if(-not (Test-Path -LiteralPath (Join-Path $taskSource 'InFalsusStudio.exe'))){throw 'Studio executable missing.'}
$taskRequired=@('InFalsus.Vfx.StudioPlus.dll','InFalsus.Presentation.Core.dll','InFalsus.Presentation.Authoring.dll','preview.bundle')
foreach($taskName in $taskRequired){if(-not(Test-Path -LiteralPath (Join-Path $PluginDirectory $taskName))){throw ('Build output missing: '+$taskName)}}
New-Item -ItemType Directory -Path $taskDestination | Out-Null
Get-ChildItem -LiteralPath $taskSource -Recurse -File | ForEach-Object {
    $taskRelative=$_.FullName.Substring($taskSource.Length+1)
    if($taskRelative -match '^(VisualUIData|UserData|BepInEx\\cache)\\' -or $_.Extension -eq '.log'){return}
    $taskTarget=Join-Path $taskDestination $taskRelative
    New-Item -ItemType Directory -Path (Split-Path $taskTarget -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $_.FullName -Destination $taskTarget
}
$taskPlugins=Join-Path $taskDestination 'BepInEx/plugins/InFalsusVfx'
New-Item -ItemType Directory -Path $taskPlugins -Force | Out-Null
foreach($taskName in $taskRequired){Copy-Item -LiteralPath (Join-Path $PluginDirectory $taskName) -Destination $taskPlugins}
$taskLauncher='@echo off'+"`r`n"+'setlocal'+"`r`n"+'cd /d "%~dp0"'+"`r`n"+'set "INFSTUDIO_CONFIG_ROOT=%~dp0UserData\studio"'+"`r`n"+'set "VFX_WORK_ROOT=%~dp0UserData\vfx"'+"`r`n"+'start "" "%~dp0InFalsusStudio.exe"'+"`r`n"
[IO.File]::WriteAllText((Join-Path $taskDestination 'Start-VFX.bat'),$taskLauncher,[Text.Encoding]::ASCII)
$taskGuide=@'
VFX 参数预览测试版
0.1.2：演出面板和确认框复用 Studio+ 扁平 UI 组件。

双击 Start-VFX.bat。使用该入口可将测试设置保存到本目录 UserData。
右上角“演出参数”打开面板；可使用内置背景或选择本地 PNG/JPEG。
拖动滑杆实时更新，数值可输入后回车应用，Esc 取消输入。
通过 Studio+ 原有进度条定位；演出时间跟随谱面显示时刻，暂停时定格。
参数支持撤销、重做、恢复默认、工程保存、另存和打开。
关闭或切换工程时，未保存确认仅显示一个；可保存、放弃或取消。取消保存或保存失败不会退出。

本版仅编辑内置材质的常量参数，尚无关键帧时间轴或游戏包导出。
图片按原路径引用，不复制进工程；请保留原图片位置。
测试目录与既有安装分开，视觉与实际交互由用户验收。
'@
[IO.File]::WriteAllText((Join-Path $taskDestination '使用说明.txt'),$taskGuide,[Text.UTF8Encoding]::new($false))
Write-Output $taskDestination
