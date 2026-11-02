# 非游玩 AP 与预览一致的视频颜色（2026-11-01）

已安装到副本 E:\RhythmGame\Infstudio\InFalsusStudio_1.0.10。版本仍 1.0.7、148 个 C# 源文件。用户要求停止继续检查后，直接安装最新合并 DLL，保留其他工作区最新 SPC2、noinput 裁切及 FFmpeg 路径布局修改；未打包、未改公告、未启动用户副本。

## 修改

- DrawLifeGauge 移除非游玩暂停/手动光标状态下的隐藏条件。血条开关开启时，非游玩常驻 AP，游玩仍使用真实 GaugeState/GaugeValue。开关关闭仍隐藏。不改血量公式和判定逻辑。
- 原导出直接将 URP 场景绘制进 R8G8B8A8_SRGB 目标；正常预览实际使用 B10G11R11_UFloatPack32 HDR 场景缓冲。SkyArea 的蓝色 HDR 边缘值较高，8 位场景目标在透明混合结束之前截断高光，导致紫边失蓝、偏白偏黄。这是编码之前的场景差异；前一轮编码前后对比不足以覆盖该问题。
- 导出新增独立 HDR 场景目标，遵循相机 allowHDR 与 URP supportsHDR、hdrColorBufferPrecision，当前 32 位精度使用与预览相同的 B10G11R11；不支持时使用默认 HDR。完成透明混合与后处理后，显式按目标 sRGB 状态解析到视频 RGBA8 表面，再执行已有 HUD Gamma 合成与 BT.709 编码。LDR 相机维持原路径；退出/取消释放 HDR 目标并恢复相机。

## 已有证据

- baseline 保存修改前安装基线 E30D... 的同谱同时间正常预览和导出。原紫色区域蓝通道平均偏差约 -20/255，GPU 目标格式日志证明预览 HDR、导出 LDR。
- verified 使用 CDBB3573A8755C1C4BA73695E49E79A6BC9D3BA3275C48B9608825266F48F99D 候选，保留当前 SPC2 与 noinput 修复。正常与导出均为 B10G11R11，固定相机、谱面时间及 shader 时钟，直接比较正常预览与导出场景。不是只比较导出原始帧和编码后画面。
- 合成 SkyArea 场景中心比较区平均绝对 RGB 差 [0.070,0.084,0.327]/255。实际 Outliers FBD、14s、视速5、Mindscape2-Light 白色竞技场，比较区 [0.343,0.548,0.732]/255；实际编码 MP4 对正常预览 [2.856,2.206,2.974]/255。紫色像素色偏另存 image-comparison.json。区域比较排除 GUI 和部分会受播放起点影响的瞬态打击光效，不宣称全部像素一致。
- 同步原始像素与生产异步 GPU 回读逐字节一致；生产编码管道短片成功，另实际渲染 3840x2160 帧并恢复相机；非游玩 AP/关闭隐藏检查通过。见 verified/checks.txt、Outliers 对照 PNG/MP4、4K PNG 和 ffprobe.json。已查看实际对照画面。
- gauge-regression/checks.txt：78项、0失败，包含六态动态血条/显示开关/非游玩 AP、血量公式、当前谱面导出位置、MP4与取消恢复。上述检查在用户要求停止检查之前完成。
- 之后其他工作区合入 FFmpeg 路径显示布局，最终交付 8590...；本轮 HDR/AP 源码保持上述已验证实现。按用户要求，未再对该最终合并 DLL 追加运行检查。

## 安装

最终 DLL：8590C37A3390B9686D0D8E9E2A1F65F1FC1065B56327F9AC15AF6058184DCFE0。
备份：VisualUIData\backups\20261101-101111-876-infstudioplus-preview-hdr-ap_20261101。
仅替换主 DLL；配置、谱面、其他插件、外置工具与资源保护由安装器核对。安装记录 installation-verification.json；最终源码快照 Source，权威源码继续为 studio_icon_work/Source。
