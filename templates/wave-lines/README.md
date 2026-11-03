# Wave Lines

原创程序化演示模板：径向波动、细发光曲线、中心收拢与三角网格过渡。固定背景平面，不创建 3D 场景对象或历史帧纹理；新画面由用户验收。

## 创作入口

- `Effect.shader`：画面算法。18 个 Properties 自动生成可打帧参数，显示标签带功能前缀，范围和单位明确。Phase 与 Travel 是显式运动坐标，允许停顿、反向和非匀速推进。
- `effect.json`：一次性声明 Shader 入口、目标与自动参数发现；日常无需填写参数列表。
- `sequence.json`：36 秒编排输入。旁边放 `Source/` 源码工程与 `background.png`，通过[自定义工作流](../../docs/vfx/CUSTOM_EFFECTS.md#agent-与声明式编排)生成新的 `.vfx.json` 副本。

普通创作者可直接打开已准备的演出工程，在 UI 中调整数值和关键帧。只有改 Shader 算法才需要外部代码编辑器与匹配的 Unity 编译器；仅播放已编译效果不需要 Unity Editor。

| 时段 | 目标观感 | 主要控制 |
| --- | --- | --- |
| 0–4 秒 | 较安静的底图与轻微细线变化 | Lines Brightness / Phase |
| 4–12 秒 | 背景边缘发生传播式弯曲 | Wave Displacement / Phase |
| 12–20 秒 | 金色细曲线扫动、摆动并增强 | Lines Phase / Curvature / Brightness |
| 20–25 秒 | 中心收拢、紫色软边界扩张 | Inward Warp / Reveal Radius |
| 25–32 秒 | 固定透视的亮三角网格快速推进 | Tunnel Visibility / Travel |
| 32–36 秒 | 软遮罩回收，底图与细线恢复 | Reveal Radius / Dimming |

这是独立演示节奏，未绑定真实歌曲，也不声称复现了原作品 Shader。所有时刻均为秒；Ease 控制当前帧到下一帧的变化。

## 调整建议

- 先关 `Tunnel / Visibility`，只观察波动与细线。分别把 Wave Displacement 或 Lines Brightness 归零，可单看另一部分。
- 波动适合有直线、建筑边缘的底图。增大幅度前先小幅调整；边缘使用钳制采样，强扭曲可能拉伸边缘。
- 线条有细亮芯、柔和外光与中心减亮，不依赖额外 Bloom。Color 的 Alpha 当前不参与计算。
- Travel 的斜率决定推进速度，直接编辑路程关键帧可连续加减速；不要将绝对时间直接乘一个突变的速度值。
- Reveal Radius 与 Tunnel Visibility 联合控制软显露。这里使用透明度权重，不使用 Stencil。

GPU 路径是底图绘制加一个合并后处理 Pass；常态一份图像取样，曲线和网格使用解析数学。单 Pass 不等于普遍低开销，仍需实际测量。

当前用于 Studio 实时预览；最小 addon 导出器尚不支持任意自定义后处理。Shader 数学可后续移入游戏 BG 材质，但不是已有的游戏安装包。
