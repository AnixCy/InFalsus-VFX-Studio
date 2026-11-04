# 轨道与游玩 UI 透明度

编辑器及 MelonLoader 玩家端 0.8.0 支持五种逻辑场景控件。在演出视图的效果库搜索“透明度”，拖到时间轴，调节“不透明度”或添加关键帧。

| 控件 | 游戏目标 | 参数 |
| --- | --- | --- |
| 轨道透明度 | 地轨、侧轨、框架、轨道标记与判定线 | `_TrackOpacity` |
| 分数透明度 | 普通分数区域 | `_ScoreOpacity` |
| 曲名资料透明度 | 曲名、作者、难度、封面和该区域的进度条 | `_SongInfoOpacity` |
| 通关状态透明度 | 原生通关状态区域 | `_ClearStateOpacity` |
| 判定文字透明度 | 原生判定文字容器 | `_JudgementOpacity` |

每项只有一条默认可见的标量轨道。1 为保留原生不透明度，0 为隐藏，区间内可使用线性、缓动或 Bezier 曲线。多个同组实例相乘；禁用、删除或离开片段区间恢复原生表现。它们不会增加全屏后处理 Pass，也不隐藏 note、天键光标或暂停菜单；Combo 和战斗专用 UI 尚未适配。

## 预览与游戏边界

演出视图保持独立，不同时运行制谱相机。加入上述控件后，画布叠加简化轨道及 UI 参照，方便观察曲线；参照并非真实游玩场景，也没有进入 RenderTexture/视频输出。

游戏中绑定真实对象。UI 通过 Constrained2D 原生连续 alpha setter 叠加当前原生透明度，在原生 `_m._wi._ERA(bool)` 布局计算前应用、计算后恢复字段；计算结果保留，避免游戏 tween 读取已经相乘的基值。入口通过匹配游戏的 IL2CPP 元数据解析，并由 MelonLoader NativeHook 接入，不重复执行整套 UI 计算。

轨道使用范围过滤后的 Renderer、可复用私有材质和属性块。控制原生基值及恢复归属，避免连续采样越来越透明，关闭后不覆盖其他控制器的新值。0 时只关闭目标 Renderer 的显示，不关闭玩法对象。轨道未启用淡出时不更新其材质或属性块。

轨道包含数种原生 Shader。当前隔离验证覆盖标准透明材质、属性块、零值隐藏与恢复；各原生轨道材质在 0 到 1 之间的视觉一致性仍需实际进歌验收，不将材质参数检查当成完整视觉验收。

## 保存、导出与兼容

控件使用既有工程实例、片段区间、关键帧、撤销及保存流程，不改变 SPC。节点标记为 `target: postprocess`、`material: vfx/scene-opacity`，这是逻辑控制标识，不是真正的 Shader。`SceneOpacityPlan` 将它们从材质处理流程分离，并验证每种控件恰好有对应的一条 Float 通道。

工程和 addon 必须声明 `scene.opacity.v1` 及 `effect.instances`。导出器保留控件、区间和动画，不将它们烘焙成全屏图像。需要玩家端 0.8.0；旧玩家端缺少该能力时拒绝整个包，避免默默忽略隐藏效果。普通背景包仍兼容既有玩家端。

当前导出仍只接受通用背景、最多一个色彩与曝光实例，以及这五种控件。原始材质组及任意自定义 Shader 的通用导出范围没有扩大。关闭 addon、目标销毁、退场或初次加载失败时恢复原生场景；退场保留的背景不继续隐藏 HUD。

## 开发入口

- `src/Presentation/core/SceneOpacity.cs`：能力声明、分组、区间与标量组合；Unity 无关。
- `src/StudioPlusPreview/EffectLibrary.Scene.cs`、`PreviewPlugin.Scene.cs`：效果库与简化参照。
- `src/Addon.Export/BackgroundExport.cs`：导出支持列表与能力保护。
- `src/GameRuntime/NativeGameBridge.Opacity.cs`、`UnitySceneOpacity.cs`：原生目标、材质与恢复。
- `PreviewPlugin.SceneChecks.cs`、`tests/Game.Runtime.Checks/SceneChecks.cs`：编辑/导出及原生生命周期检查。

本次未修改主力游戏安装。隔离运行结果和待验收项见 [VALIDATION.md](VALIDATION.md)。
