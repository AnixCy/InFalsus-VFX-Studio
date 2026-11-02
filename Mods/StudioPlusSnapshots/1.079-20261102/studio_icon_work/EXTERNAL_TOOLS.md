# 通用外置工具接口

随程序分发的简明指南见 [工具编写指南](ExternalTools/Docs/工具编写指南.md)，完整字段参考见 [PROTOCOL](ExternalTools/Docs/PROTOCOL.md)。

工具位于编辑器根目录 `ExternalTools/<id>`。文件夹名与 `tool.json` 的 `id` 一致，`script` 指向本目录 PowerShell 脚本。`version` 仍为 1；旧 `blackline`、`timing`、`split`、`timingedit` 输出模式保留，新增工具优先使用 `output: "spc"`。

通用请求提供文档文本、BOM 标记、源行及 SourceId/原 token/时间组、当前时间和时间组、选择、段落范围、捕获物件及工具参数。可通过 `selection`、`gridField`、`captureKind` 和 `groupMode` 声明需要的选择与网格，无需在编辑器内识别工具 ID。实际 DTO 和检查入口见 `Source/UI/ExternalSpcOutput.cs`、`NativeExternalToolDefinitions.cs` 和 `NativeExternalToolRunner.cs`。

脚本接收 `-RequestPath` / `-OutputPath`，把结果写入输出文件。`spc` 接受两类输出：

1. 普通 SPC 文本：可包含官方语句、插件支持的扩展语句、未知函数式语句及时间组块。无组标记时追加到目标组；包含组块时按完整块处理。
2. 版本 1 的 JSON 补丁：

```json
{
  "version": 1,
  "operations": [
    { "action": "append", "scope": "$target", "lines": ["track(1000,1)"] }
  ]
}
```

`append` 使用 `lines`；`scope: "$target"` 为本次目标组，空字符串为基础组，具体组名指定已有时间组，`null` 允许完整时间组块。`replace` 使用 `sourceId` 和 `text`；`delete` 使用 `sourceId`。可用 `expected` 约束原行内容。修改只能针对请求快照中存在且没有变化的源行，同一源行不能重复修改。

结果通过现有语句、分组、几何和作者参数检查后，整批作为一次撤销提交。运行期间文档、时间组或历史版本发生变化会拒绝旧结果。估算不修改文档；保存仍需用户显式操作。数量、文件大小及语句形式也受检查；“任意语句类型”不意味着取消格式校验或替未知语句实现游戏行为。

脚本属于本机外置程序，按用户安装的脚本执行；此接口不是脚本隔离沙箱。仓库只提供作者性脚本，不包含个人缓存或原谱面。

## 随仓库提供的工具

仅包含紫条雨、渐变 Timing 和按时序切分 SkyArea，见 [分发清单](DISTRIBUTION.md)。通用接口继续支持用户自行安装其他脚本；此清单不限制输出语句类型。

时间组、noinput/noclip 等语义属于非官方壳子扩展，见 [兼容性说明](COMPATIBILITY.md)。


## 主界面网格（内部 1.0.77）

清单的 `gridField` 仍指向已有细分字段，但该字段在界面中只读显示主界面当前细分，执行时以主界面值覆盖旧偏好。请求版本继续为 1。
按网格执行时，新增 `gridDivision`、`gridGroup` 与 `grid`：后者为请求范围内主界面实际网格线的整数毫秒时间，含当前组的 BPM 变化和相位；不按每个物件所属组另算。
通用 SPC 工具的每个 `skySources[].grid` 使用同一主界面网格，仍可按工具语义加入原物件首尾；旧字段与输出协议保留。
按数量执行不生成网格快照。执行中若主界面细分或时间组发生改变，待完成的旧请求取消，重新执行即可。
渐变 Timing 的参考 BPM 只负责等效 BPM 到倍率的换算，不再决定网格采样。
