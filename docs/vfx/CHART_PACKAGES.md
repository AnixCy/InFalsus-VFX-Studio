# 随谱演出与整曲包

0.9.0 的默认交付单位是一首完整歌曲：谱师从 Studio 导出 ZIP，玩家解压到游戏 `Charts`。谱面和演出各自可用，演出数据位于歌曲自己的 `vfx/` 下；移除它不会修改谱面、判定或音频。

## 谱师操作

1. 保存 Studio 歌曲工程中的谱面和资料，再保存演出 `.vfx.json`，以保留稳定的发布身份。
2. 在演出视图点击“导出曲包”，选择含 `infalsus.studio.json` 的歌曲目录。也接受已有 `if` 和二进制 SPC 的可游玩曲包。
3. 选择当前演出对应的 MIN/EVO/ULT/FBD 难度，保留默认 ZIP 输出，选择新的输出位置。
4. 将完整 ZIP 发给玩家。其他已声明难度、音频、曲绘随包带齐，当前演出只绑定所选难度。

导出读取磁盘中已保存的歌曲资料。不会自动保存制谱草稿或改写原工程；演出采用当前已提交快照，不包含临时预览覆盖、撤销栈或界面布局。目标已存在时拒绝覆盖，输出不得放进源曲包内部。

源工程由编辑器随附的私有编译工具转换，不要求谱师安装 Python。默认跟随工程的 `spcV2` 和 `rawChartImport` 设置；可以显式选择 ICP1 或 SPC2，选择只影响本次导出。ICP1 使用兼容二进制；SPC2 的 `timinggroup` 等参数仅适用于已适配社区壳，不能作为官方兼容包分发。无法转换的语句明确报错，不静默删除。

已评级的非空难度全部编译；本版难度等级接受 1～99 整数。缺少音频、曲绘或有效等级会阻止导出。OGG/MP3/WAV 音频按原字节复制；曲绘及可选基础背景转换为 PNG，并为缺失难度补齐显示曲绘，不虚构其谱面。`audioOffsetMs` 按编译器语义应用一次；Chart 时间域演出同步补偿，Audio 时间域不重复偏移。Raw ICP1 保留原时间，不额外套用编译偏移。

Studio 源工程采用演出 ProjectId 生成 `if.id`，同一演出工程更新后保持同一曲目身份。已有可游玩曲包则保留原 `if.id`。不要给不同歌曲复用同一个演出工程身份或手工更改 ID；更名文件夹不影响匹配。若目录同时有 Studio 工程和旧 `if`，优先编译 Studio 工程，避免发出旧版谱面。

已有可游玩曲包中的其他难度演出经校验后保留。直接从 Studio 源工程导出时，不会搜索或合并其他目录曾发布的演出；需要多难度演出时，依次对上一轮导出的可游玩曲包更新对应难度。

## 玩家操作

首次安装一个匹配的 IL2CPP Loader，并安装对应的 VFX 运行包；两种 Loader 不混装。关闭游戏后，将运行包解压到游戏 EXE 同级目录，保留 ZIP 内目录结构。已有同名 ChartLoader DLL 时先备份替换，不重复放入第二份。

之后每首歌只需把谱师 ZIP 解压到 `Charts` 并重启游戏。正确结构为 `Charts/Song_example/if`，不要多套一层目录。玩家不运行脚本，不生成 catalog，不安装 Python、Unity Editor、Studio 或后台服务。曲包在两种 Loader 下完全相同。

```text
Charts/
  Song_example/
    if
    audio.ogg
    chart_ULT.spc
    chart_FBD.spc
    jacket_ULT.png
    jacket_FBD.png
    vfx/
      chart-pack.json
      FBD/
        addon.json
        runtime.json
        background.bundle
        background.png       # 可选演出图片
```

不安装演出运行时，已有兼容 ChartLoader 仍可导入普通谱面；没有对应难度的演出则显示原背景。删除 `vfx/` 可单独移除演出；删除整个歌曲目录可移除歌曲。已进入游戏的谱面使用启动时快照，增删或替换后应重启。

## 一次安装的运行包

| Loader | 包内路径 |
| --- | --- |
| MelonLoader IL2CPP | `Mods/InFalsus.Presentation.Melon.dll`、`Mods/InFalsusChartLoader.dll`，三个共享运行库位于 `UserLibs/` |
| BepInEx Unity IL2CPP | `BepInEx/plugins/InFalsusChartLoader.dll`，演出入口和三个共享库位于 `BepInEx/plugins/InFalsusPresentation/` |

ChartLoader 在 BepInEx 中必须放在上述层级，不能移入另一个子目录。发行 ZIP 附简短说明、文件哈希清单和 ChartLoader MIT 许可，不含 Loader、游戏、互操作缓存、歌曲或创作编译器。

