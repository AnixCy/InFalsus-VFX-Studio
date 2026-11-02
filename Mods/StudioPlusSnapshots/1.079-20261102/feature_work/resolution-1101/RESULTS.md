# 导出分辨率采样修复（2026-11-01）

已安装副本，版本仍为 1.0.7。DLL ECD5D6E8521570BC142FB3D76CDA590A255ACF22326CA6A06DE0E4C375B5FFB6。安装前基线 00C599B70B99EBA31D1562EE551AC91439F4BE24BA008D83D01628A5BCFAB2F9，保留10:29更新的游玩判定显示修复。

原因：Graphics.DrawMesh 排队时获取材质属性，而 _NativeComboViewport 原先在 RenderGraph.RecordRenderGraph 才更新。离线渲染先提交数字/侧轨，后进行相机请求，造成使用上一画面尺寸采样当前背景。4K、1440p和连续切换分辨率会产生错误填充/透明效果。

修改 NativeComboBlend.View/Bind，在相机目标已设好时提前同步数字与侧轨视口；OfflineFrameStepper 在任何场景绘制提交前调用 View。保留原贴图、Gamma颜色公式和HDR导出，不通过改色补偿。

检查：隔离Player实际渲染720p、1080p、1440p、2160p，再切回1080p，含HUD、血条、SkyArea及按键文字。四档数字区域首帧与同时间重复渲染帧逐像素一致。统一到1080p数字ROI比较，4K与1080p平均RGB误差从[34.024,33.021,24.040]降至[2.809,2.478,3.104]/255；1440p从[14.048,13.233,10.008]降至[2.390,2.215,2.158]/255，剩余包含分辨率抗锯齿差异。生产异步读回与同步帧一致，实际MP4管线编码成功。原始检查和截图见before、after、final目录。

备份路径见 installation-verification.json。未打包、未改更新公告。
