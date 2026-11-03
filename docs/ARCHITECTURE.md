> 范围说明：本文保留上游 Studio 1.0.10 宿主文档；其中版本和能力描述不代表 Studio+ 1.079 增强插件或本项目当前状态。本项目入口见 [异象开发说明](vfx/DEVELOPMENT.md)。

# 开发入口与稳定规则

当前基线为 **1.0.10 + Flick 尾部修正**。本目录只描述当前实现，旧版本编号文件仍可能被现行入口复用。

## 数据流与代码位置

| 职责 | 主要入口（相对 `Assets/InFalsusStudio`） |
| --- | --- |
| 无损源码与稳定身份 | `Runtime/Core/SpcDocument.cs` |
| 歌曲目录、四难度与资源识别 | `Runtime/Core/SongFolderProject.cs`、`SongMediaDiscovery.cs` |
| 保存、冲突预检和失败回滚 | `Runtime/Core/ManualSongSession.cs`、`Runtime/StudioBootstrap.ManualSave.cs` |
| 撤销与作者操作 | `Runtime/Core/SpcDocument.cs` 中的 `EditHistory`、`EditOperations.cs` |
| Sky 分组、连接和切分 | `Runtime/Core/SkyGroups.cs`、`SkyAutoGrouping.cs`、`SkySplitOperations.cs`、`SkyGroupSyntax.cs` |
| 整数空间坐标 | `Runtime/Core/RationalAirCoordinates.cs` |
| 谱面检查 | `Runtime/Core/ChartHealthCheck.cs`、`Runtime/StudioBootstrap.AuthoringTools.cs` |
| 歌曲元数据 | `Runtime/Core/NativeSongDetails.cs`、`SongMetadataEdit.cs`、`LosslessJsonPatch.cs` |
| UI 协调与面板 | `Runtime/StudioBootstrap.cs` 及同名前缀的 partial 文件 |
| UI 焦点与延迟事务 | `Runtime/StudioBootstrap.InputFields.cs`、`StudioBootstrap.GuiFrame.cs` |
| 音符网格、裁切与拾取 | `Runtime/Rendering/NoteRenderer.cs`、`MeshBatch.cs` |
| Flick 形状 | `Runtime/Rendering/FlickGeometry.cs` |
| 舞台与相机 | `Runtime/Rendering/StageSpace.cs`、`Runtime/StudioProfile.cs`、`Runtime/Rendering/StageRenderer.cs` |
| 播放、保调减速和打击音 | `Runtime/StudioTransport.cs`、`Runtime/Core/WsolaTimeStretch.cs`、`Runtime/StudioKeySounds.cs` |
| 版本与应用公告 | `Runtime/Core/ReleaseIdentity.cs`、`ReleaseNotesCatalog.cs` |
| 组合检查与构建 | `Editor/StudioRelease110Checks.cs`、`StudioRelease100.cs`、`StudioBuild08.cs` |

`StudioBootstrap` 协调 UI、编辑模型和渲染。编辑事务先更新内存中的 `SpcDocument`，再刷新时间轴、物量、可见网格和音效计划；`ManualSongSession` 在显式保存时处理各难度的磁盘事务。不要从渲染层写回谱面。

## 源格式和手动保存

保留稳定 SourceId、未改动 token、注释、未知记录、`icp_event`、BOM/EOL 和末尾换行。读取、切换难度与字段确认不能变成自动保存、打开即迁移或整曲重导。

保存/导出对已识别时间语句做稳定排序，保持同刻运行时优先级；不能为了排序重写数值精度或丢弃未知内容。四难度保存先准备副本，通过冲突预检并全部写入成功才接受新顺序。失败回滚与 undo/redo 不能被清空。

`StudioBuild08.cs`、`CoreSelfTests.cs`、`LosslessJsonPatch.cs`、`SongMetadataEdit.cs` 已包含维护者的本地修补。以当前版本为基础修改，不从旧发行包覆盖。

## 输入与界面

运行时 UI 使用 IMGUI。Layout、Input、Repaint 保持相同控件树，操作通过已有 GUI 队列提交；不能只在 Repaint 添加控件或吞掉 `ExitGUIException`。输入草稿、焦点、命中阻挡、viewport 缩放和点击区要一起维护。

段落选择必须完整包含 Hold/Sky 的起止时间。相机和帮助位于设置，工具与谱面检查在左侧栏。任何 UI 重排都应检查一次手势一次撤销、混合类型选择以及未完成操作的取消。

## 渲染与音频

相机/StageSpace 和舞台分区有既有标定，不因普通 UI 或格式修改改变。GUI 基准 1920×1080 与相机标定参考 2048×1152 分别处理。

滚速使用真实局部积分，保留 99999×1ms 脉冲与停流，不恢复旧 10000 上限。裁切时保留 UV/状态，拾取依据实际三角形。NoteSkin106、其 include 与六张原图集需一并保留；Hold 连续镜像采样避免接缝。

当前 Flick 使用固定头高与有理尾部衰减，最大宽度四分之三处保留约 4.76% 头高，实际尾点才为零；不得用旧指数衰减覆盖当前实现。

减速播放使用 WSOLA，音乐输出 `AudioSource.pitch=1`；原始音频仍用于波形和时长，DSP/offset/打击音同步沿用现有通路。

## 更改前后

先找到实际调用链，再选择[相关检查](BUILDING.md)。代码检查不替代原生界面、GPU 与听感验证。默认不使用 Computer Use，只有用户明确要求时才使用。

引入壳子专属功能遵循[非官方兼容约定](COMPATIBILITY.md)，不能把新的时间组机制塞进既有 Sky 分组而改变默认语义。
