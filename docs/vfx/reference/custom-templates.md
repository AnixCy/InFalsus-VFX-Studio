# 已知自定义模板

按标识检索；正文与编辑器效果说明共用数据。

## `custom/flow-feedback-background`

轻量连续扭曲和同帧嵌套缩放旋转。

Flow Distortion 拉弯直线，Nested Scale 建立嵌套层次，Nested Rotation 可反转，Transform Center 决定旋转与缩放中心。

确认边界：这是已知模板的说明；自定义改写代码后，以实际代码为准。

## `custom/procedural-pulse`

在源画面上叠加彩色同心脉冲环。

Intensity 控制亮度，Frequency 控制环带密度，Speed 控制传播相位；时间相对效果片段起点。

确认边界：模板不分析音频；节奏需要用关键帧编排。

## `custom/wave-lines`

径向波动扭曲、细金线和可选三角网格穿行。

Wave 改变底图取样；Lines 叠加六条带细亮芯与柔和外光的曲线。Portal Radius 扩张软遮罩，露出固定透视的三角网格；Phase 与 Travel 可打帧、反向和停住。

确认边界：这是原创简化模板；无历史帧、真实粒子或相机移动。所有运动由参数驱动，不分析音乐；新的动态观感由用户验收。

## `custom/perspective-cells`

上方远点光线、径向波动与不规则实体三角骨架。

Strands 从远端收束后向近处展开。结构采用扰动三角点阵、线段距离与色阶实体化，再叠加倒角亮边和底图反射；Travel 控制由远及近的推进。

确认边界：不绘制轨道或前景遮罩；不是原作源码或精确 Delaunay/Voronoi 移植。显示坐标上方为 Y=0，外观与动态相似度由用户验收。
