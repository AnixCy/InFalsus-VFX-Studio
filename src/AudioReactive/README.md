# 音频驱动基础设施

`netstandard2.1` 创作端库，引用现有演出 core，无 Unity、设备音频、DSP NuGet 或本地 DLL 依赖。不被 Studio 或游戏项目引用；此次不增加 UI、自动监听或游戏加载入口。

链路：`IPcmSource → BandAnalyzer → AudioAnalysis → AudioEnvelope → AudioParameterDriver → AudioCurveBaker → ChannelDocument`。

- `IPcmSource` 由宿主提供已解码、交错排列的 PCM，支持绝对位置分块读取；宿主负责文件解码和数据生命周期。
- 分析缓存按解码内容指纹、分析算法版本、采样率/通道数、窗口、hop 和频段区分。改变包络、映射或目标参数可复用频段 RMS，不必重新 FFT。
- 预览是可随机定位的函数，平滑提前算在整段包络中；采样不依赖上次播放、音量、设备、帧率或 SV。
- 烘焙生成既有 Float 通道；保存来源关键帧和 recipe，允许无损返回创作设置。库不改写工程、撤销栈、音频或谱面。
- `AudioAnalysisStore` 提供可回读校验、checksum、写锁与原子保存；`AudioBakeBatch` 验证整组目标和总关键帧预算，保留未驱动通道。两者仍未接入应用层。

详细定义、示例与接入边界见 [AUDIO_REACTIVE.md](../../docs/vfx/AUDIO_REACTIVE.md)。运行 [验证脚本](../../scripts/Check-AudioReactive.ps1) 生成合成测试音、缓存、recipe、CSV 和可手动打开的演出工程；不自动播放、不启动 Studio 或游戏。
