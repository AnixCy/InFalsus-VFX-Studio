param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$taskRepo=Split-Path $PSScriptRoot -Parent
$taskOutput=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $taskOutput){throw 'Use a new output directory.'}
$taskParent=Split-Path $taskOutput -Parent
New-Item -ItemType Directory -Path $taskParent -Force|Out-Null
$taskStaging=Join-Path $taskParent ('.vfx-player-package-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskStaging|Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
try {
    foreach($taskLoader in @('MelonLoader','BepInEx')) {
        $taskPayload=Join-Path $taskStaging ('payload-'+$taskLoader)
        $taskPlugin=if($taskLoader-eq 'MelonLoader'){'Mods'}else{'BepInEx/plugins/InFalsusPresentation'}
        $taskLibs=if($taskLoader-eq 'MelonLoader'){'UserLibs'}else{$taskPlugin}
        $taskMap=[ordered]@{}
        foreach($taskName in @('InFalsus.Presentation.Core.dll','InFalsus.Vfx.Addon.dll','InFalsus.Presentation.Game.dll')){
            $taskMap[$taskLibs+'/'+$taskName]='src/GameRuntime/bin/Release/net6.0/'+$taskName
        }
        if($taskLoader-eq 'MelonLoader'){
            $taskMap['Mods/InFalsus.Presentation.Melon.dll']='src/GameLoader.Melon/bin/Release/net6.0/InFalsus.Presentation.Melon.dll'
            $taskChartPlugin='Mods'
        }else{
            $taskMap[$taskPlugin+'/InFalsus.Presentation.BepInEx.dll']='src/GameLoader.BepInEx/bin/Release/net6.0/InFalsus.Presentation.BepInEx.dll'
            $taskChartPlugin='BepInEx/plugins'
        }
        $taskMap[$taskChartPlugin+'/InFalsusChartLoader.dll']='src/ChartLoader/bin/'+$taskLoader+'/Release/net6.0/InFalsusChartLoader.dll'
        foreach($taskEntry in $taskMap.GetEnumerator()){
            $taskTarget=Join-Path $taskPayload $taskEntry.Key
            New-Item -ItemType Directory -Path (Split-Path $taskTarget -Parent) -Force|Out-Null
            Copy-Item -LiteralPath (Join-Path $taskRepo $taskEntry.Value) -Destination $taskTarget
        }
        $taskDocs=Join-Path $taskPayload 'InFalsusVFX'
        $taskLicenses=Join-Path $taskDocs 'licenses'
        New-Item -ItemType Directory -Path $taskLicenses|Out-Null
        Copy-Item -LiteralPath (Join-Path $taskRepo 'src/ChartLoader/LICENSE') -Destination (Join-Path $taskLicenses 'InFalsusChartLoader-MIT.txt')
        Copy-Item -LiteralPath (Join-Path $taskRepo 'src/ChartLoader/ORIGIN.json') -Destination (Join-Path $taskLicenses 'InFalsusChartLoader-source.json')
        $taskReadme=@"
InFalsus VFX 0.10.0 — $taskLoader / Windows x64 / IL2CPP

1. 先安装对应的 $taskLoader 及其要求的运行时。两种 Loader 只选一种。
2. 退出游戏，将本 ZIP 解压到游戏 EXE 同级目录，保留目录结构。
   已有同名 DLL 时先备份再替换，避免重复安装其他版本的 ChartLoader。
3. 将谱师提供的曲包 ZIP 解压到游戏 Charts 目录；确认形如 Charts/歌曲目录/if。
4. 重启游戏，进入自制谱曲包选歌。各难度的异象按曲包绑定自动加载。

玩家不需要 Python、Unity Editor、Studio、脚本或额外后台服务。
支持已适配的 1.0.7b 官方/社区二进制；SPC2 仅适用于匹配的社区壳。
尚未适配的游戏版本自动跳过异象；这不表示该版本已支持自制谱。
演出缺失或损坏时保留正常谱面与原画面。不会修改游戏二进制或原谱面。
UserData/InFalsusPresentation/config.json 首次自动生成；enabled=false 可关闭演出。

本包仅包含运行 DLL、许可与清单，不包含 Loader、游戏或曲目资源。
ChartLoader 基于 MagicNeko InFalsusChartLoader v0.2.2（MIT），本地兼容修改见本目录 licenses。
源码与适配范围：https://github.com/AnixCy/InFalsus-VFX-Studio
卸载时仅删除 package-manifest.json 中列出的本包文件；保留自己的曲包和配置。
"@
        [IO.File]::WriteAllText((Join-Path $taskDocs '使用说明.txt'),$taskReadme,[Text.UTF8Encoding]::new($false))
        $taskFiles=@(Get-ChildItem -LiteralPath $taskPayload -Recurse -File|Sort-Object FullName|ForEach-Object{
            [ordered]@{path=$_.FullName.Substring($taskPayload.Length+1).Replace([char]92,[char]47);bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
        })
        $taskManifest=[ordered]@{version='0.10.0';loader=$taskLoader;platform='Windows x64 IL2CPP';files=$taskFiles}
        [IO.File]::WriteAllText((Join-Path $taskDocs 'package-manifest.json'),($taskManifest|ConvertTo-Json -Depth 6),[Text.UTF8Encoding]::new($false))
        [IO.Compression.ZipFile]::CreateFromDirectory($taskPayload,(Join-Path $taskStaging ('InFalsus-VFX-0.10.0-'+$taskLoader+'-win-x64.zip')),[IO.Compression.CompressionLevel]::Optimal,$false)
    }
    # Publish only the two ZIPs and their hashes, not the intermediate payload trees.
    New-Item -ItemType Directory -Path $taskOutput|Out-Null
    Get-ChildItem -LiteralPath $taskStaging -Filter '*.zip' -File|ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination $taskOutput}
    Get-ChildItem -LiteralPath $taskOutput -Filter '*.zip' -File|ForEach-Object {
        [pscustomobject]@{name=$_.Name;bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
    }|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $taskOutput 'packages.json') -Encoding utf8
    Write-Output $taskOutput
}finally{
    $taskOwned=[IO.Path]::GetFullPath($taskStaging)
    if((Split-Path $taskOwned -Parent)-eq $taskParent-and (Split-Path $taskOwned -Leaf)-like '.vfx-player-package-*'-and (Test-Path -LiteralPath $taskOwned)-and ((Get-Item -LiteralPath $taskOwned).Attributes-band [IO.FileAttributes]::ReparsePoint)-eq 0){Remove-Item -LiteralPath $taskOwned -Recurse -Force}
}
