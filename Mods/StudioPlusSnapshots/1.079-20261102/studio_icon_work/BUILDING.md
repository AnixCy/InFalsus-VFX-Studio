> 2026-11-01 迁移提示：当前入口为根目录《开始开发.md》。以下历史版本、路径和状态不应覆盖该入口的当前说明。

# 构建与安装 InFalsus Studio+ 1.06

## 前置条件

- Windows、PowerShell、InFalsus Studio **Windows Mono 1.0.10**。
- 匹配的 `InFalsusStudio_Data/Managed` 及 `BepInEx/core`。目标核心程序集 SHA-256 见 [兼容性说明](COMPATIBILITY.md)。
- Unity 2022.3.8f1c1 的 Roslyn / NetCoreRuntime 编译工具。参数指定 Unity 安装根目录，脚本不依赖作者机器的盘符。
- 现有整合版的 Presentation 运行资源、所需字体与依赖；仓库不再次分发游戏提取资源。原生视觉和新界面不能仅凭一个 DLL 独立运行。

在仓库根目录执行：

```powershell
.\Mods\InFalsusStudioPlus\1.06\Build.ps1 `
  -Editor 'C:\Games\InFalsusStudio_1.0.10' `
  -UnityEditor 'C:\Program Files\Unity\Hub\Editor\2022.3.8f1c1'
```

脚本编译 131 个源码文件并嵌入 `Source/BrandAssets/about-mascot.png`，输出 `Source/InfStudioVisualUI.dll`，Git 忽略此产物。程序集身份仍为 `InfStudioVisualUI`、入口 ID 仍为 `local.infalsusstudio.visualui`，用于保留既有配置与插件兼容性；项目/界面名称为 InFalsus Studio+。

## 更新已安装的整合版

1. 保存并退出目标编辑器。
2. 1.0.83 还需要配套 `Assets/side-triangles.bundle`（原生片元程序 + 坐标转换顶点程序），可用 Tools/Build-NativeTriangleShader.py 在本机生成。1.0.82 还需要配套升级 `Assets/nativevisual.bundle`、`Assets/Dependencies/8719fa86ae7f9dbe9b11654f9dc69197.bundle`、`Assets/native-display.bin` 和 `Presentation/clearstate-ap*.png`，资源指纹见 distribution.json。它们来自本体侧轨与 ClearStateContainer；不可只替换 DLL 或从旧资源包覆盖。
3. 备份原 `BepInEx/plugins/InfStudioVisualUI/InfStudioVisualUI.dll`，再放入本次构建的 DLL。保留配置、Presentation 资源、ExternalTools 和用户谱面；不同时加载独立旧 NativeVisualMod / NativeEditorUI DLL。
4. 如需更新 EXE 图标，在备份后执行：

```powershell
.\Mods\InFalsusStudioPlus\1.06\Tools\Set-AppIcon.ps1 `
  -Editor 'C:\Games\InFalsusStudio_1.0.10'
```

该脚本只更新目标 EXE 的图标资源，并检查非图标资源及非资源 PE 区段不变。它不会从源码构建完整编辑器，也不会更新你的安装器记录。

外置工具可将选定工具文件夹复制到编辑器根目录的 `ExternalTools` 下。保留原文件夹 ID；不要将 `Default` 本身作为工具目录。先备份已有同名工具，保留个人捕获数据。删除/复制 Timing 已有内置快速入口。

本节点没有自动下载依赖或发布完整整合包。底层 Unity 工程的构建仍按根目录 [上游构建说明](../../../docs/BUILDING.md)执行；仅构建底层工程不会自动安装此可选插件。

## 仅在本机制作 Windows 完整包

使用已有匹配运行环境；不会自动下载、上传或改写输入编辑器。输出为新目录及 ZIP，已存在同名产物时拒绝覆盖。

```powershell
.\Mods\InFalsusStudioPlus\1.06\Tools\Build-WindowsRelease.ps1 `
  -Editor 'C:\Games\InFalsusStudio_1.0.10' `
  -OutputRoot 'C:\Packages'
```

先运行插件构建器，或用 `-Plugin` 指定本次程序集。打包只复制运行必需目录，排除用户配置、历史、缓存、测试插件和谱面；工具严格按 [distribution.json](distribution.json) 选择。完整包及依赖不进入 Git。输出附带逐文件 SHA-256 清单及 ZIP 校验值。

内部 1.0.76 继承 1.0.74 的侧轨 Gamma 着色器依赖：匹配运行环境中须有 `BepInEx/plugins/InfStudioVisualUI/Assets/sidewall-gamma.bundle`，哈希见 `distribution.json`。着色器作者代码在 `Source/Native/Shaders/NativeSideGamma.hlsl`；提取的运行资源不提交 Git。插件公开版本与核心 ReleaseIdentity / Unity bundleVersion 分别保持 1.06 / 1.0.10。
