# 制谱器切换 Gamma 色彩空间方案

日期：2026-10-28。目标：在保持已有制谱、编辑功能和既有 Gamma 兼容效果的前提下，让 InFalsus Studio+ 的整体 Unity Player 使用 Gamma 色彩空间，按 InFalsus Modded 1.07b 的实际渲染逻辑逐项校验。本文是方案，不代表已经执行 Gamma 改造。

## 1. 当前基线与问题

- 本体 1.07b 的 PlayerSettings `m_ActiveColorSpace=0`（Gamma）；制谱器 Unity 工程为 `m_ActiveColorSpace=1`（Linear）。这是整体渲染环境的明确差别。
- 发布节点是 `E:\RhythmGame\Infstudio\IfsRelease\InFalsusStudioPlus_1.0.69_build20261028.zip`，SHA-256 `39979517F612A12CEEF9657DCC87859505BE8144E78A7E1B49C8D6118CE83E69`；包内插件和当前副本 DLL SHA-256 为 `A522DD697E91A4AF0CBA5F6AA2121E0103189E8AA6261C7E7628A55BCF9E455D`。当前安装仍然是 Linear，不是 Gamma 实装。
- 侧轨 `NativeSideGamma.hlsl` 使用 `QualitySettings.activeColorSpace` 对应的参数，Linear 时转换到 Gamma 混合后再转回；Gamma 时已有条件分支。其采样、背景捕获、透明度和绘制顺序仍需实测，不能只因看到分支就认为已经完全兼容。
- SkyArea 有为 Linear 帧缓冲转换本体 Gamma RGB 的适配；Combo 使用单独的颜色混合及捕获；中央动态背景、判定光效和部分贴图使用显式 Linear RenderTexture。切换 Player 后，可能出现重复转换、漏转换或中间贴图解释错误，需按输入与输出逐段定位。
- 背景发灰和未选中 Tap 发暗的唯一原因尚未确定；不同 Bloom 配置、贴图 sRGB 标记、材质及透明混合也会产生差异。切换 Gamma 是渲染环境改造，不自动证明这些问题已经解决。

## 2. 构建前置条件和范围

1. 使用 `E:\RhythmGame\InFalsusStudio+\upstream` 当前工程作为来源，保留其中未提交的本地修改；不要从旧包覆盖。将工程复制到隔离工作目录或使用安全的独立工作副本，保留 `Assets`、`.meta`、`Packages`、`ProjectSettings` 与用户已有改动，不带个人歌曲和构建缓存。先核对其源码与目前副本 `Assembly-CSharp.dll` 和资源是否一致；如果不一致，先确定可复现的核心基线，不能拿旧源码重建并覆盖现有功能。
2. 工程 `ProjectVersion.txt` 指定 **Unity 6000.3.25f1**，URP 17.3.0，Windows x64 / Mono。已检查的常见安装位置只有 2022.3.8f1c1，尚未找到 6000.3.25f1 的编辑器；开始实装前应确认正确版本和 Windows Build Support 可用。2022 版用于当前插件编译工具链，不能直接代替 6000 版重建整个 Player。
3. 在隔离工程中将 `PlayerSettings.colorSpace` 改为 `ColorSpace.Gamma`，构建完整 Windows Player（EXE、`_Data`、Managed 和 Mono 所需内容）；不能只替换插件 DLL，也不要仅改已编译 PlayerSettings 的字节。沿用 `StudioRelease110Checks.VerifyAndBuildBatch` / `StudioBuild08.BuildTo` 的检查与构建路径，并核对 BuildReport、错误、警告和实际 Player 输出。构建目录必须为新目录。
4. 对新 Player 重新编译并配套加载当前 1.0.69 功能基线插件与资源；检查反射字段、Harmony 钩子、AssetBundle、URP 渲染路径是否兼容。旧发布脚本按 Linear 基线核心和资源哈希校验，新构建应生成独立的新清单和哈希，不能通过关闭校验掩盖差异。
5. Player 启动后在隔离配置下读取 `QualitySettings.activeColorSpace`，记录必须为 Gamma；同时确认平台、图形 API、分辨率和项目实际使用的 URP 渲染路径。Player 运行状态和成功加载不能由工程设置或编译结果代替。

## 3. 保留已有兼容：逐模块检查

**通用原则：保留功能和现有 Linear 路径。** 先判定每个输入、着色器输出、混合背景和中间纹理各自使用的空间，再依据实际运行色彩空间决定是否转换；需要更改时保留 Linear 分支，使旧版测试和问题定位仍可复现。不得机械删除全部兼容代码，也不能对所有材料统一套 Gamma 修正。

