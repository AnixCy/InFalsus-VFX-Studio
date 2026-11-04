# 自定义 Shader 与程序化效果

0.10.0 的“新建背景…”模板和背景 Shader 游戏导出见 [背景材质契约](BACKGROUND_MATERIAL_EXPORT.md)。下文的后处理模板继续用于屏幕效果预览；两者的顶点/深度/输入不同，不能只修改 Target 名称互换。

0.7.5 的日常工作流是创建效果、编辑 Shader、保存。手动编译与导入资源收在“高级选项”。

## 使用与制作

1. 在“自定义”点“新建 Shader 效果”，选择父目录，外部编辑器会打开 Effect.shader；首次编译成功后自动加入当前时间轴。
2. 修改 Shader 并保存。监听器合并连续保存，后台自动编译并更新；编译期间的新修改会在之后补编。
3. 在 Shader Properties 声明 Float/Range/Int/Color/Vector。名称、显示标签、默认值与 Range 范围自动生成检查器控件，不用填写参数 JSON；纹理和隐藏/时钟字段不生成动画参数。
4. 调整数值，点击旁边 ◆ 打帧；也可开启自动关键帧。界面始终显示当前效果与编译状态，编译错误显示文件/行号并保留上次可用预览。

自定义参数可按标签或 `_Property` 名筛选，空格分隔多个词；× 清空。点击时间轴的自定义关键帧，会自动筛到对应参数并将检查器回到顶部，方便看到数值和帧设置。改 Properties 的显示标签可以改善界面命名；保持属性名与类型不变，才能保留对应动画。给相位、路程、幅度写清楚 Range，避免普通 Float 的自动滑杆范围不适合实际用途。

“打开效果文件夹”继续编辑此前创建的源码工程。匹配版本 Unity 首次定位后保存路径；这是后台编译更新，并非 Player 内即时 HLSL 编译。已编译包的使用者无需安装 Unity、Python 或 SDK。

更新保留实例、区间及匹配属性/类型的默认值和关键帧，一次更新一次撤销。删除或改变类型的参数只在旧修订中保留。老效果工程升级编译工具前备份生成脚本与描述，Shader 源码不改；显式 AutoParameters=false 仍使用手动描述。

.vfx/cache 是生成的 Unity 编译工程，.vfx/build 保存编译修订，latest-build.json 仅指向成功结果。自动更新可关闭并持久保存，目前监听所选的一个源码工程；空闲只比较文件信息，变动后计算内容哈希。高级入口仍支持 Build.cmd、VS Code 构建任务和已编译包导入。

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

新工程默认 AutoParameters=true，描述由工具维护；高级手动包可设 false 并提供 Parameters。

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

## Agent 与声明式编排

Agent 可直接修改 `Effect.shader` 并保存；已打开且选中的源码工程沿用相同自动更新链路。`.vfx/build-status.json`、`.vfx/latest-build.json` 和构建目录中的 `shader-properties.json` 可供程序读取，不需要靠截图猜测状态或参数名。示例见 [Wave Lines](../../templates/wave-lines/README.md)。

需要生成一个演出工程副本时，可提供编排 JSON，再使用隔离 Studio 检查入口；参数求值、添加帧和保存仍使用现有共享事务与实际编辑器格式，不直接拼内部工程结构：

```powershell
# 在源码工程中编译；也可让已打开的 Studio 监听保存。
& '<源码工程>/.vfx/Build.ps1' -UnityEditor '<匹配的 Unity.exe>'

# 在仓库根目录；PreviewPath 必须是可丢弃的测试副本，OutputDirectory 必须不存在。
& ./scripts/Check-PreviewRuntime.ps1 `
  -PreviewPath '<独立 Studio 测试副本>' `
  -OutputDirectory '<新的生成与检查目录>' `
  -AuthoringSpec '<sequence.json>'
```

编排字段：`FormatVersion: 1`、`Name`、`SourceDirectory`、`BackgroundPath`、`DurationSeconds`、`Defaults`、`Tracks`、`CheckTimes`。Source/图片相对路径以编排文件所在目录为基准，也接受明确的绝对路径。每条 Track 用 Shader 的 `Property` 标识，Keys 包含 `Seconds`、数组 `Value`、可选 `Ease`（默认 Linear，作用于到下一帧的区间）。每条动画显式写起点；Float 是单值数组，Color/Vector 是四值数组。CheckTimes 提供 2–16 个需要呈现不同输出的验算时刻。

成功输出 `AuthoredEffect.vfx.json`、`authored-effect.json` 与常规检查报告。编排仅用于生成新工程，不会自动合并 UI 后续编辑，也不会覆写输入。已有人工调整以当前保存的 `.vfx.json` 为准，Agent 应先读取当前工程，再在副本上操作，避免用旧编排覆盖新工作。

仅在明确需要视觉检查时加 `-AuthoringFrames`，将 CheckTimes 输出为显示颜色的 PNG；默认只做数值验证，不生成这些渲染图。

当前限制：只监听当前选中的一个源码工程；编译需要等待外部 Unity；工程分享仍需自定义资源缓存和图片，尚无完整资源打包器。界面可操作性、物理键鼠与视觉需人工验收；后台检查不等于完成了这些验收。
