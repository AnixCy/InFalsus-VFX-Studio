# InFalsus Studio+ 内部源码 1.079

2026-11-02，公开版本 1.0.7，绑定版本 1.0.7.0。非官方目标 InFalsusModded1.07b，编辑器 Windows Mono 1.0.10。

当前160个主插件C#源文件及独立频谱模块。包含独立FMOD游玩音频、事件时间判定、天空音效和Hold断手修复、频谱缓存及参数解耦、OGG剪歌等当前修改。详见docs和feature_work中的结果记录。

构建：studio_icon_work/Build.ps1 -Editor <编辑器目录> -UnityEditor <Unity2022.3.8f1c1目录>。需要匹配Managed/BepInEx引用。FMOD使用官方Core 2.03.09运行库，分发路径及指纹见distribution.json；本仓库不上传运行库、本体提取资源或安装包。嵌入资源沿用既有源码快照。

仅更新源码内部标记并做编译检查，不替换副本。25项天空音效/Hold检查对应版本标记修改前同逻辑二进制；不冒充本次重新界面验收。无个人配置、歌曲、缓存、日志、外置可执行工具。

原SPC及非官方timinggroup语义保持，兼容边界见COMPATIBILITY.md。
