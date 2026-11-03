# 自定义 Shader 与程序化效果

0.4.0 的“自定义”分类提供新建/打开源码模板、外部编辑器与构建更新。已有编译资源仍可通过“导入已编译效果”加入库，拖到统一时间轴创建独立实例。

## 使用与制作

测试包的 `CustomEffects/SampleCompiled/effect.json` 可直接导入。相邻 Bundle 已构建，使用者无需安装 Unity、Python 或 SDK。

制作新效果：在“自定义”点“新建模板”，选择父目录；生成独立的 `Effect.shader`、`effect.json`、工程标记及 VS Code 构建任务。有 VS Code 时直接打开，否则打开文件夹供任意编辑器使用。

修改后按 Ctrl+Shift+B，或点编辑器“构建并更新”/运行模板中的 Build.cmd。首次定位匹配版本 Unity，之后保存工具链位置。成功构建自动加入效果库并更新当前源码关联实例，保留名称、区间及匹配属性/类型的默认值和关键帧。一次更新一次撤销，旧 Shader 修订仍可恢复。删除或改变类型的参数只在旧修订中保留；不会猜测迁移。

模板的 `.vfx/cache` 是可复用的生成工程，`.vfx/build` 保存编译修订，`.vfx/latest-build.json` 只指向最近成功结果。失败保留原效果，状态和日志在 `.vfx`。重叠构建被独占锁拒绝；不覆盖已有源码目录。自动更新可关闭并持久保存，目前监听所选的一个源码工程。

源码构建需要 Unity **6000.3.25f1**。本轮已验证单 Shader 与同目录 `.hlsl/.cginc` 构建、打包脚本副本独立运行及实际 D3D11 导入。当前不提供游戏运行时 HLSL 编译或用户 C# DLL 执行；编辑器更新以外部 Unity 成功编译为界。

仓库命令：

```powershell
& ./scripts/Build-CustomEffect.ps1 `
  -ShaderPath '<源 Shader 文件>' `
  -DescriptorPath '<效果描述 effect.json>' `
  -OutputDirectory '<新的输出目录>' `
  -UnityEditor '<匹配版本的 Unity.exe>'
```

模板见 [effect.json](../../templates/custom-effect/effect.json)、[Shader](../../templates/custom-effect/ProceduralPulse.shader) 和[构建入口](../../scripts/Build-CustomEffect.ps1)。更复杂的材质也可自行构建 AssetBundle，再使用同一描述格式导入。

## 描述契约

根对象包含 `FormatVersion: 1`、`UnityVersion`、`Platform: StandaloneWindows64`、`ColorSpace: Linear`、`Effect`。版本、平台和色彩空间需匹配当前预览。

`Effect` 字段：

| 字段 | 含义 |
| --- | --- |
| `Id`、`Label` | 作者标识与界面名称；导入后以内容哈希标识本地修订 |
| `Target` | `postprocess` 叠加效果，或 `background` 底图材质 |
| `Bundle` | 相对描述文件的资源包路径，不能逃出该目录 |
| `Material` | Bundle 内 Shader 或 Material 的资源入口 |
| `Parameters` | 参数名 `Property`、标签 `Label`、`Kind`、默认数组 `Value`、建议 `Min/Max` |
| `Keywords` | 可选关键词；对应 `_KW_关键词` Float 参数可控制开关 |

参数类型为 `Float`（1 个值）、`Vector/Color`（4 个值），名称与 Shader Properties 类型一致。默认值会保存，但未打关键帧的参数不自动占据可见轨道。每个后处理实例另有“混合强度”，由编辑器合成，不要求 Shader 自行实现。

单描述最多 128 个参数、32 个关键词、256 KiB；单 Bundle 最多 64 MiB。导入核对入口、Shader 支持及参数类型。缓存按内容标识存入当前安装的 `BepInEx/plugins/InFalsusVfx/UserEffects`，重复导入去重；损坏的缓存条目被隔离，不停用整个编辑器。

工程引用缓存资源；分享工程还需携带使用到的自定义资源。当前没有完整工程资源打包器。已有安装移至新版时，应保留对应 `UserEffects` 缓存。

## Shader 接口

| 接口 | 含义 |
| --- | --- |
| `_BlitTexture`、`_BlitTexture_TexelSize` | 后处理输入图像及像素尺寸 |
| `_PresentationTime` | 可定位的全局演出秒数，暂停时固定 |
| `_EffectTime` | 当前片段开始后的秒数 |
| `_EffectDuration` | 有限区间的总时长；持续效果为 0 |
| `_EffectProgress` | 有限区间内 0–1 进度；持续效果为 0 |

后处理第 0 个 Pass 使用全屏三角形，参考模板的 `SV_VertexID` 顶点入口。背景材质绘制在正交背景平面，使用普通平面顶点与 UV。无需创建场景对象或移动相机。

程序化绘制可仅依赖 UV、时间和参数，也可读取输入图像做变形或混合。作者需自行控制循环与采样成本；不以 Shader 数量推断游玩性能。当前只验证离线编辑预览，未部署游戏。
