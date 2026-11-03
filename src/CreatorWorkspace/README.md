# 可搬迁创作工程基础设施

`netstandard2.1` 制作端独立库，引用现有 authoring 与 AudioReactive；不被 Studio、游戏或 Loader 项目引用，没有 Unity、JSON 库、解码器或进程依赖。

`WorkspaceCollector` 收集声明资源，`WorkspaceStore` 在新目录发布/搬迁并可靠保存，`WorkspaceModel` 关联稳定轨道、原音频、recipe、原人工动画和可丢弃的烘焙结果。

- 独立容器是 `workspace.vfxwork.json`，不是现有 Studio `.vfx.json` 或游戏 addon。
- 人工动画始终保存在 `ManualProject`；`Bake` 独立保存。重复烘焙不会使用上次输出继续叠加。
- Bundle 与 `file:` 纹理地址保留相对布局；主音频/背景和显式源码一同复制，不收集未引用文件和编译缓存。
- 保存保留外部修改检查、codec 回读、写锁与原子替换；搬迁只创建新副本，原文件不动。

使用、格式、错误与限制见 [创作工程手册](../../docs/vfx/CREATOR_WORKSPACE.md)。验证使用 [Check-Workspace.ps1](../../scripts/Check-Workspace.ps1)；只创建测试夹具，不启动应用、播放音频或执行视觉检查。
