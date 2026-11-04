# 效果问答知识卡

这些卡片用于快速选效和回答工程问题，供自由取舍，不规定配色、效果风格或创作结构。无需先浏览外部网站或观看教程。

## 按问题定位

| 用户要的观感或问题 | 先查 |
| --- | --- |
| 建筑、直线像薄膜一样弯曲 | [底图流场 `_EphemeralAmount`](background-controls.md) |
| 图像层层嵌套，旋转、反向再复原 | [同帧嵌套 `_FeedbackAmount`](background-controls.md) + [角度 `_FeedbackRotation`](parameters.md) |
| 有清楚边界的切片、几何错位 | [Slash / Triangle Displacement](background-controls.md) |
| 放射状 RGB 分离 | [通用 `chroma`、`effect-chromatic-zoom`](effects.md)；只处理底图时查 Background Chromatic Zoom |
| 环形亮线向外扩散 | [通用 `ring`](effects.md)，给 `_RingRadius` 打关键帧；需要画面扭曲时选 `ripple` |
| 整体换色、变亮、变灰 | [色彩与曝光 `color-grade`](effects.md) |
| 明亮后显露不同细节 | [双图混合 `_BrightAmount`](background-controls.md)，需要对应 BrightMap |
| 分区域响应音乐击点 | [四路遮罩 `_ChugAmount`](background-controls.md)，XYZW 对应 RGBA，节奏需手工编排 |
| 数字故障、块状撕裂、瞬态破坏 | [独立后处理](effects.md)中的 Block/Digital Glitch、Screen Fracture |
| 某个英文参数具体做什么 | [参数卡](parameters.md)，先核对所在效果和材质组 |
| 想一次添加一组关联控制 | [组合预设](presets.md) |
| 想直接改代码 | [已知模板](custom-templates.md)和[源码工作流](../CUSTOM_EFFECTS.md) |
| 细发光曲线、径向波动、三角网格穿行 | [Wave Lines 模板](../../../templates/wave-lines/README.md)与[参数说明](custom-templates.md) |
| 不规则实体骨架、倒角亮边、建模思路与遮挡归因 | [程序结构知识卡](procedural-geometry.md)与[上方远点模板](../../../templates/perspective-cells/README.md) |
| 音频频段电平驱动参数、缓存、平滑与烘焙 | [音频驱动基础设施](../AUDIO_REACTIVE.md)，当前无界面接入 |
| 底层是否齐备、哪些尚未接入或部署 | [基础设施状态手册](../INFRASTRUCTURE.md) |
| 搬迁创作工程、保存 recipe 与原动画、避免重复烘焙 | [创作工程契约](../CREATOR_WORKSPACE.md)，当前为独立容器 |

## 回答时的确认边界

- 通用效果和已知模板的说明对照仓库 HLSL；现有原始材质说明对照已完成的控制器/编译程序研究与当前适配层。没有逐项做新的动态视觉验收。
- Neuro、若干命名纹样、特定过渡分支等保留明确的未确认说明。不要按名称把 Neuro 说成神经网络、把 Feedback 说成历史帧、把 ZOffset 说成移动相机。
- 自定义 Shader 可任意改写。同名参数不保证同义；没有已核实模板时，打开其源码或让作者说明。
- 参数滑块范围属于编辑器当前配置，不是所有 Shader 的通用单位或普遍安全范围。只有已知的 Rotation 明确以弧度解释。

## 高频工程问答

**Feedback 需要上一帧吗？** 当前嵌套实现对同一帧 UV 迭代缩放/旋转后取样底图，不需要历史帧资源。

**径向波动与光环一样吗？** 波动移动取样坐标，扭曲原图；光环叠加一条发光环带。两者可组合，但用途不同。

**Fade 能导出透明背景吗？** 当前通用 Fade 把 RGB 压黑，输出仍不透明。效果混合强度为 0 是绕过效果，也不是把整个画布变透明。

**换一张主背景图，双图/网络过渡就完整了吗？** 不一定。BrightMap、SpecialMap、Network/Cell 等辅助素材仍来自材质；新主图不自动产生对应的其他版本。

**这些效果会自动跟随音频节拍吗？** 当前采用绝对音频/谱面时钟与手工关键帧，没有自动 FFT 或节拍检测。频率、相位或 RippleBeat 参数不等于项目 BPM。

**一个效果片段就是一个 Pass 吗？** 不是。兼容的底图控制合入同一背景材质；独立后处理可能需要独立绘制，部分透明混合还需要合成。先看[渲染约定](../RENDERING.md)。

**能直接导入游戏吗？** 只按[addon 的当前支持范围](../ADDONS.md)回答。现阶段不能把整个效果库都描述为可导出，也不能把 Mod 的能力说成游戏原生谱面语句。

**怎样起步编排一次冲击？** 可先试强度 0 → 小幅峰值 → 0，再调整进入、停留和退出曲线。这是创作建议，不是固定配方；曲线、中心、色彩和强度由作者决定。

**播放开头短暂卡顿先查什么？** 先区分 UI 卡顿与预览画面卡顿。已有有限预热用于首批资源/渲染准备；仍应测实际帧时间，不把离屏渲染耗时或隐藏窗口 Update 频率当成真实 GUI FPS。

## 按标识查卡

**复杂背景能否随谱导出？** 从 0.10.0 起，原始背景控制项与关联源码的自定义背景走[通用材质导出](../BACKGROUND_MATERIAL_EXPORT.md)，不按效果名单独适配。预览与游戏共用参数/区间/关键字规则；额外屏幕后处理仍需另行接入。反馈嵌套不需要历史纹理，不能只因名字申请上一帧资源。

- [24 个通用/独立效果入口](effects.md)
- [36 个底图控制项](background-controls.md)
- [12 组材质组合](presets.md)
- [参数含义与上下文](parameters.md)
- [已知自定义模板](custom-templates.md)

## 文件与更新

编辑器注释和本套卡片共享 [effect-notes.json](effect-notes.json)。新增说明先更新此数据，再运行 `scripts/Generate-EffectReference.py`；卡片不保存图片、音频或编译材质。

实际代码入口：

- [通用后处理 HLSL](../../../src/ShaderAssets/Assets/UtilityPost.shader)
- [轻量流场/嵌套模板](../../../templates/game-addon/FlowFeedbackBackground.shader)
- [程序化脉冲模板](../../../templates/custom-effect/ProceduralPulse.shader)
- [底图控制与组关联](../../../src/StudioPlusPreview/EffectLibrary.BackgroundControls.cs)
- [预设与独立后处理关联](../../../src/StudioPlusPreview/EffectLibrary.Presets.cs)
- [合并与取样路径](../../../src/StudioPlusPreview/PreviewSurface.BackgroundControls.cs)

阅读源码或另做验证后再扩充不确定项；不要将新猜测写成已确认机制。
