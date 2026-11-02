# 动态血条与视频默认导出路径（2026-11-01）

两项已安装到副本 `E:\RhythmGame\Infstudio\InFalsusStudio_1.0.10`。版本仍 1.0.7；147 个 C# 源文件；未打包、未改公告、未启动用户副本。原配置、谱面、外置工具、其他插件及上轮音效/视频颜色修复保留。

## 本体依据与实现

本轮直接读取 InFalsusModded1.07b 的 GameAssembly.dll 和 CoreScene 资产，未修改本体。反汇编保存在 native-gauge-disassembly.txt；布局、材质、字形依据见 native-gauge-artwork.json、native-gauge-text-layout.json、native-gauge-font.json。

- `_Uh..ctor`（0x4ae810）初始化 Gauge=15；`_Uh._pMA`（0x4aead0）的六档跳转表确认：Break 减 1、Near 加 50/N、Exact/Exact+ 加 100/N；N 为有效判定点数，每个结果结算后单独限制到 [-15,15]。在 NativeJudgeEngine.Settle 接入，Tap/Hold/Flick/Sky 的所有判定点均参与，不依赖是否启用音效/特效/血条显示。
- 保留现有中途练习规则：只统计进入位置之后的有效判定点，血量重新从 15 开始；既往跳过点不扣血、不影响 AP/FC，回击也不能清除已发生的 Near/Break。
- `_Uh._oMA`（0x4aeab0）：先按偶数舍入到百分位整数，再做有符号整数除 100，得到显示整数；不是直接对 Gauge 四舍五入。
- `ClearStateContainer._xBA`（0x683d50）：没有 Break 且没有 Near 为 AP；没有 Break 但有 Near 为 FC；有 Break 时按显示整数 <=-6、-5..-1、0..6、>=7 选择负扩展、负、正、正扩展四状态。状态与数字在同次实际 GUI 绘制中更新。
- 原来只有常驻 AP 两张图，本轮新增六张本体原图用于 FC 和正负状态，沿用已有 AP 与 FC/AP 底图。左右指示条遵守本体 anchor/offset（正侧右端 +15，负侧左端 -15），不把所有图居中或拉成长血条。数字使用本体 NumberFontSet 的 style=1，即 BaiJamjuree-Bold，采用本体字号、颜色与偏移；原图及布局来自资产，状态选择来自代码。
- 设置标签中英文改为“血条 / Life gauge”，保留原 NativeApGauge 配置键及显示开关以兼容已有偏好。暂停编辑不再常驻 AP；游玩按真实结果显示，自动预览/视频导出按自动全命中显示 AP。此处为本体基础节奏 Gauge/ClearState，不新增角色战斗技能或血量耗尽强退。

## 视频导出

复用现有“打开谱面文件夹”的 CurrentProjectFolder 解析：歌曲工程采用工程根目录，单独打开的谱面采用谱面所在目录。MP4 和唯一临时 WAV/MP4 均在该目录，不再创建应用根目录 Render。

“打开文件夹”按钮每次重新解析当前谱面，不会在切换谱面后继续打开上一次导出的位置。没有所属目录的未保存谱面提示先保存或打开工程；文件名冲突仍自动追加序号，取消/失败删除本次临时文件，不修改已有视频与谱面。

## 验证

- candidate/checks.txt 与 final/checks.txt 均 78 项、0 失败；final 使用最终 Bold 字体校准后的 DLL。涵盖各判定增减、逐点夹限、空谱/练习分母、重复结算保护、恢复与状态阈值、偶数舍入、六种实际 GUI 状态/截图、显示开关、暂停隐藏、设置标签，以及工程/散谱/未保存/换谱路径、同名保护、真实 MP4 和取消清理。
- regression/checks.txt：110 项原有游玩判定及键鼠回归通过，0 失败；执行于最终字形校准之前，随后只校准了血量数字的 Medium→Bold 字体，不涉及判定逻辑。
- 已查看正扩展与负扩展的实际界面截图。程序驱动隔离 Player 检查，不是与本体同一物理输入逐帧对打或角色技能验收。

## 安装

用户确认退出后，Install.ps1 仅更新 DLL 与六张新增原生状态图，共 7 个载荷文件。安装器验证旧 DLL 基线和所有保护文件，原文件及安装记录均有备份；runtimeAssets 指纹同步。安装后的 DLL SHA-256：

`E30D155903E1AA9420884D6817240C1A24034DB6F87EE00CDB6AB680DD462B07`

备份：`VisualUIData\backups\20261101-094859-261-infstudioplus-life-gauge-path_20261101`。验证记录 installation-verification.json，修改清单 source-changes.json。
