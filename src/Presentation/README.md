# 共享演出核心

从项目既有创作基础设施迁入，原始 14 个文件的来源与 SHA-256 见 [ORIGIN.json](ORIGIN.json)。后续仅 AuthoringSession.cs 增加可撤销材质图替换并另列当前哈希，其余 13 个文件保持原样。本目录不依赖 Unity 或具体编辑器。

- `core/`：时间域、播放快照、资源契约与可随机定位的时间轴求值。
- `portable/`：以 `netstandard2.1` 编译核心，JSON 编解码由宿主提供。
- `authoring/`：参数目录、临时预览、工程数据、事务、撤销重做和可靠保存。

Studio+ 时钟、GUI、文件对话框和 GPU 资源归 `../StudioPlusPreview`。界面已暴露关键帧与常用缓动；ReplaceGraph 是一次独立事务，提交前不可再通过旧参数目录编辑新图。

本次未迁入游戏适配器、既有离线渲染器或提取素材，避免将运行平台和媒体依赖带入共享层。测试和构建入口见 [开发说明](../../docs/vfx/DEVELOPMENT.md)。
