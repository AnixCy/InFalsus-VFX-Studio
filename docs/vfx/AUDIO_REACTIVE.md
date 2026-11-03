# 音频驱动参数：创作基础设施

当前为无界面的底层与验证 demo。Studio 仍为 0.7.5，既有安装、设置和游戏入口不变。尚未将音频驱动 recipe 写入 Studio 工程格式或接入界面；demo 只用已烘焙曲线在现有预览器中表现亮度变化。

## 组件与调用

| 组件 | 职责 |
| --- | --- |
| `IPcmSource` | 宿主解码后的 PCM 分块读取，不依赖任何媒体容器或播放设备 |
| `BandAnalyzer` | 居中 Hann STFT、频段 RMS、内容指纹；支持取消与进度 |
| `AudioAnalysis` | 可序列化的多频段缓存；校验后持有不可变副本 |
| `AudioEnvelope` | 固定 dB 范围、输入增益、响应指数及 Attack/Release |
| `AudioParameterDriver` | 音频时间到参数时间的转换与 Replace / Add / Multiply |
| `AudioCurveBaker` | 与预览共用求值器，采样、误差精简及既有曲线输出 |

实现入口：[src/AudioReactive](../../src/AudioReactive/README.md)。JSON 编解码仍由宿主提供，库不自带序列化运行时。

```csharp
// pcm 是宿主实现的 IPcmSource，不需要播放音频。
var analysis = BandAnalyzer.Analyze(pcm, new AnalysisSettings {
    WindowSize = 2048, HopSize = 400,
    Bands = new[] { new FrequencyBand { Id = "low", LowHz = 40, HighHz = 180 } }
}, cancellation);

var recipe = new AudioDriverRecipe {
    AnalysisCacheKey = analysis.CacheKey, BandId = "low",
    Node = "background", Property = "_Brightness", AudioOriginSeconds = 0,
    Envelope = new EnvelopeSettings { FloorDb = -50, CeilingDb = -8,
        AttackSeconds = .015, ReleaseSeconds = .15 },
    Mapping = new ParameterMapping { Minimum = 0, Maximum = 1.4, Blend = AudioBlend.Add }
};
var driver = AudioParameterDriver.Create(analysis, recipe);
var baseTrack = AudioCurveBaker.Compile(manualChannel, tempo);
double preview = driver.Sample(timelineSeconds, baseTrack);
var baked = AudioCurveBaker.Bake(manualChannel, tempo, driver,
    new BakeSettings { StartSeconds = 0, EndSeconds = 12, MaximumError = .002 });
// baked.Channel 是新的既有 ChannelDocument；manualChannel 和 recipe 保留。
```

## 电平与时间定义

分析使用居中、周期 Hann 窗，窗口外补零。时间点为 `i × HopSize / SampleRate`，生成覆盖音频末端所需的最后一个采样；音频时间区间为 `[0, FrameCount / SampleRate)`，区间外电平/包络为零。

单边谱平方和按窗口能量和 FFT 长度归一化，再开方得到频段 RMS。DC/Nyquist 不加倍，其余频率加倍；边界频率格按与频段的重叠比例计权。多声道先各自求功率再平均，不将左右声道先相加，因此反相内容不会相消。全刻度正弦的 RMS 约为 0.707（-3.01 dBFS）。这不是乐器分离、节拍识别或感知响度标准测量。

包络输入为 `20 log10(RMS) + GainDb`，在固定 Floor/Ceiling 之间归一化、限制到 0–1，再应用指数与 Attack/Release。平滑使用 `exp(-step / tau)`，从零状态整段预计算；零时常为即时响应。不会按当前帧或局部片段自动归一化，以免静音也被抬满。

`AudioOriginSeconds` 明确表示 **PCM 第 0 帧在目标时间轴上的时刻**：`audioTime = timelineTime - AudioOriginSeconds`。例如 `chart = audio + 0.125`，则图表时间驱动使用 `AudioOriginSeconds = 0.125`；使用音频时间则为 0。偏移只转换一次，不使用 SV，不累计墙钟。倍速时按原曲对应位置取缓存；不分析变调或拉伸后的设备输出。

居中分析会产生约半窗口宽度的瞬态展宽，Attack/Release 还会影响峰值形状；时间标记不额外偷加半窗口延迟。窗口/hop 是分析设置，和播放 FPS 无关。

## 缓存、快照与安全边界

