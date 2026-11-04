# 可选游戏演出包

0.9.0 默认导出随歌曲分发的完整 ZIP，玩家放入 Charts 即可，操作和安装见 [CHART_PACKAGES.md](CHART_PACKAGES.md)。旧独立 addon 导出继续保留；下文描述演出自身的契约与支持范围。0.7.0 接通背景材质，0.8.0 增加可选轨道和四组游玩 UI 透明度。不安装或关闭演出时，兼容谱面仍可独立游玩。

## 当前范围

0.10.0 增加[通用背景材质路径](BACKGROUND_MATERIAL_EXPORT.md)：原始背景及其控制项、自定义背景 Shader 与暴露参数动画可随曲导出，不按具体效果名逐个适配。预览与玩家共用材质选择/参数规则；背景项合并或切换一个材质，不新增逐项全屏 Pass。

支持通用背景图片、亮度、颜色、基础 UV 参数，以及最多一个“色彩与曝光”实例。底色、曝光、对比度、饱和度和混合强度合并进背景 Shader，实例区间转换为运行参数，不额外添加全屏处理。保留关键帧、缓动、时间域和一次偏移；只导出已提交状态。

另支持轨道、分数、曲名资料、通关状态和判定文字的透明度实例，保留区间与动画，不增加全屏 Pass，需要玩家端 0.8.0。详见 [SCENE_OPACITY.md](SCENE_OPACITY.md)。通用背景材质包需要 0.10.0；额外屏幕后处理和不兼容的渲染阶段仍会阻止导出并说明原因，停用效果不导出。独立演出文件夹不含源 SPC、音乐、草稿、撤销记录和布局；完整曲包包含已编译谱面和声明媒体。PNG/JPEG 最大 16 MiB，宽高各不超过 4096；先检查尺寸头再解码。

导出器按 UTF-8 字节数检查 `runtime.json` 和 `addon.json`，分别不得超过玩家端既有的 4 MiB 限制；超限则拒绝发布并清理自有暂存目录。该保护涵盖所有合并曲线，不只单条音频驱动曲线；不自动降低精度或截断。0.8.0 安装包含此检查。

游戏入口支持 MelonLoader IL2CPP 与 BepInEx Unity IL2CPP，资源目标为 Unity 6000.3.9f1 / Windows x64。已核对适配内置于运行库；其他游戏二进制须单独适配，不宣称支持所有官方或社区版本。Studio 的 Mono 插件不能直接装入 IL2CPP 游戏。

## 使用

在演出视图点击“导出曲包”，默认完整歌曲输出。需要旧方式时点击“独立演出…”，选择目标谱面和输出父目录；每次创建新文件夹，不覆盖已有包。开发者预先准备插件的 `GameExport/` 游戏资源，独立演出模式另需目标清单。

```text
ExampleAddon/
  addon.json          # 版本、运行清单路径、文件 SHA-256
  runtime.json        # 运行数据、曲目/难度与可选谱面指纹
  background.bundle   # 匹配游戏目标的预编译材质
  background.png      # 选了图片时才有
```

将整个文件夹放入 `UserData/InFalsusPresentation/Addons/` 后重启游戏。玩家只需一个匹配的 Loader、必要运行时、通用演出 DLL 和 addon；无需编辑器、Unity、Python 或后台服务，每曲包不加载任意用户 C#。当前没有自动安装器，不修改游戏二进制。

`config.json` 示例：

```json
{ "enabled": true, "reloadSerial": 0 }
```

关闭 `enabled` 后恢复原材质并释放自有资源。增加 `reloadSerial` 显式重读 addon；正常游玩帧不重算资源哈希。配置约每半秒检查一次。目标目录是生成时的快照；游戏或自制谱变化后需要重新准备匹配数据。

## 回退与所有权

- 文件缺失、校验失败、版本/能力不兼容或初次加载失败：跳过演出，保留原画面。
- 有效演出更新失败：保持最近有效版本；删除整个包后不再继续使用它。
- 同谱面匹配多个包：使用原画面并限频提示，不猜测优先级。
- 使用私有材质，关闭、离开和应用退出时清理；其他控制器接管材质后不恢复旧绑定。
- C2D 应用材质会清空待应用字段，归属同时检查实际生效材质。正常关闭延迟两帧释放资源，等待原生绑定更新。
- 原生特殊演出默认拒绝接管；轨道及指定 HUD 仅支持透明度，位移、相机、Combo/战斗 UI 和全屏处理尚未进入导出支持列表。

## 开发入口

| 路径 | 职责 |
| --- | --- |
| `src/Addon` | 玩家与编辑器共享契约、校验、图片头检查；不依赖创作层 |
| `src/Addon.Export` | 编辑工程转换、基础效果融合与原子发布；不进入玩家包 |
| `src/GameRuntime` | 已迁入的 IL2CPP 适配、包选择、材质资源与恢复 |
| `src/GameLoader.Melon` | Loader 入口及练习定位观测 |
| `src/GameLoader.BepInEx` | Bep IL2CPP 入口、原生 UI 与练习定位钩子 |
| `src/ChartLoader` | 随运行包安装的 MIT 谱面加载组件 |
| `templates/game-addon` | 自有背景 Shader 和资源构建器 |

`Build-GameRuntime.ps1 -GameReferenceRoot '<匹配的参考游戏>'` 只构建 DLL，不安装到参考游戏。`Build-GameAddonAssets.ps1` 用匹配 Unity 构建资源；`Prepare-GameExport.py` 按实测 Unity/色彩空间准备制作端缓存。脚本不内置开发机路径。

资源构建可选 `-ShaderSource '<背景 Shader>'`；`-GenericMaterial` 支持符合单背景契约的 Shader，可无图像入口。自定义源码使用内容标识生成独立内部 Bundle 名称和默认输出目录，避免覆盖通用背景资源或与已加载包重名。Studio 0.10.0 从关联的源码自动调用制作端目标构建；任意全屏 Shader 不会自动改写为背景 Shader。

`templates/game-addon/FlowFeedbackBackground.shader` 提供轻量流场与嵌套缩放/旋转参考：一次背景图片采样，可独立驱动三个强度/旋转参数和变换中心，不使用历史帧或附加全屏 Pass。没有性能帧时间验收，单次采样也不等于零成本。

游戏背景模板采用原背景的 Geometry 队列、LEqual 测试与深度写入，并响应 Constrained2D 的 `_Color`。不能照搬离线全屏画布的 `ZTest Always / ZWrite Off`，否则会改变背景对远处透明物件的遮挡。背景替换不调整 Note 的淡入系数或生成距离。

玩家端在 transport 拆除后，只为仍可见、身份未变的原场景保留最后一帧；不在这一阶段加载新包。目标隐藏、销毁或切换时清理，显式关闭始终优先。不使用固定延时猜测转场长度，日志用 `Transition` 标识保留阶段。

`New-GameSandbox.py` 创建新的物理副本，使用独立产品名和可执行文件名。`Check-GameAddonRuntime.ps1` 只接受有标记的副本；使用真实 IL2CPP/C2D 与合成时钟检查，不模拟键鼠或进入歌曲。测试插件不进入手动验收启动目录。

实际进歌、原生暂停/返回选曲、视觉一致性和全尺寸帧时间仍待人工验收，详见[验证记录](VALIDATION.md)。
