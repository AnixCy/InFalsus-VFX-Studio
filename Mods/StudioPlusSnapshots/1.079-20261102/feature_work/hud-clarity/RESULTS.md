# HUD 清晰度、渐变与辉光调整（2026-11-01）

已仅安装到制谱器副本，仍1.0.7，未打包。

修改：SPEED由8.5→10.5个1080p参考像素，速度值10→11；数值按标签实际字宽留5像素间距。静态HudText整行起点对齐物理像素。SDF覆盖率按UV导数、2048图集尺寸和16 texel距离跨度计算，字形UV采样限制到自身矩形，边界外覆盖率为0，避免邻字泄漏。保留图集清零与8 texel打包间距。分数渐变从baseline-size到baseline+size*.25，保留本体HDR端点。Bloom低质量预提取改为单次双线性采样；保留原生9tap横向/5tap纵向滤波、阈值转换后2.53715524、软拐点1.26857762、强度2、scatter映射.78889998，补齐Gamma中间编码/解码，按尺寸计算层级且上限6。文字本体与辉光分别转换到Linear后合成，消除原先先混合再按实体alpha反解颜色的误差。

源码：studio_icon_work/Source/Native/NativeVisualPresentation.cs；着色器：feature_work/native-dots-text/ShaderProject/Assets/NativeHud.shader。修改前文件在本目录，最终载荷在Candidate（DLL及native-hud.bundle）。distribution.json与SOURCE_MANIFEST.json同步。原始运行时字体/布局与本体参数依据见visual_research_107b/hud-restoration及分数与曲目信息本体还原方案.md。

必要验证：Unity着色器与完整插件编译通过。固定1920x1080、2560x1440离屏HUD绘制，检查字形覆盖、HDR、字宽、缺字回退、导出合成有限值；另检查普通Linear预览合成路径的0～9全部数字。已查看两个尺寸的HUD导出图及全部数字的预览合成图。不是完整窗口缩放回归，也没有做与本体同背景同帧逐像素验收。初期旧素材/隐藏HUD和旧长标题窗口坐标检查失败不计为通过；最终将测试收敛为本轮实际受影响的离屏目标与合成检查。

1080-verified: 10 PASS, DONE failures=0
1440-surface: 10 PASS, DONE failures=0
both-paths: 11 PASS, DONE failures=0

备份：E:\RhythmGame\Infstudio\InFalsusStudio_1.0.10\VisualUIData\backups\20261101-145457-767-infstudioplus-hud-clarity_20261101

仅替换主插件及HUD材质两个文件，配置、主程序、其他插件和外置工具由安装器保护。安装记录：installation-verification.json。
