# 紫条雨 / Make Rain

入口：工具 → 外置工具 → 紫条雨。Start 的 `+` 使用当前播放头。执行只修改内存，Ctrl+Z 一次撤销，Ctrl+S 手动保存。可先点“计算物件数”而不插入。

按输入的时间和 X 范围生成直线雨；单条时长大于 0 ms。每次执行会追加一批，Ctrl+Z 撤销，Ctrl+S 保存。

黑线输出固定为宽度 1、split 100 的 SkyArea；每段分配独立 groupid 和 1 ms 界外辅助段藏头。所有 SkyArea 时长至少 1 ms。默认新建 noinput 组；关闭新建组时必须选择已有 noinput / noclip 组。没有 Y 参数。需要在设置开启天空超界。

参数可在制谱器面板反复调整并记忆；编辑 defaults.json 后点“重载默认值”。脚本每次执行重新读取。Windows PowerShell 和 .NET Framework 为本机内置组件，不需要 Python。工具请求/返回缓存在 VisualUIData/ScriptCache，用后清理。工具文件均在本文件夹内。不要把 shell 指令写进数值字段。

English: Tools → External Tools → Make Rain. The `+` button uses the playhead. Run inserts one undoable in-memory batch; Save writes the chart. Count-only generation does not insert. Generate straight rain in the exact time/X bounds, with positive durations. Run appends one undoable batch; Ctrl+S saves.

Generated blacklines are head-hidden, width-1 SkyAreas at split 100, with strictly positive durations and unique group IDs. New groups default to noinput. No Y parameters. Uses built-in Windows PowerShell/.NET Framework. All tool files are local to this folder. This relies on the editor copy's existing nonofficial timinggroup/noinput syntax.
