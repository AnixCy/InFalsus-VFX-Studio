# 可搬迁工程与音频驱动来源保存

当前是无界面的创作基础设施，不接入现有 Studio 工程管理，不替换安装。现有 `.vfx.json` 和游戏 addon 格式不变。新容器用于验证数据往返和后续适配，不能直接当作 Studio 工程或 addon 打开。

## 目录与数据

```text
ExampleWorkspace/
  workspace.vfxwork.json
  preview.bundle                 # 沿用原逻辑地址；也可能是其他 Bundle 相对路径
  images/mask.png                # 原 file: 纹理地址
  media/audio.wav                # 若声明了主音频，扩展名沿用输入文件
  media/background.png           # 若声明了主背景
  sources/effect/Effect.shader    # 可选，显式声明
  sources/effect/Noise.hlsl       # 保留作者指定的 include 相对布局
```

容器包含四部分：

| 字段 | 含义 |
| --- | --- |
| `ManualProject` | 原人工 AuthoringProjectDocument，保留工程、轨道、关键帧身份与参数提示；AssetRoot 改为 `.` |
| `Resources` | 稳定资源 ID、种类、相对路径、字节数和 SHA-256；不存原机器绝对路径 |
| `AudioDrivers` | 驱动 ID、稳定 TargetTrackId、原音频资源/文件 hash、解码 PCM 指纹及完整 recipe |
| `Bake` | 派生输出通道、源 ContentId/Revision、完整输入/输出摘要和采样/误差预算，可丢弃重建 |

主音频和背景通过资源 ID 引用。音频分析缓存只按 recipe 的 CacheKey 关联，不自动塞进作品包；在新机器可以重新分析，再按解码指纹校验。资源原始文件 SHA 与解码 PCM SHA 是两种身份，不能混用。

后续宿主需把这两个资源 ID 解析回自身的音频/背景字段；`BakedRuntime()` 只返回既有参数运行文档，不自动创建 AudioSource、设置背景图片或调用播放。

## 基本调用

实现入口是 [src/CreatorWorkspace](../../src/CreatorWorkspace/README.md)。宿主负责现有 `.vfx.json` 的解码、选择音频/背景、准备稳定快照与 JSON codec。本库不解析音频、不操作窗口，也不安装游戏资源。

```csharp
var prepared = WorkspaceCollector.Collect(manualProject, originalProjectDirectory,
    audioFile, backgroundFile, explicitSourceFiles, cancellation);
var document = prepared.Snapshot();
document.AudioDrivers = new[] {
    WorkspaceModel.Link(document, "low-brightness", targetTrackId,
        document.AudioResourceId, analysis, recipe)
};
var baked = WorkspaceModel.Bake(document,
    new Dictionary<string, AudioAnalysis> { [analysis.CacheKey] = analysis }, bakeSettings,
    cancellation: cancellation);

// encode/decode 为宿主提供的 CreatorWorkspaceDocument JSON codec。
var store = new WorkspaceStore(encode, decode);
var opened = store.Publish(prepared.WithDocument(baked), freshDirectory, cancellation);
var relocated = store.Relocate(freshDirectory, anotherFreshDirectory, cancellation);
var snapshot = relocated.Snapshot();
// 只有 Current 的派生结果可以取运行数据；资源完整性由 Open/Inspect 单独验证。
PackageDocument runtime = WorkspaceModel.BakedRuntime(snapshot);
```

`Collect` 自动收集原工程所引用的 Bundle、`file:` 纹理，以及声明的主音频/背景。Bundle 与纹理保留相对地址，内部材质/纹理地址不变。额外源码用 `SourceResource` 逐文件声明到 `sources/` 下，原始字节不改写。不会递归抓整个目录、Unity Library、编译历史、日志或未引用音乐。

`PreparedWorkspace` 是内存计划，原绝对路径只用于复制。`Snapshot()` 与 `WithDocument()` 不暴露内部可变数据；后者不允许把另一个资源清单偷偷套在原输入映射上。资源清单要变化时重新 Collect 到新计划。

## 关联保存与重复烘焙

每个 driver 绑定 `TargetTrackId` 与明确的 node/property，同时保存音频原文件 hash 和解码 PCM 指纹。改变数组顺序不会换绑到另一条轨道；删除/改名导致绑定不匹配时明确拒绝，必须显式重新 Link。Link 同 ID 可以替换已失效的旧关联，但不自动猜新目标。

`WorkspaceModel.Bake` **只读取 ManualProject 的通道**，不会从已有 Bake 输出取基底。原关键帧身份、缓动、曲线、资源选择和参数提示保持独立；重复调用会生成同一派生结果。每次重新提交前还应由未来宿主检查工程代次，并通过原 AuthoringSession 一次事务应用，不在本库另建撤销系统。

