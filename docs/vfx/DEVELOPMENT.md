# 项目开发说明

## 当前实现

InFalsus VFX Studio 是 Studio+ 的演出创作扩展。现已迁入共享求值、临时参数、事务、撤销和保存核心，并接入常量参数编辑与实时背景预览。使用方法见[参数预览说明](PARAMETER_PREVIEW.md)。

新增代码位于 `src/`，没有改写根宿主工程或增强插件快照。核心有关键帧能力，当前界面尚无关键帧编辑、循环区间编辑或游戏包导出。既有离线渲染器和游戏适配器未在本次迁入。

## 版本与源码

| 层次 | 基线与入口 |
| --- | --- |
| 原版编辑器宿主 | Studio Windows Mono 1.0.10；根目录 `Assets/`、`Packages/`、`ProjectSettings/` |
| 增强插件源码 | Studio+ 内部 1.079；公开版本 1.0.7，绑定程序集 1.0.7.0 |
| 本次运行基线 | 已有 Studio+ 1.0.7 build1030 完整应用；未重建完整 1.079 主插件 |
| 本项目 | 参数预览插件 0.1.2 原型，尚无正式应用发行版本 |

Studio+ 源码固定于提交 `2c8c63722948b8e739f0eef6ebd0478cdf8ddd3d`，快照在 [Mods/StudioPlusSnapshots/1.079-20261102](../../Mods/StudioPlusSnapshots/1.079-20261102/README.md)。历史快照用于溯源，不叠加安装。

| 路径 | 用途 |
| --- | --- |
| [vfx-project.json](../../vfx-project.json) | 仓库身份、基线指纹与构建入口，不是演出工程格式 |
| [src/Presentation](../../src/Presentation/README.md) | Unity 无关的共享核心与创作层；来源文件哈希记录在 `ORIGIN.json` |
| [src/StudioPlusPreview](../../src/StudioPlusPreview/) | BepInEx/Harmony 适配、时钟、参数面板、项目文件和渲染资源生命周期 |
| [src/ShaderAssets](../../src/ShaderAssets/) | 独立 Unity Shader 资源工程，不使用上游根工程构建 |
| [tests](../../tests/) | 67 项参数检查、117 项创作工程检查及通用契约夹具 |
| [scripts](../../scripts/) | 插件与资源构建、独立副本生成、静音运行检查 |
| [studio_icon_work/Source](../../Mods/StudioPlusSnapshots/1.079-20261102/studio_icon_work/Source/) | 所选增强主插件的 160 个 C# 源文件 |

快照中的开发机路径与历史检查不是本机安装指令。需要修改宿主或增强插件时，先核对实际调用链和文件指纹。

## 构建参数预览

构建环境：Windows PowerShell、.NET SDK 9、Unity Editor **6000.3.25f1**，以及匹配基线的完整 Studio+ Windows Mono 应用目录。运行测试副本不需要 SDK 或 Unity Editor。

以下路径是占位示例，替换为本机完整应用、Unity 编辑器与一个尚不存在的测试目录：

```powershell
& .\scripts\Build-Preview.ps1 `
    -StudioPath '<完整 Studio+ 目录>' `
    -UnityEditor '<Unity 6000.3.25f1 的 Unity.exe>'

& .\scripts\New-PreviewCopy.ps1 `
    -StudioPath '<同一完整 Studio+ 目录>' `
    -Destination '<新的测试目录>'
```

默认构建输出为 `local/build/preview`，包含三个项目 DLL 和 `preview.bundle`。`-SkipShader` 仅用于资源已构建且未变化时的 C# 重建。脚本核对宿主核心 SHA-256，Shader 构建还核对 Unity 版本、编译错误、Bundle 可加载性和资源入口。

生成脚本复制完整运行依赖，排除原日志和常用缓存/用户数据，添加项目插件与 `Start-VFX.bat`；目标必须不存在，不覆盖原安装。启动入口设置副本自己的配置目录。该副本为本地测试用途，脚本不代表获得了重新分发全部依赖和素材的许可。

只编译根 Unity 工程不会得到本项目插件。根宿主构建见保留的 [docs/BUILDING.md](../BUILDING.md)；完整增强主插件的构建仍参考快照中的 `Build.ps1`，本次没有重建它。

## 验证命令

在仓库根目录执行：

```powershell
dotnet run --project tests/Authoring.Checks/Authoring.Checks.csproj -c Release -- .
dotnet run --project tests/Project.Checks/Project.Checks.csproj -c Release -- .

& .\scripts\Check-PreviewRuntime.ps1 `
    -PreviewPath '<已生成的测试副本>' `
    -OutputDirectory '<新的检查输出目录>' `
    -PluginDirectory '.\local\build\preview'
```

前两项不启动 Unity。第三项显式启动并退出独立 Studio+ 副本，静音、不播放 transport，在真实 GPU 上读取数值；不会启动游戏。需要可用图形设备，不能使用 `-nographics`。检查报告与配置保存到所选输出目录，不进入源码。

本次结果和未验收范围见 [VALIDATION.md](VALIDATION.md)。实际操作和视觉由人工验收，自动检查不替代它们。

## 接入约定

- GUI 只控制 `PreviewModel`；数据求值和事务属于共享层，渲染资源属于 `PreviewSurface`，宿主时间与对话框属于 `StudioBridge`。
- `StudioUiTheme` 复用当前宿主的 UI 绘制组件和皮肤；字体资源由宿主管理，插件不复制或销毁它们。组件清单和草稿隔离边界见 [UI_COMPONENTS.md](UI_COMPONENTS.md)。
- 预览使用经 Studio+ 适配的显示时刻，另保留音频偏移信息，避免二次偏移；SV 不是时钟。时间驱动 Shader 不自行累计 `Time.time`。
- 参数拖动使用临时覆盖，松手一次提交；取消恢复原值。背景材质和 Bundle 在参数变化时复用，不逐帧加载。
- 退出确认使用单个非阻塞界面，原生文件操作由动作队列互斥执行。`Application.Quit` 延后处理，授权最多消费一次，其他订阅者阻止退出时未用授权会过期；不要在退出回调内启动消息循环或弹窗。
- 演出工程独立于 SPC；原编辑器的谱面保存逻辑不变。`timinggroup`、`noinput` 等已有扩展不是本项目的演出格式。
- 视频导出时钟通过可选反射适配；导出采样排除临时值，但完整视频导出仍待验证。玩家运行包和创作端依赖分别管理。

默认集成分支是 `vfx`，开发分支使用 `codex/<任务名>`。提交及来源要求见 [CONTRIBUTING.md](../../CONTRIBUTING.md)。
