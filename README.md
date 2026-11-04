# InFalsus VFX Studio

基于 [InFalsus Studio+](https://github.com/xandp7716-lab/In-Falsus-Studio-plus) 的异象演出编辑扩展，面向音乐同步背景、Shader 特效与关键帧编排。

**开发中。已接入独立演出视图、关键帧时间轴与实时背景预览，可在独立 Studio+ 副本中测试；尚无正式发行包。视觉与实际交互待人工验收。**

编辑器补丁 0.10.1 修复效果筛选等文本框在改绑删除快捷键后无法 Backspace/Delete 的问题。玩家运行包仍为 0.10.0，无需随此次文字输入修复更新。

## 创作目标

复用 Studio+ 的歌曲、音频、波形与谱面预览，提供音频区间选择、循环播放、特效参数编辑、关键帧编排和独立工程保存。功能按实际创作需求逐步扩展。

视觉演出采用背景平面、Shader 与参数时间轴。参考资料用于技术与美术研究，不限定视觉风格。

## 当前状态

| 内容 | 状态 |
| --- | --- |
| 本项目名称、仓库与开发规范 | 已整理 |
| Studio+ 1.079 增强插件源码 | 已保留为开发基线 |
| 原版 Studio 1.0.10 Unity 工程 | 继承上游 |
| 演出求值、参数预览、编辑事务、撤销与可靠保存 | 已迁入 `src/Presentation`，196 项检查通过 |
| 独立演出视图、效果片段与关键帧 | 已实现统一时间轴、拖入/删除/排序/启停、可调轨道高度、Bezier 控制柄与独立背景栏；支持连续标尺定位、对应检查器、持久化的 2K/1080p/720p 预览选择，310 项基础运行检查通过 |
| 效果目录 | 11 个原始后处理功能、36 个底图控制项、5 个轨道/UI 透明度控件；原始组合统一放入效果库，支持名称搜索；101 个内置入口有简述、悬停说明与参数帮助 |
| 自定义程序化效果 | 保存 Shader 自动编译更新；Properties 自动生成可打帧控件并可筛选参数，匹配动画保留并可撤销 |
| 可编辑 Shader 演示 | Perspective Cells 的程序化背景效果基线已获用户认可；保留旧原型与可编辑编排。后续视觉与操作变更仍需验收 |
| 音频驱动基础设施 | 离线频段 RMS、checksum/原子磁盘缓存、包络、标量调制及批量曲线烘焙；独立数值/demo 夹具验证，无编辑器界面或播放器接入 |
| 创作工程基础设施 | 独立 `.vfxwork.json` 容器、声明资源复制/搬迁、稳定轨道与音频 recipe 关联、原人工动画/派生结果分离；尚未接入 Studio 文件管理 |
| 通用背景材质导出 | 0.10.0 按资源/参数契约导出原始背景、背景控制项及有源码的自定义背景；预览与游戏共用求值，同源项合并、按区间切换材质，保持单背景绘制，无新增全屏处理或历史帧 |
| 完整曲包导出 | 0.9.0 从已保存 Studio 歌曲工程编译全部有效难度，或读取可游玩曲包；ZIP 携带音频、曲绘和按难度绑定的演出，23 项文件检查、13 项编译检查及 324 项 Studio 运行检查通过 |
| 游戏加载与回退 | MelonLoader / BepInEx IL2CPP 共用随谱演出，首次安装运行包，之后曲包解压 Charts；两入口各 52 项隔离原生生命周期检查通过，含 ICP1/SPC2 读取，实际进歌、视觉与帧时间待验收 |
| 循环区间编辑、完整效果游戏导出 | 待开发 |

上游源码、功能与历史检查记录不等于本项目已经完成对应集成或验收。

## 开发入口

- [项目开发说明](docs/vfx/DEVELOPMENT.md)：实际源码入口、版本关系、工作顺序与验证边界。
- [基础设施状态与接入顺序](docs/vfx/INFRASTRUCTURE.md)：可复用接口、尚未接入的能力、无人工验收时的推进边界。
- [可搬迁工程与来源保存](docs/vfx/CREATOR_WORKSPACE.md)：资源清单、独立容器、recipe/原动画关联和烘焙失效。
- [渲染与效果组合](docs/vfx/RENDERING.md)。
- [轨道与游玩 UI 透明度](docs/vfx/SCENE_OPACITY.md)：五组可打帧控件、简化参照与游戏适配范围。
- [效果问答知识卡](docs/vfx/reference/index.md)：按观感选效、参数速查、实现边界；与编辑器注释共用数据。
- [可选游戏 addon](docs/vfx/ADDONS.md)：导出范围、玩家包与隔离验证。
- [完整曲包与安装](docs/vfx/CHART_PACKAGES.md)：Studio 源工程到歌曲 ZIP、两种 Loader 一次安装、兼容范围与失败回退。
- [通用背景材质导出](docs/vfx/BACKGROUND_MATERIAL_EXPORT.md)：复杂背景 Shader、共享参数求值、制作端目标构建与渲染阶段边界。
- [演出时间轴与效果目录](docs/vfx/TIMELINE.md)。
- [自定义 Shader 与程序化效果](docs/vfx/CUSTOM_EFFECTS.md)。
- [音频驱动参数基础设施](docs/vfx/AUDIO_REACTIVE.md)：PCM 接口、缓存、时间对齐、包络与烘焙契约。
- [参数预览使用说明](docs/vfx/PARAMETER_PREVIEW.md) · [本次验证范围](docs/vfx/VALIDATION.md)。
- [可复用 UI 组件与接入边界](docs/vfx/UI_COMPONENTS.md)。
- [贡献规范](CONTRIBUTING.md) · [Agent 工作规范](AGENTS.md) · [本项目更新记录](CHANGELOG.md)。
- [基线清单](vfx-project.json)：固定提交、版本和源码路径。
- [Studio+ 1.079 快照](Mods/StudioPlusSnapshots/1.079-20261102/README.md)。

`vfx` 是本项目集成及默认分支；功能分支使用 `codex/<任务名>`，当前入口为 `codex/vfx-editor`。`main` 和继承的其他分支保留上游历史；更新上游时先读取差异再合入本项目。

## 上游与构建

本项目 fork 自 **xandp7716-lab/In-Falsus-Studio-plus**，后者基于 **Arthas-0/In-Falsus-Studio**。开发基线是 Studio+ 内部版 **1.079**，固定提交 `2c8c63722948b8e739f0eef6ebd0478cdf8ddd3d`；其公开界面和程序集绑定版本仍为 1.0.7 / 1.0.7.0，宿主为 Windows Mono 编辑器 1.0.10。

仓库根目录的 Unity 工程仍属于宿主。直接构建它不会自动得到 Studio+ 增强插件或本项目演出功能。本项目独立插件通过 `scripts/Build-Preview.ps1` 构建，再由 `scripts/New-PreviewCopy.ps1` 创建测试副本，步骤见[开发说明](docs/vfx/DEVELOPMENT.md)。

当前运行验证使用已有 **Studio+ 1.0.7 build1030** 二进制副本及匹配的 Unity 6000.3.25f1。1.079 快照仍是后续适配的源码基线，本次未重建或验收其完整主插件。

保留的[上游 README](README.upstream.md)、[上游更新记录](CHANGELOG.upstream.md)和[素材来源记录](THIRD_PARTY_NOTICES.md)用于溯源。上游旧版本说明与当前增强插件有差异时，以明确标注版本的源码、基线清单及实测结果为准。

## 创作端与玩家端

创作端复用 Studio+，配套私有谱面编译工具；谱师不必安装 Python。玩家首次安装一个匹配的 MelonLoader / BepInEx IL2CPP 和对应运行 ZIP，之后直接将完整曲包解压到 Charts；不需要编辑器、创作工具链或逐曲脚本。当前只适配明确指纹的游戏版本，完整真实游玩验收尚未完成。

演出工程独立保存为 `.vfx.json`，源工程与媒体不被改写；完整曲包在新位置生成二进制 SPC 和相对资源。官谱资源缓存只在本地生成，不进入 Git。预览、导出和游戏表现分别验证，不包含真实进歌或视觉验收。
