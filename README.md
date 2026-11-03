# InFalsus VFX Studio

基于 [InFalsus Studio+](https://github.com/xandp7716-lab/In-Falsus-Studio-plus) 的异象演出编辑扩展，面向音乐同步背景、Shader 特效与关键帧编排。

**开发中。已接入独立演出视图、关键帧时间轴与实时背景预览，可在独立 Studio+ 副本中测试；尚无正式发行包。视觉与实际交互待人工验收。**

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
| 独立演出视图、效果片段与关键帧 | 已实现统一时间轴、拖入/删除/排序/启停、可调轨道高度、Bezier 控制柄与独立背景栏，218 项运行检查通过 |
| 效果目录 | 常用独立效果及 11 个原始 Shader 功能条目；12 套本地材质组合归入“预设” |
| 自定义程序化效果 | 保存 Shader 自动编译更新；Properties 自动生成可打帧控件，匹配动画保留并可撤销 |
| 循环区间编辑、游戏包导出 | 待开发 |

上游源码、功能与历史检查记录不等于本项目已经完成对应集成或验收。

## 开发入口

- [项目开发说明](docs/vfx/DEVELOPMENT.md)：实际源码入口、版本关系、工作顺序与验证边界。
- [演出时间轴与效果目录](docs/vfx/TIMELINE.md)。
- [自定义 Shader 与程序化效果](docs/vfx/CUSTOM_EFFECTS.md)。
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

创作端复用 Studio+，可使用本地构建工具。玩家交付目标是受支持的 Loader、必要运行时、预编译 Mod 和资源包；玩家不需要安装编辑器或创作工具链。该目标尚未在本仓库实现或做安装验收。

演出工程独立保存为 `.vfx.json`，音频和图片按本地路径引用，不改写 SPC。官谱资源缓存只在本地生成，不进入 Git。预览、导出和游戏表现分别验证；本次验证 Studio+ 演出编辑与预览。
