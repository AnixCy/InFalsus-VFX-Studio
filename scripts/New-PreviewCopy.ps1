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
if(Test-Path -LiteralPath (Join-Path $PluginDirectory 'Effects')){Copy-Item -LiteralPath (Join-Path $PluginDirectory 'Effects') -Destination (Join-Path $taskPlugins 'Effects') -Recurse}
$taskCustom=Join-Path $taskDestination 'CustomEffects'
New-Item -ItemType Directory -Path (Join-Path $taskCustom 'scripts') -Force|Out-Null
New-Item -ItemType Directory -Path (Join-Path $taskCustom 'templates/custom-effect') -Force|Out-Null
foreach($taskName in @('ProceduralPulse.shader','effect.json','BuildCustomEffect.cs')){
    Copy-Item -LiteralPath (Join-Path $taskRepo ('templates/custom-effect/'+$taskName)) -Destination (Join-Path $taskCustom ('templates/custom-effect/'+$taskName))
}
foreach($taskName in @('README.txt','Build-Sample.bat')){Copy-Item -LiteralPath (Join-Path $taskRepo ('templates/custom-effect/'+$taskName)) -Destination (Join-Path $taskCustom $taskName)}
Copy-Item -LiteralPath (Join-Path $taskRepo 'scripts/Build-CustomEffect.ps1') -Destination (Join-Path $taskCustom 'scripts/Build-CustomEffect.ps1')
if(Test-Path -LiteralPath (Join-Path $taskRepo 'local/custom-effect-sample/effect.bundle')){
    New-Item -ItemType Directory -Path (Join-Path $taskCustom 'SampleCompiled')|Out-Null
    foreach($taskName in @('effect.bundle','effect.json')){Copy-Item -LiteralPath (Join-Path $taskRepo ('local/custom-effect-sample/'+$taskName)) -Destination (Join-Path $taskCustom ('SampleCompiled/'+$taskName))}
}
$taskLauncher='@echo off'+"`r`n"+'setlocal'+"`r`n"+'cd /d "%~dp0"'+"`r`n"+'set "INFSTUDIO_CONFIG_ROOT=%~dp0UserData\studio"'+"`r`n"+'set "VFX_WORK_ROOT=%~dp0UserData\vfx"'+"`r`n"+'start "" "%~dp0InFalsusStudio.exe"'+"`r`n"
[IO.File]::WriteAllText((Join-Path $taskDestination 'Start-VFX.bat'),$taskLauncher,[Text.Encoding]::ASCII)
$taskGuide=@'
VFX 背景效果与拖动反馈测试版 0.6.0

双击 Start-VFX.bat，点击右上“演出视图”；左上“制谱视图”返回。
演出编辑期间停止谱面编辑输入；可直接加载本地音频和 PNG/JPEG，不必打开谱面。
效果库“效果/预设/自定义”只作筛选；所有效果与参数共用一条时间轴。
拖入效果创建5秒片段；也可双击或点击 + 添加。效果可重复添加；原始 Shader 组合按支持范围叠加。
拖动时显示跟随卡片、插入位置及时间；拖片段移动，拖右边缘改时长；右侧可修改区间、启停、名称、合成顺序或删除。
默认只有 Base Color、曝光两条参数轨道；右侧选择参数，点击 ◆ 或调整时打帧后才展开轨道。
悬停参数行底边会高亮并显示上下拖动光标；边界上下两侧均可拖，双击恢复，高度自动保存。
双击参数轨道加帧，拖菱形调整时间和数值；选帧后拖紫色柄调整过渡，或点右侧“贝塞尔”。一次拖动一次撤销。自动关键帧默认开启，首次动画以效果起点为基准。
快捷键共用制谱视图“设置→操作→热键”。默认 Space 按住预览/松开返回，Q 播放/暂停。
默认 Ctrl+S 保存，Ctrl+Shift+S 另存，Ctrl+Z 撤销，Ctrl+Y / Ctrl+Shift+Z 重做。
旧工程若首帧前有突变，点时间轴“补齐起点”；可撤销，保存才写入。开关用对应 Amount 调渐变。
“背景”只设置图片；Ephemeral、Feedback 等在效果库顶部，可搜索名称后拖入时间轴。
同组底图效果共用原始背景 Shader，不逐个增加后处理 Pass。跨组或互斥模式重叠会提示，以下方组为准。
“自定义→新建 Shader 效果”打开 Effect.shader，首次编译成功自动加入时间轴。
修改 Shader 后保存即可自动编译；Properties 自动生成右侧参数，点击 ◆ 打帧。
左侧显示当前效果和编译状态；手动编译与导入包在高级选项，编译错误保留原预览。
构建成功后预览自动更新；匹配动画保留，可撤销。源码构建需要 Unity 6000.3.25f1，使用编译效果无需 Unity。
“高级选项→导入编译包”读取 effect.json + Bundle；CustomEffects/SampleCompiled 有可直接使用的样例。
滚轮滚动轨道，Ctrl+滚轮缩放时间轴，Shift+滚轮平移；“跟随”定位播放头。
工程保存为 .vfx.json；音频与图片按原路径引用，不复制整曲。请保留媒体位置。
关闭时未保存确认仅显示一个；取消保存或保存失败不会退出。

本版为独立测试副本，旧版保留。视觉、实际操作和听感由用户验收。
尚无游戏包导出；并未复制原始演出的完整事件编排。
'@
[IO.File]::WriteAllText((Join-Path $taskDestination '使用说明.txt'),$taskGuide,[Text.UTF8Encoding]::new($false))
Write-Output $taskDestination
