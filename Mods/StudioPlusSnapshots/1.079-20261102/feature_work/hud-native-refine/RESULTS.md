# 本体小字、血条过渡与 NO JACKET（2026-11-01）

已安装副本 `E:\RhythmGame\Infstudio\InFalsusStudio_1.0.10`。仍1.0.7，149个C#文件；未打包、未改本体、未改谱面与个人配置。

## 字体差异的明确依据

- 上版小字存在人工调整：FBD字号2.05（本体1.85）、SPEED2.1（本体1.7）、数值2.2（本体2），难度/等级强制纯白，SPEED也额外提亮并做整像素取整。以上使相对尺寸、笔画亮度及亚像素覆盖与本体不同，本次恢复原生大小、坐标、颜色和透明度，不用更换成任意细体修表象。
- 更关键的渲染差异：`FastText.UpdateCommonParametersBuffer`（0x4300c0）把SDF范围写成16；原版像素着色器`font-shader-0-3.asm`通过dp2对两个相同项求和，使覆盖率使用2×16。上版Shader只用16，边缘过渡范围扩大一倍。此次修正为32，保留字形自身UV范围限制，不重新引入邻字泄漏。记录见native-font-buffer.asm及visual_research_107b/dots-font-plan/font-shader-0-3.asm。
- Supreme Regular/Bold和BaiJamjuree SemiBold等原文件与本体资产RawBytes逐字节匹配。英文DefaultFontSet的Regular/Bold槽与现用字体一致；本体还含语言替换字体族，不能把不同语言截图的字形不经核对地当成同一字体。本轮实际采样的主分数字体为BaiJamjuree-Medium；live-fonts.json、live-linked.json保存只读采样，未声称已直接采到当前SPEED实例。

## 血条

直接依据本体ClearStateContainer._yBA（0x683e10）、_XBA（0x683b50）、Update（0x683990）及_Ui._QrA（0x50a200）。当前状态与目标状态分离，降级跨档依次经过中间档；中间档0时长提交，最终档0.3000000119秒SineInOut（EaseType字节值3）；升至AP/FC沿本体直接设置分支。过渡中更新目标不打断当前忙碌档，结束后追随最新目标。正负数字透明度由Update以dt/0.200000003速率移动。C2D有效透明度按原有平方语义用于图片和文字，不改判定引擎的分数/血量/状态阈值。

新增NativeGaugeTransition.cs，实际绘制同时提交旧/新指示图与底板，数字独立淡入淡出。更换游玩引擎重置显示状态；导出保存/恢复gaugeAnimation、gaugeOwner，避免离线时钟污染预览状态。

## 缺省曲绘

已找到原版资源。Addressables键：`Assets/BuildSpecificAssets/release/if-gamedata/songs/nojacket.mat`；材质所在包f6773a65b5ab922e440e9f94a17e4945.bundle；_MainTex pathID=-979486316240444844，Texture2D名nojacket，256×256。按原像素导出为nojacket.png，未重绘或用用户截图裁切。材质详情nojacket-source.json。

缺曲绘时仅显示该图，保留原曲绘框和裁切；不写songJacket元数据，不把缺省图当成用户选图保存。PNG SHA-256：64C8BDC9B1D6F3794C5C244295A787D6F0C8278522CA41CDCCAEB21E599B1F2A。

## 检查

Checks.cs在隔离真实Unity播放器中执行：1080p/checks.txt（本目录checks.txt）与1440/checks.txt均25项、0失败。包括0/0.15/0.3秒AP→FC、跨级、过渡中换目标、帧率无关、数字正负交叉淡入淡出、时钟倒退、真实游玩进入/退出和原图加载；实际GPU绘制AP/FC/正状态的起点、中间与终点。已查看ap-nojacket.png、fc-mid.png、1440/positive-mid.png。初次检查因隔离UI未绑定而失败，检查器显式绑定真实Bootstrap后通过；没有为此改变产品逻辑。

这些是程序驱动的离屏图与状态检查，不是本体同输入同背景的逐像素比对。只执行本轮必要检查，没有重跑全部游玩回归。

## 安装与回退

Install.ps1替换主插件、native-hud.bundle及新增nojacket.png，共3个文件；校验原安装记录、候选指纹、受保护文件并备份。SOURCE_MANIFEST.json和distribution.json更新；修改前源码/Shader在本目录*.before.*，新增文件为studio_icon_work/Source/Native/NativeGaugeTransition.cs。

DLL：FCB80D91CE9E58C9185DD54706D02EABE8FD7260C9C8C38FC2B6EE6749DDCDED

备份：E:\RhythmGame\Infstudio\InFalsusStudio_1.0.10\VisualUIData\backups\20261101-153331-763-infstudioplus-hud-native-refine_20261101

安装记录：installation-verification.json。回退时恢复备份files中的DLL/native-hud.bundle及previous-installation.json，移除本次新增nojacket.png即可。
