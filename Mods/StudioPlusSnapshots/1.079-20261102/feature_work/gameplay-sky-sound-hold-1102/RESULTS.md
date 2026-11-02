# 天空音效与 Hold 判定：已安装副本

2026-11-02，公开 1.0.7 / 内部 1.075。只更新主插件 DLL，160 个 C# 源文件不增减；未打包、未提交 Git。

## 本体证据与原因

直接读取 1.07b GameAssembly 与 CoreScene 序列化映射；本轮未要求用户重新进入游玩，也未修改本体。

- 音效文件映射正确：55=59_note_hit_3_draft2.wav、62=59_note_side_hit_draft2.wav、58=58_note_hit_skylane_draft1.wav、61=60_note_flick_5_draft2.wav；持续音 57/63 分别是天空/地面。编辑器资源对应 FloorHit / SideHit / SkyHit / Flick / SkyHold / FloorHold，没有交换样本。
- Flick：本体 `_VD._Oz` 0x5f4fb8、0x5f50c3 在 `_nz` 返回已提交且非 Break 时才发出音效事件 0；`GameScene._Mk` 的 0x706451 分支使用 `_zf._pFA`，传入当前本体时刻加物件剩余时间。当前重写版额外在第一次候选滑动时发声，可提前约 200ms。本次删除额外候选攻击音，只在有效判定提交后请求原物件时间，迟到由现有混音器完整保留音头。
- Flick 轨迹：本体 `_TD._bz` 0x5f3c10 按当前时间删除过期记录；编辑器原有 50ms SwipeTracker 仅在新鼠标事件里 Trim。事件输入重写后，停手不会再清理，导致旧动作继续升级候选甚至误触后面的 Flick。现每次判定推进都清理两方向轨迹。
- SkyArea：本体 `_VD._Oz` 0x5f52fd / 0x5f5487 记录捕获的组号；0x5f5c5d 对比前后捕获组并发送事件 6，`GameScene._Mk` 0x7066ee 使用 58 号样本。原实现借用了编辑试听的 SkyConnections 首段规则，不能正确表达捕获切组。默认选项改为本体捕获规则：同组续段、短暂离域不重放头音，切换捕获组时播放；每段/关闭头音两个显式选项保留。设置中将默认项名称改为“本体捕获”，英文同步。
- 本体 `GameScene._Mk` 四个独立时间槽以及 `_zf._oFA` 对相同样本做 10ms 抑制。现按样本独立合并，避免同刻同类攻击多次叠加；使用事件时间并按播放倍率换算，慢帧批量交付不会把远隔的攻击错误吞掉。
- Hold 不是完全一致。本体 `_VD._Oz` 0x5f59e3..0x5f5a47 仅对上一轮有效按住、当前不再有效的 Hold 登记断手，要求已过头部且离尾部还剩 >50ms。`_Uz` 0x5f73f0 要求头节点已有状态。原代码只要过头时间且当前没按住、距离上次松开 >50ms 就登记；未按过时 LastRelease=-Infinity，正常晚按几毫秒也会错误置位。本次使用独立 HoldInputActive 表示上一轮有效状态，并按本体端点条件登记真实断手。
- 本体 `_pz` 0x5f8737..0x5f8781 在下个非尾持续节点提交时消费断手标记，并按原掩码压到 Near。原实现另加“当前仍按住”的条件，本次移除。25/50/100ms 头部判定窗、tick 拆分及原候选升级掩码未改；没有通过放宽判定窗掩盖问题。

## 改动范围

- Gameplay/NativeJudgeEngine.cs：Hold 有效状态转换、断手标记消费、每次推进清理 Flick 轨迹、删除额外候选攻击音。
- Gameplay/GameplaySoundController.cs：独立可检查的游玩音效路由、本体 Flick / Sky 捕获事件及相同样本 10ms 去重。
- Gameplay/GameplayOptions.cs、UI/NativeEditorUILocalization.cs：默认 Sky 选项的准确名称。
- 音频时钟、缓冲、音乐与音效延迟参数、样本资源、编辑试听、谱面文件均保持。

## 必要检查

旧版 DLL 在隔离 Player 中复现 3/3 原始错误：正常 +3ms 头部后持续 Hold 误 Near；真实 10ms 断手漏记；一次滑动误触下一颗 Flick。

修正版同环境 25 项检查全部通过，包括上述回归、30/60/144FPS 稍晚起按 Hold、合法 Near 头部不牵连后续节点、尾部 50ms 保护、宽 Hold 换指、两方向 Flick / 漏击、Sky 同组续段与短暂离域 / 跨组及返回 / 显式每段与关闭头音、相同样本去重和倍率、运行时三个天空音效资源。

初次检查有一项失败来自检查谱面把 Tap 的宽度和轨道参数写反，修正检查资料后 25/25 通过；未为让该项通过修改产品代码。编译成功，现有 URP 过时接口警告未新增处理。未扩大到整谱回放或真人听感验收。

## 安装与回退

目标：E:\RhythmGame\Infstudio\InFalsusStudio_1.0.10

备份：E:\RhythmGame\Infstudio\InFalsusStudio_1.0.10\VisualUIData\backups\20261102-155242-901-infstudioplus-gameplay-sky-sound-hold_20261102

DLL SHA-256：6A4B910B730DDB34E08A8F8A474407F169B57BC95B42EF4820CAE985562809E7

安装前再次检测副本已关闭，安装只替换 DLL，指纹一致。原配置仅备份，不写入；FMOD 依赖、频谱、其他资源不替换。安装脚本与逐文件结果见 Install.ps1 和 installation-verification.json。主源码与频谱宿主指纹清单已同步。