1. **侧轨**：验证 `NativeSideGamma.hlsl` 在 Gamma 时参数为 0，检查输入纹理采样、源 alpha、背景捕获及 `Blend One Zero` 输出是否只发生一次本体等价混合。比较左右侧轨及轨道与背景边缘，背景捕获位置、相机视口和不同分辨率也要检查。若 Gamma 下已有分支正确，则保留；若不正确，只修相应分支或材质绑定。
2. **SkyArea**：追踪当前适配 shader/AssetBundle 的实际源代码或字节逻辑，明确 Gamma RGB 到 Linear 的转换发生在哪里；Gamma Player 只避免不需要的转换，保留原颜色、中心/边缘透明、噪声、首尾和 ghost 行为。缺少可重建 shader 源时先还原构建链和双空间变体，再实施，不直接猜参数。
3. **Combo 和文字**：检查 `NativeComboBlend` 的空间标志、捕获纹理格式与 combo shader；确保 Gamma 模式不会对数字或背景做二次转换，保留遮挡、顺序和混合行为。核对 `combo-digits` 作为数据纹理的特殊导入/创建方式，不把原始数值当普通颜色贴图反复解码。
4. **背景和中央动态层**：分别验证用户图片加载的 sRGB 解释、背景 shader 的采样和亮度、`NativeVisualCenter` 的各张显式 Linear RenderTexture、模糊和合成顺序。Gamma 模式下按数据用途检查格式；不是所有显式 Linear RT 都应改成 Gamma。图片尺寸、裁切、亮度、空背景和失效路径均保持。
5. **地面、Tap/Hold/Flick、判定特效**：逐项核对单宽、多宽、左右轨材质、贴图/UV、混合、render queue 与距离淡出，区分选中变暗与未选中本体颜色。检查 `NativeJudgementPresentation`、`NativeSkyFeedback`、选中变暗纹理处理中的临时 RT 和 `GetPixels`/`SetPixels`，防止 Gamma 切换造成颜色或 alpha 被重复处理。相同效果先与本体逻辑对照，不以统一提亮替代。
6. **Bloom**：现制谱器 threshold 1、intensity 0.3、scatter 0.65；此前提取的本体分别为 1.5、2.0、0.821。Gamma 切换后先分别记录关闭及开启的结果，检查当前 URP 和本体运行时覆盖；保留用户的开关和已设强度。只有有可复现对照后才提出参数迁移，不能直接把数字照搬或覆盖用户值。
7. **编辑器界面**：检查 IMGUI 色值、网格线、选择描边、节点注释、标记与图标在 Gamma 下的可读性。界面是编辑器交互元素，不强行套本体游戏 HUD 的色值逻辑；尤其保留网格线当前线宽及颜色增强。

## 4. 对照方法

- 固定同一背景素材、16:9 有效视口、本体 1.07b 视图、相同谱面和接近的时间位置、视速、输出分辨率与图形 API。记录配置、资源哈希、背景亮度、Bloom 开关、音符选中状态和具体帧；游戏 HUD、打击动态及无法严格同步的元素单独标记。
- 三组同时保留：1.0.69 Linear 基线、隔离 Gamma 构建、本体 1.07b。不能重新生成或拉伸截图；保留原图及原始像素数据。对背景无遮挡区、侧轨半透明区、中央轨、未选中 Tap 内外和高光分别取样；记录相同归一化坐标及通道差异。几何不完全一致处不伪装成同一像素对照。
- 先独立比较背景图片原像素与背景输出，再依次开启中央动态效果、侧轨、音符和 Bloom；每次仅切换一项，并记录改变的区域，以定位首个产生差异的阶段。测试黑、灰、白和高饱和样本及实际曲图。
- 比较必须覆盖图像贴图、各类透明与叠加效果、单宽/多宽和左右 Tap、Hold、Flick、SkyArea、Combo、判定反馈，以及有无 Bloom。检查不同背景亮度和缺图、暗/亮背景、窗口比例/分辨率、预览与暂停、选中和取消。

## 5. 功能与安装验收

- 先跑原 Unity 核心与发布检查，再在隔离 Player 跑插件兼容和有针对性的渲染测试。检查普通/播放缓存一致、屏幕裁切、时间标签、网格、十字线、段选线、节点、撤销、输入焦点及保存；同一场景对点选、框选、侧轨/空域与轨道远端裁切边缘进行射线和矩形命中回归，确认显示与选取范围一致。
- 保持 SPC、音频偏移及用户谱面不被修改；保留 `%APPDATA%/infstudio` 内相机、背景、热键等个人配置，不因新 Player 覆盖；测试使用隔离配置。
- 因 Player 文件整体改变，先验证完整发布清单、新构建核心与资源哈希、干净解压启动和独立回退。安装副本前备份将被替换的整个运行时与安装记录，逐项校验并支持失败回滚，不覆盖个人工程、第三方外置工具和共享配置。最终 ZIP 与安装副本须使用同一已验证构建。
- 区分代码检查、隔离 Player、真实键鼠及本体同帧画面验收。任何未做到的对照和异常必须如实列出，不以“Gamma 已启用”替代效果验收。

## 6. 主要风险与决策点

- Unity 6000.3.25f1 Windows 构建工具未在已检查位置找到；如无法取得，不能可靠完成 Player 重建。
- 源码和现行核心是否可重现、兼容 shader/bundle 是否有完整重建链，需要先核对；这两项不清楚前不覆盖副本。
- Gamma 与 Linear 分支可能导致 HDR/Bloom 和透明叠加不同，不能承诺切换后所有元素自动与本体一致；适配既要避免双重转换，也要保留已有视觉逻辑。
- 性能要测背景捕获和额外 RT 的代价；仅在画面一致且功能无回归后，才考虑移除 Gamma 模式下确实不需要的计算，不能为省一次捕获而破坏侧轨/Combo。
- 本次 1.0.69 保留 Linear。以上方案需在隔离构建和验收通过后再应用，用户指定的副本是默认安装目标。