实测 Loader 为 MelonLoader 0.7.3 Open-Beta、BepInEx Unity IL2CPP 6.0.0-be.791+b782da4。安装时遵循所选 Loader 的运行时要求，不能把 Studio 使用的 Mono BepInEx 装入 IL2CPP 游戏。首次运行时 Loader 可能需要生成互操作程序集。

当前内置适配以 GameAssembly SHA-256 精确匹配：

- 官方 1.0.7b：`0314adaedf482bb9c3d8d300b69e1fa47235aaf618033c03bd631ade567ab98f`，适配信息核对；本轮没有在官方二进制中实际进歌。
- 社区 1.0.7b：`c7d0c835b88f8b2bde577107ace496a15913491588df85bbd30d756e2375f44d`，两种 Loader 均完成隔离原生加载检查；支持经过边界校验的 SPC2。

其他版本不推测地址。未知版本会跳过演出并记录原因；这不构成对未知版本自制谱加载的兼容承诺。

## 匹配、安全与成本

`vfx/chart-pack.json` 为版本 1 清单，包含 SongId、四槽 Charts、ChartHashes 和 Addons。运行时身份为 `pack:<if.id>` + 难度 + SPC SHA-256。游戏中实际导入的逻辑名为 `<if.id><difficulty>.spc`，与 ChartLoader 保存的只读字节快照核对后才启用演出；换谱不会误套旧演出。

启动和显式 reload 扫描 `Charts`，正常游玩帧不扫描目录、重新哈希或启动编译器。缺文件、校验失败、旧能力、重复 ID 或重复演出匹配均退回原画面；一个曲包清单无效时跳过该曲包演出，普通谱面导入独立处理。缺少导入快照也不推测匹配，因此应使用运行包随附的 ChartLoader，未核对其他导入器兼容性。有效演出更新失败保留最近有效版本，关闭与退出按既有生命周期清理。配置首次自动生成，不覆盖已有设置；旧独立 addon 路径仍兼容。

导出只复制声明的资源，拒绝越界/绝对路径、链接和已存在目标；不递归打包整个工程。单曲资源复制预算 2 GiB，单个运行文档 4 MiB，源谱面文本每难度 8 MiB；图片沿用既有尺寸与字节预算。失败清理自有暂存树，不删除或改写源文件。

演出支持范围没有随打包扩张：通用背景、一个融合色彩实例、五组轨道/UI 透明度。任意自定义 Shader、原始材质组及全屏后处理仍会阻止通用导出；详见 [ADDONS.md](ADDONS.md)。当前玩家后端只绑定背景及逻辑透明度，不带未使用的 Game.Common.Rendering/URP 后处理注册依赖，避免不同 Loader 的程序集命名差异；后续处理阶段应通过独立适配扩展。

## 构建与验证入口

- `src/Addon/ChartPack.cs`：共享曲包契约、路径和指纹。
- `src/Addon.Export/ChartPackExport.cs`：完整歌曲复制、其他难度演出保留和 ZIP 发布。
- `src/StudioPlusPreview/StudioSongPackage.cs`：保存的 Studio 源工程编译适配。
- `src/ChartCompiler`：制作端 stdin/stdout 编译协议；`scripts/Build-ChartCompiler.ps1` 使用核对 SHA 的 Python 3.13.9 x64 官方嵌入包，输出放在插件的 `GameExport/ChartCompiler/`，许可一并保留。
- `src/ChartLoader`：配套谱面组件，MIT 来源见 ORIGIN.json。Melon/Bep 分别使用 `Loader=MelonLoader/BepInEx` 构建；Bep 设置 `LoaderDir` 为对应 `BepInEx/core`。
- `src/GameLoader.BepInEx`：Bep IL2CPP 入口，构建时设置 `GameReferenceRoot` 与 `BepInExRoot`；Melon 构建入口不变。
- `scripts/Build-PlayerPackage.ps1 -OutputDirectory '<新目录>'`：使用已编译 DLL 生成两种直接解压的运行 ZIP；不修改参考游戏。
- `tests/ChartPack.Checks`、`scripts/Check-ChartCompiler.py`、`Check-PreviewRuntime.ps1 -ChartPackSource`、`Check-ChartPackRuntime.ps1`：文件/编译、实际 Studio 导出及两种隔离 IL2CPP 生命周期检查。最后一项仅接受带所有权标记的物理副本和合成夹具。

SPC2 在原生 List 的备用容量中保存元数据；计算 native note stride 必须用底层数组 Length，而非 List.Count。这个差异已经固化到配套 ChartLoader，避免重复将“能解码的谱面”误判为记录宽度错误。

实际游玩、低配置帧时间、安装交互与视觉仍由人工验收，自动记录见 [VALIDATION.md](VALIDATION.md)。
