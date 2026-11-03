# In Falsus Studio

**面向音乐游戏 In Falsus 的社区谱面编辑器。** 使用 Unity 开发，提供六轨与空域音符编辑、谱面预览、歌曲工程管理和检查工具。

当前源码基线：**1.0.10，包含 Flick 尾部修正**。维护者：Arthas。本仓库是编辑器工程，不是游戏本体，也不代表官方开发团队。

[更新记录](CHANGELOG.md) · [1.0.10 更新公告](docs/releases/1.0.10.md) · [构建指南](docs/BUILDING.md) · [贡献与提交规范](CONTRIBUTING.md) · [格式兼容性](docs/COMPATIBILITY.md)

非官方编辑器运行时扩展源码见 [Mods](Mods/README.md)；插件不随 Unity 编辑器核心默认启用。

## 当前功能

- 编辑 Tap、Hold、SkyArea、Flick；属性与原始语句编辑，多选、完整段落选择、撤销和重做。
- 四难度歌曲工程，主音乐与曲绘识别，歌曲预览片段、谱师及曲绘画师信息；重新打开时检查资源变化。
- 节拍网格、时序事件、波形、循环和书签；减速播放保持音调。
- Sky 等分、按网格切分及转为 Flick，左右边界采样；采样坐标最多 128 份，提供小数修复与大分母压缩。
- Sky 首尾连接允许 ±2ms，仍要求横向接触；可显式自动分组或清除全谱组号。
- 谱面检查：未知语句、Tap/Hold/Flick 叠键，以及持续时间小于 5ms 的 Hold/Sky，支持跳转和选择。
- 显式保存时对已识别时间语句稳定排序，保留未知记录、注释、源标识与文本布局。
- Windows 现代文件夹选择器及成功打开路径记忆；每版首次启动显示最近三版公告，也可在设置中打开。

## 快速开始

1. 安装 **Unity 6000.3.25f1**，并安装 Windows 构建支持。
2. 克隆仓库，在 Unity Hub 中打开仓库根目录（这里直接包含 `Assets`、`Packages`、`ProjectSettings`）。

   ```powershell
   git clone https://github.com/Arthas-0/In-Falsus-Studio.git
   ```

3. 等待包恢复和编译。通过 `Tools → InFalsus Studio → Open Demo Scene` 打开应用场景，进入 Play。
4. 点击“打开歌曲”，选择包含谱面与 MP3/OGG 的歌曲目录。多首主音乐或多张曲绘时按提示选择。
5. 编辑后点击“保存”或按 **Ctrl+S**。参数确认、切换难度和退出都不等于保存；已有工作谱不会在打开时被全量重导。

独立 Windows 程序需要从源码构建，步骤见[构建指南](docs/BUILDING.md)。运行时保留完整构建目录，不能只复制 EXE。个人歌曲和 Unity 音频导入缓存不包含在仓库中。

## 开发环境

| 项目 | 当前配置 |
| --- | --- |
| Unity | 6000.3.25f1 |
| 渲染管线 | URP 17.3.0 |
| 运行时 UI | IMGUI，使用 C# 绘制 |
| 发布目标 | Windows x64 / Mono |
| GUI 设计尺寸 | 1920×1080；舞台相机标定另用 2048×1152 |
| 序列化 | Force Text，保留 `.meta` GUID |

依赖版本以 `Packages/manifest.json` 和 `Packages/packages-lock.json` 为准。首次导入需要 Unity 包服务；不要为运行本工程顺手升级依赖或重设相机。

## 源码导航

| 目录 | 内容 |
| --- | --- |
| `Assets/InFalsusStudio/Runtime/Core` | 无损谱面模型、歌曲工程、编辑事务、时间轴、检查和回归用例 |
| `Assets/InFalsusStudio/Runtime/StudioBootstrap*.cs` | 应用协调、IMGUI 面板、输入、资源加载与手动保存 |
| `Assets/InFalsusStudio/Runtime/Rendering` | 舞台、音符网格、材质和拾取 |
| `Assets/InFalsusStudio/Editor` | Unity 菜单、构建入口、真实几何检查及回归数据 |
| `Assets/InFalsusStudio/Resources` | 必需的 Shader、图集、HUD、音效与默认配置 |
| `docs` | 开发说明、兼容性、验证记录和公告 |

详细入口与开发约束见[架构说明](docs/ARCHITECTURE.md)。现有验证结果见[验证记录](docs/VALIDATION.md)；编译和几何检查不能代替实际界面、GPU 或听感验收。

## 提交非官方壳子功能时必须注明

**任何包含非官方壳子功能的 commit，都必须在标题中带 `[非官方壳子]`，并说明适用壳子和版本。** PR 与更新公告也必须标注。

例如，群友修改游戏本体后增加的 `timinggroup` 属于非官方扩展。当前 1.0.10 **没有实现该扩展**，不能将它描述为原版 In Falsus 参数。它也不是 SkyArea 现有的 `groupid`。

```text
feat(timinggroup): [非官方壳子] 支持某壳子的独立时间组
```

提交正文需说明扩展语法、官方兼容性、启用方式、官方格式导出行为和测试情况。完整要求与示例见[贡献规范](CONTRIBUTING.md#非官方壳子扩展必填)。

## 素材与授权记录

素材来源说明见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。代码、原游戏素材与参考谱面的授权范围不能混用；本次仓库初始化没有为历史素材追加授权，也未选定整个项目的统一开源许可证。