烘焙状态分为：

- `None`：尚无输出。
- `Current`：输入与输出摘要一致，并通过既有运行数据结构校验。
- `Stale`：recipe、原动画、曲线、时间、资源清单、身份或烘焙设置发生变化；旧结果保留供对照，不能冒充当前输出。
- `Invalid`：派生数值被改动、摘要不匹配或运行数据不合法；原人工动画仍单独保留。

摘要使用有版本的规范二进制编码、固定 DTO 类型白名单、字典键排序和 IEEE 数值表示，不依赖 JSON 排版或字典插入顺序；不把 .NET 核心库程序集版本写入摘要。它是变更检测，不是不可伪造的签名。当前只做 Windows/.NET 自动验证，未宣称已在 Unity/Mono 或 macOS 实际回读。

摘要与资源检查适合在打开、保存、修改和烘焙边界执行，不应逐帧计算。OpenedWorkspace 缓存自己的 BakeStatus；后续拿到可编辑 Snapshot 并修改时，应重新求状态。

## 文件保存与搬迁

`Publish` 和 `Relocate` 只允许新目录。先在目标旁的自有暂存目录复制声明文件，逐文件校验长度/hash，完成容器 codec 回读与资源验证，再以一次目录重命名提交。原项目、音频与背景不修改或移动。改动源文件导致复制不符、取消、编码失败或目标已存在，均不发布半成品。

`Relocate` 复制打开时的一致快照，而非监听原目录后续修改。提交重命名是完成点，完成后的取消不会把已提交的完整副本伪报成未完成。复制不是打包成单文件，分享时需要整个目录。

`Open` 默认校验全部文件，缺失或损坏则拒绝完整打开；`Open(requireResources: false)` 可保留数据用于修复，同时返回稳定资源 ID、相对位置与诊断。`Inspect` 区分缺失、长度变化、hash 变化及链接路径；权限/磁盘 I/O 问题传播，不静默当作文件丢失。BakeStatus 的 Current 仅表示来源数据匹配，不能替代资源检查或 GPU/媒体解码验证。

修改已有容器的元数据使用 `Save(root, snapshot, opened.Version)`：同一工作区身份、文件内容版本与资源均须匹配，编码器必须完整回读；写锁和临时文件 flush 后原子替换。外部修改或旧 token 会拒绝覆盖。资源更新与复制不混进元数据保存；用户可先保留旧副本，再 Collect/Publish 新版本。

## 限制与接入边界

- 创作容器最大 64 MiB；最多 512 个文件，每个 2 GiB、总计 8 GiB。玩家 runtime/addon 文档仍是 4 MiB，不能借创作容器放宽。
- 所有持久化资源路径为可搬迁相对路径，拒绝 `..`、绝对路径、盘符、保留设备名、尾部空格/点、大小写冲突与文件/目录冲突。存储器的元数据、锁和暂存命名空间保留，不可作为资源。
- 拒绝资源/父目录的链接与重解析点，避免复制或清理走到包外。本轮符号链接创建失败，相关实际链接夹具标为跳过；词法路径、冲突和复制失败清理已实测。
- 源码只按显式文件布局复制。不会解析或改写任意源码/JSON 的隐藏绝对路径，也不复制整套可执行 Shader workspace；编译器路径、工具链部署和源码热更新重连由未来适配层处理。
- 调用方负责保证 analysis 的 PCM 确实由所声明音频解码得到；本库校验已捕获的文件/PCM/cache 身份，不替宿主做媒体解码证明。
- 当前容器是独立 prototype，不自动升级旧工程，不接 UI，不新增玩家依赖。现有 Studio 和 addon 支持范围保持原状。

## 自动验证

```powershell
& .\scripts\Check-Workspace.ps1 -OutputDirectory '<新的测试目录>'
```

需要制作端 .NET SDK 9。夹具生成合成 PCM/WAV、明确标注的占位 Bundle/图片和显式 Shader/include 源文件；不启动 Unity/游戏、不播放音频、不看截图。复制后断开原输入与第一份包，再在新根目录回读资源、recipe、原关键帧与烘焙曲线，并独立密集采样对照。

还验证重复烘焙不叠加、recipe/原曲线变更失效、输出篡改、稳定轨道重排、显式重新关联、缺失/修改媒体、保留名/大小写/文件目录冲突、codec 失真、外部修改、写锁、取消和半成品清理。输出检查报告会记录跳过项；不将占位文件验证写成真实 GPU/视听验收。
