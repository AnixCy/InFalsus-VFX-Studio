> 当前源码内部版号 **1.075**（2026-11-01），149 个 C# 文件。公开界面版本仍 1.0.7、程序集绑定版本仍 1.0.7.0；AssemblyInformationalVersion 为 1.075。下文历史版本说明不能作为当前源码数量或验证状态。当前源码快照见 upstream/Mods/StudioPlusSnapshots/1.075-20261101。

# InFalsus Studio+ 1.0.7

`infstudioplus` 的当前整合插件源码节点，内部程序集版本 **1.0.7.0**，面向 **InFalsus Studio Windows Mono 1.0.10**。136 个 C# 源文件与当前安装版本的源码逐一校验一致；源码哈希见 [SOURCE_MANIFEST.json](SOURCE_MANIFEST.json)。

本次发布版本为 1.0.7：移除平滑播放开关并始终启用，补全英文翻译，加入中英文更新公告。Windows release 另附既有独立鼠标拖尾插件，按 distribution.json 中的文件指纹分发。当前验证与安装记录见工作区《制谱器开发交接.md》最新条目。

该节点包含原生视觉、现代界面、编辑与预览优化、时间组、快速工具、可修改热键及通用外置工具接口。复制粘贴保留各物件的 SkyArea 组号和时间组；起止端点支持时间/位置拖移模式；切窗后恢复把手与点立得输入。EXE 图标及制谱器图标描边资源随源码保留。

这是一份源码交付，不包含编译 DLL、修改后的 EXE、BepInEx、游戏提取资源、字体、整合 ZIP、个人设置或谱面。完整运行仍需要匹配的编辑器及原有运行资源。[构建与安装说明](BUILDING.md) · [兼容性](COMPATIBILITY.md) · [外置工具接口](EXTERNAL_TOOLS.md) · [验证边界](VALIDATION.md)。

## 工具分发

- `ExternalTools/Default` 仅分发 MakeRain（紫条雨）、GradientTiming（渐变 Timing）、TimingSplit（按时序切分 SkyArea）。后续 GitHub 提交和 Windows 包均按 [分发清单](DISTRIBUTION.md)执行。
- 删除/复制 Timing 保留在程序内置快速工具中。升级时不删除用户已安装的其他外置脚本。
- 加入新工具时，使用 `output: spc` 通用接口输出语句或批量补丁，无须在编辑器内增加工具 ID 分支。未知语句的保留不等于预览器已经实现它的游戏语义。

## 版本边界

公开界面版本为 1.0.7，程序集版本为 1.0.7.0，底层编辑器仍为上游 1.0.10。这些版本号分别记录，避免把插件源码快照误认为重建过的 Unity 编辑器核心。

Forked from [Arthas-0/In-Falsus-Studio](https://github.com/Arthas-0/In-Falsus-Studio)。时间组为非官方壳子扩展；目标游戏内判定和播放没有在本次源码提交中验证。
