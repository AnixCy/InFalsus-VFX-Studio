# 项目开发说明

## 定位与当前任务

InFalsus VFX Studio 是 Studio+ 的演出创作扩展。先让人可以选一段音乐、循环预览、调整效果、修改关键帧并保存，再按实际反馈补功能。当前只完成仓库准备，尚无本项目可运行的编辑界面。

既有演出求值、临时参数、编辑事务、撤销、关键帧、保存及离线 GPU 预览在独立工作区已有实现，但尚未迁入本仓库。不能把那些结果当作本仓库已能构建或已完成 Studio+ 适配。

## 三组版本

| 层次 | 基线与入口 |
| --- | --- |
| 原版编辑器宿主 | Studio Windows Mono 1.0.10；根目录 `Assets/`、`Packages/`、`ProjectSettings/` |
| 增强插件 | Studio+ 内部 1.079；公开版本 1.0.7，绑定程序集 1.0.7.0 |
| 本项目 | 开发准备阶段，尚无独立应用发行版本 |

Studio+ 来源是 `xandp7716-lab/In-Falsus-Studio-plus` 的 `feat/unofficial-editor-visual-mods`，固定提交 `2c8c63722948b8e739f0eef6ebd0478cdf8ddd3d`。所选快照在 [Mods/StudioPlusSnapshots/1.079-20261102](../../Mods/StudioPlusSnapshots/1.079-20261102/README.md)，更早快照保留作历史，不叠加安装。

## 实际源码地图

| 路径 | 用途 |
| --- | --- |
| [vfx-project.json](../../vfx-project.json) | 本仓库身份、基线与入口清单，不是演出工程格式 |
| [studio_icon_work/Source](../../Mods/StudioPlusSnapshots/1.079-20261102/studio_icon_work/Source/) | 所选增强主插件，共 160 个 C# 源文件 |
| [studio_spectrogram](../../Mods/StudioPlusSnapshots/1.079-20261102/studio_spectrogram/) | 已有独立插件适配的参考 |
| `Assets/InFalsusStudio/Runtime/` | 原版宿主与渲染模型 |
| `src/` | 后续本项目独立核心与演出插件；需要时创建，当前尚无代码 |
| `docs/vfx/` | 本项目设计、接入与验证说明 |

快照中有上游开发机路径、旧版本说明及分阶段验证记录；它们用于溯源，不是本机安装路径或当前统一状态。需要修改时先找实际调用链，核对当前源码和资源指纹。

## 构建边界

当前没有本项目的构建命令或整合包。整理文档不要求安装新的 Unity，也不修改已有编辑器。

- 根宿主工程：见保留的 [docs/BUILDING.md](../BUILDING.md)，项目设置为 Unity 6000.3.25f1 / URP 17.3。构建宿主不会自动安装增强插件。
- 增强插件：参考 [studio_icon_work/Build.ps1](../../Mods/StudioPlusSnapshots/1.079-20261102/studio_icon_work/Build.ps1)。参数指定匹配的编辑器目录和编译工具所在 Unity 目录，依赖编辑器 Managed/BepInEx 引用及原运行资源。
- 快照说明中的 Unity 2022.3.8f1c1 用于其脚本编译工具路径；不能据此把宿主项目改为 Unity 2022。新增材质/Shader 的 Bundle 需按目标运行环境另行验证。
- 运行资源见快照的分发清单；这里只保留上游已有内容，不把个人运行副本或提取媒体自动复制到仓库。

以上是入口核对，未在本次重新编译宿主或插件。

## 最小创作闭环

下一阶段按实际需要接入：一个组合背景材质、少量参数、当前播放头写关键帧、一拖一次撤销、片段循环预览、保存与重新打开。先验证单项效果，再组合转场和疾走；不先扩建通用节点图或实时 3D 场景。

时钟需要统一适配 Studio+ 的编辑预览、内置游玩与视频导出；演出使用明确的时间域和一次性偏移，不跟随全局 `Time.time` 自行累计。临时预览与已提交工程分开；导出使用已提交状态。

工程计划独立于 SPC 保存。`timinggroup`、`noinput`、`noclip`、`hidegroup` 是已有增强插件的非官方谱面扩展，不是本项目的 BG 演出格式。原版格式约束见 [docs/COMPATIBILITY.md](../COMPATIBILITY.md)，增强语义以所选快照源码及对应版本验证为准。

## 分支、检查与交付

本项目默认集成分支为 `vfx`，当前工作分支为 `codex/vfx-editor`；原 `main` 保留上游基线。常规开发从 `vfx` 建立 `codex/<任务名>`，具体提交规范见 [CONTRIBUTING.md](../../CONTRIBUTING.md)。

文档仅做路径、版本、链接和 diff 检查。本项目当前的 GPU/交互/游戏集成未验证；上游文档、独立工作区报告均不替代这些检查。玩家运行端目标为 Loader、必要运行时和预编译 Mod/资源，创作环境与玩家依赖分别管理。