`AudioAnalysisDocument` 保存算法、配置、解码 PCM 的 SHA-256、CacheKey 和各频段 RMS。`FromDocument` 校验格式、身份、长度和有限非负电平，并复制数组；取出文档/电平再修改不会影响既有缓存。它校验配置身份，不作为不可信远程文件的完整性签名。宿主可额外按文件内容指纹快速定位缓存；文件名或修改时间不足以替代内容校验。

`AudioDriverRecipe` 只保存缓存身份、频段、绑定、包络、映射与时间原点。原始人工动画另存。驱动仅支持 Float；颜色/向量分量、多个驱动器的排序和 UI 事务留给后续接入。数值不自动限制到 Shader 属性范围，宿主需要按参数能力提示或限制。

宿主保持 PCM 在一次分析期间不变，并在后台工作前提供稳定快照。分析缓冲按 FFT/声道大小复用，不载入整首 PCM；实际解码器可以流式读取。库限制 8–384 kHz、1–8 声道、24 小时以内音频、128–32768 的二次幂窗口、至多 32 频段及总计 800 万电平值；超预算显式失败。文件解码、磁盘缓存原子保存和异步任务调度属于宿主，当前没有接入。

## 烘焙与玩家契约

默认 120 次/秒的起始采样网格，同时纳入包络采样点、原人工关键帧和区间端点，避免漏掉两帧之间的源峰值。另检验中点，按**纵轴数值误差**精简；误差过大时有界细分，超过样本/关键帧预算则失败，不静默截断。

Hold 跳变与音频边界隔离为最长 1 微秒的保护段，输出 Hold + Linear，避免生成可见的提前渐变。合并相差不足 `1e-10` 秒的采样网格舍入；保留原关键帧精确时刻，无法区分的两个固定端点明确报错。输出 Float 精度不足以满足容差时失败。

`MeasuredMaximumError` 是指定采样网格及细分中点上的实测误差，**不是对所有实数时刻的数学上界**；快速变化应提高采样率并独立加密验证。只约定给定烘焙区间内的行为，调用方负责区间放置和区间外衔接。

玩家只接收普通 `ChannelDocument`，无需本库、FFT、PCM、recipe 或新播放器能力。现有 addon 导出器已经能保留受支持背景参数的烘焙曲线；验证夹具会运行该转换和既有运行契约求值。自定义 Shader 的游戏支持范围保持原状，烘焙不扩展材质兼容性。本轮不安装 addon、不启动游戏。

玩家清单仍受现有 4 MiB 文件限制。`MaximumKeys` 是本次通道的计算/点数预算，不是 JSON 字节数保证；接入导出时还需校验所有通道合并后的文件大小，不能按单条曲线分别放宽。

## 验证 demo

```powershell
& .\scripts\Check-AudioReactive.ps1 -OutputDirectory '<新的输出目录>' `
    -PreviewAssets '<已有预览插件资源目录，可省略>'
```

仅需要制作端 .NET SDK 9。可选 `PreviewAssets` 用于校验已有 `preview.bundle`，不覆盖资源。不传时仍可完成纯数值验证；所有输出必须写入新目录。

生成文件：

- `demo.wav`：12 秒双声道合成测试音。1–2 秒低频、3–4 秒高频，5–9 秒低频脉冲，7–10 秒叠加高频脉冲，末尾静音。
- `analysis.json` / `driver.recipe.json` / `manual-channel.json`：可重用分析、驱动设置与原人工动画。
- `bake-settings.json` / `baked.runtime.json`：烘焙设置及预览运行数据。
- `game-runtime.contract.json`：通过既有 addon 导出器的契约夹具，使用虚构目标，不是可安装的游戏包。
- `AudioReactive.vfx.json`：现有 Studio 可打开的已烘焙亮度演示，手动加载和播放；不是新增音频驱动 UI。
- `comparison.csv` / `demo-report.json` / `checks.json`：频段、包络、人工值、直接求值与烘焙值，误差和性能结果。

夹具验证校准正弦、独立时域 Parseval、频段分离、反相立体声、静音、边界、内容失效、JSON 回读、回跳、正负时间原点、叠加/乘法、原关键帧不变、Hold、误差、取消与预算。不会启动 GUI、播放音频或执行视觉检查。计时仅说明此次机器上的离线分析与曲线求值，不代表真实游戏 FPS。

STFT 的窗口、hop 与时间/频率采样定义可参考 [SciPy 官方说明](https://docs.scipy.org/doc/scipy/reference/generated/scipy.signal.ShortTimeFFT.html)。本库独立实现 radix-2 FFT，没有复制该库代码或引入其运行依赖。
