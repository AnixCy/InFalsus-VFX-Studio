namespace InFalsusStudio.NativeEditorUI {
 public static partial class UILocalization {
  static void AddEnglishExtras(){
   string rows=@"
画面微调|Visual timing adjustment
画面微调须为 -200～200 ms|Visual adjustment must be between -200 and 200 ms
游玩音频缓冲|Gameplay audio buffer
FMOD 独立游玩音频；缓冲与延迟设置在下次进入游玩时生效。|Independent FMOD gameplay audio. Buffer and timing changes apply the next time gameplay starts.
游玩延迟|Gameplay latency
延迟须为 -1000～1000 ms|Latency must be between -1000 and 1000 ms
仅游玩模式生效；正值使画面与判定相对音乐延后，负值提前。|Gameplay only. Positive values delay visuals and judgement relative to music; negative values advance them.
未找到 FFmpeg，请在视频渲染设置中指定 FFmpeg。|FFmpeg was not found. Set its path in video rendering settings.
OGG 文件校验失败。|OGG file verification failed.
OGG 编码失败：|OGG encoding failed:
侧边音符视觉增强|Side note visual enhancement
开启（双侧）|Both sides
仅左侧|Left only
仅右侧|Right only
视频渲染|Video render
渲染范围与时间|Render range and time
时间范围|Time range
全曲|Whole chart
循环段|Loop range
自定义|Custom
开始时间|Start time
结束时间（0=曲末）|End time (0 = chart end)
开始设为当前|Set start to current time
结束设为当前|Set end to current time
视频规格与编码|Video format and encoding
分辨率|Resolution
帧率|Frame rate
视频编码器|Video encoder
画面元素与混音|Picture and audio
显示游戏 HUD|Include game HUD
包含打击音效|Include key sounds
首尾淡入淡出（0.5 秒）|Fade in / out (0.5 seconds)
FFmpeg 与导出|FFmpeg and export
未找到 FFmpeg，请指定 ffmpeg.exe。|FFmpeg not found. Choose ffmpeg.exe.
FFmpeg 已就绪：|FFmpeg ready: 
指定 FFmpeg 路径|Choose FFmpeg executable
开始渲染视频|Render video
正在渲染视频|Rendering video
取消渲染 (Esc)|Cancel render (Esc)
正在取消…|Cancelling…
已导出：|Exported: 
渲染未完成：|Render incomplete: 
渲染未开始：|Could not start rendering: 

游玩模式|Gameplay
返回操作设置|Back to operation settings
鼠标灵敏度|Mouse sensitivity
仅影响游玩模式；鼠标倍率在系统鼠标移动的基础上乘算。|Gameplay only. Multiplies mouse motion after system sensitivity.
每段|Every segment
内置游玩模式|Built-in gameplay
内置游玩（Esc 退出）|Built-in gameplay (Esc to exit)
通过游玩热键从当前时间进入；1 秒预备，Esc 返回。|Start from the current time using the gameplay shortcut; 1-second lead-in, Esc to return.
编辑时暂停钻石动画|Pause diamond animation while editing
打开谱面文件夹|Open chart folder
当前谱面文件夹不存在或尚未打开谱面。|No chart folder is open, or the folder no longer exists.
无法打开谱面文件夹：|Could not open chart folder: 
撤销 · 无可撤销操作|Undo · No actions to undo
重做 · 无可重做操作|Redo · No actions to redo
鼠标轨迹线|Pointer tether
轨道十字定位线|Track crosshair
判定显示|Judgement display
轨道按键文字|Track key labels
侧轨红色三角形|Native red side triangles
血条|Life gauge
垂直同步（跟随屏幕）|VSync (match display)
显示帧时间波动|Frame-time details
平滑播放显示|Smooth visual playback
播放时分摊内存回收|Distribute garbage collection during playback
将自动内存回收分摊到更多帧；内存增长时自动加大预算。暂停或关闭选项后恢复默认。|Distributes automatic garbage collection across more frames and raises its budget when memory grows. Pause or disable to restore the engine default.
同步开启时跟随屏幕刷新率；关闭后使用上方帧率上限。同步可能增加少量输入延迟，可随时切换。|VSync follows the display refresh rate. Turn it off to use the frame cap above. VSync may add some input latency; switch at any time.
空域 X 最小值|Air X minimum
空域 X 最大值|Air X maximum
开启天空超界后，可自定 X 网格范围（百分比）。原轨道为 0～100；范围只影响标尺和吸附，不移动现有物件。|With sky overflow enabled, set an X grid range in percent. The original track spans 0..100. This only affects the ruler and snapping; existing notes stay in place.
X 网格范围须在 -10000～10000 内，最小值小于最大值。|X grid bounds must be within -10000..10000, with minimum below maximum.
目标帧率|Target frame rate
无限制|Unlimited
允许天空物件超界|Allow out-of-bounds sky notes
未打开谱面|No chart open
操作设置已保存。|Operation settings saved.
操作设置保存失败；上次设置已保留：|Could not save operation settings; previous preferences retained: 
背景已应用到视图；全局保存后记住。|Background applied to view; use Save to remember it.
背景已清除。|Background cleared.
当前组名称|Name
重命名当前组|Rename
删除空组|Delete empty
非官方时间组：按各组起点 BPM 估算物量；尚未在目标壳子验证判定规则。|Unofficial timinggroups: counts use each group's starting BPM. Judgment rules remain unverified in the target runtime.
非官方时间组：各组独立 BPM 的候选计数，尚非本体判定认证。|Unofficial timinggroups: provisional counts use each group's BPM. These are not verified game judgments.
配对谱面的当前时间组内容不匹配，已取消补入。|The paired chart does not match the current timinggroup. Import cancelled.
配对谱面的 BPM 超出支持范围。|The paired chart BPM is outside the supported range.
当前时间组存在冲突的 BPM，请手动处理。|This timinggroup has conflicting BPM events. Resolve them manually.
未知的组属性。|Unknown timinggroup property.
默认group 无组头属性。|Default group has no header properties.
 行：时间组不能嵌套。|: timinggroups cannot be nested.
 行：组属性语法无效。|: invalid timinggroup property syntax.
 行：时间组名称无效。|: invalid timinggroup name.
重复的时间组名称：|Duplicate timinggroup name:
 行：groupend() 没有对应的时间组。|: groupend() has no matching timinggroup.
 行：应为 hidegroup(ms,0/1)，时间须在 0～86400000 ms。|: expected hidegroup(ms,0/1), with time from 0 to 86400000 ms.
 行：chart 头部只能放在基础组。|: the chart header must be in the default group.
 行：时间组语法无效。|: invalid timinggroup syntax.
 缺少 groupend()。| is missing groupend().
[非官方时间组] |[Unofficial timinggroup] 
目标时间组不存在。|The target timinggroup does not exist.
不能把时间组边界作为物件移动。|Timinggroup boundaries cannot be moved as notes.
只能移动物件、BPM、track 和 hidegroup 事件。|Only notes, BPM, track and hidegroup events can be moved.
时间组边界已变化。|The timinggroup boundaries changed.
目标 timinggroup 已不存在，请重新选择。|The target timinggroup no longer exists. Select it again.
选中的物件已变化，请重新选择。|The selected notes changed. Select them again.
名称限 1～64 个中文、字母、数字、下划线或连字符，首字符不能为数字。|Use 1 to 64 letters, Chinese characters, digits, underscores or hyphens. The first character cannot be a digit.
时间组名称已存在。|This timinggroup name already exists.
时间组名称无效。|Invalid timinggroup name.
时间组不存在或新名称重复。|The timinggroup does not exist or the new name is already in use.
基础组不能删除。|The default group cannot be deleted.
请先移出该组物件及事件，再删除空组。|Move this group's notes and events elsewhere before deleting the empty group.
timinggroup 已变化，请重新选择。|The timinggroup changed. Select it again.
时序语句已变化，请重新选择。|The timing statement changed. Select it again.
该字段不可编辑。|This field cannot be edited.
请输入有效数值。|Enter a valid number.
hidegroup 只接受 0（显示）或 1（隐藏）。|hidegroup accepts only 0 (show) or 1 (hide).
BPM / 拍数须大于 0。|BPM and beats per measure must be greater than 0.
继承行已变化，请重新选择。|The inherited row changed. Select it again.
全谱初始 BPM / 拍号不能删除。|The initial chart BPM and time signature cannot be deleted.
此处只能编辑当前时序语句。|Only the current timing statement can be edited here.
谱面检查|Chart check
时序 / 滚速事件|Timing / scroll events
歌曲工程 / 四个难度|Song project / four difficulties
背景 / 窗口|Background / window
拟合总物量；判定时刻与首尾判定规则尚未完全确定。|Fitted total count; tick times and endpoint rules are not fully established.
选中物件以查看候选判定数与起始 BPM。|Select a note to inspect provisional ticks and starting BPM.
导出当前播放头估算报告…|Export count estimate at playhead...
估算模型 A：Hold 按头部、周期判定与尾部计数；Sky 按开始与周期判定计数。具体判定时刻及跨 BPM 行为尚未确定。track 只影响视觉滚速。|Estimate A counts Hold heads, periodic ticks and tails, and Sky starts and periodic ticks. Exact tick times and cross-BPM behavior remain unverified. track affects visual scroll speed only.
估算总物量：|Estimated total count:
匹配实测参考：|Measured reference match:
播放头估算 @ |Estimate at playhead @ 
播放头分项：|Playhead breakdown:
选中 ID |Selected ID 
起始 BPM |Starting BPM 
间隔 |Interval 
｜跨 BPM（按起始 BPM 估算）| / crosses BPM (starting-BPM estimate)
关闭|Close
当前相机采用经图像校准的模型参数，50° 视场角为该模型的参考值。|The current camera uses image-calibrated model parameters, with a reference field of view of 50 degrees.
恢复图像校准默认值|Restore image-calibrated defaults
加载相机参数 JSON…|Load camera JSON...
导出相机参数 JSON|Export camera JSON
加载参考 PNG 叠加图…|Load reference PNG overlay...
显示参考叠加图（F1 隐藏操作界面）|Show reference overlay (F1 hides controls)
参考视频视速为 4.50，截图视速为 1.00。比较物件间距前请先统一视速。|The reference video uses scroll speed 4.50; screenshots use 1.00. Match scroll speed before comparing note spacing.
加载本地文件：输入完整路径|Load a local file: enter the full path
本地文件路径|Local file path
加载路径中的 SPC|Load SPC from path
加载路径中的音频|Load audio from path
滚轮向上：下一条网格线；向下：上一条网格线。|Wheel up: next grid line. Wheel down: previous grid line.
滚轮每格输入单位|Input units per wheel step
播放连续进行。启用节拍网格时，暂停或定位会将播放头吸附到网格线，原始语句中的时间保持原值。|Playback is continuous. With the beat grid enabled, pause and seek snap the playhead to a grid line. Source timestamps stay unchanged.
垂直视场角：|Vertical field of view:
相机俯角：|Camera pitch:
相机高度：|Camera height:
相机后移：|Camera distance:
侧轨高度：|Side lane rise:
侧轨横距：|Side lane run:
空域高度：|Sky height:
Flick 固定高度：|Flick fixed height:
Flick 斜端面偏移：|Flick cap skew:
参考图透明度：|Reference image opacity:
当前输入单位：|Current input unit:
；不足一格的输入会累积。|; partial steps accumulate.
上次滚轮输入：|Last wheel input:
 格网格线| grid steps
整合插件最近三版；可在设置中随时查看。|Latest three integrated plugin releases. Reopen from Settings.
知道了|Got it
谱面或 timinggroup 已切换，请重新选择。|The chart or timinggroup changed. Select it again.
 拍| beats
同刻存在多条事件；保留所有语句，按列表顺序以后者生效。|Events share a timestamp. All statements are retained; the last in list order takes effect.
从配对明文补入 BPM / 拍号…|Import BPM / meter from paired text...
track 用法说明|About track events
track 只改变本组物件滚速，不改变歌曲播放速度。0 为停流，负数为反向滚动。BPM 控制本组节拍网格；统一流速可辅助编辑停流和反向段。|track changes this group's note scroll speed, not music playback speed. Zero stops scrolling; negative values reverse it. BPM controls this group's beat grid. Uniform scroll speed helps edit stopped or reversed sections.
高级：未识别内容提示（|Advanced: unrecognized content (
谱面已切换，请重新操作。|The chart changed. Try the action again.
请暂停播放后编辑。|Pause playback before editing.
谱面已切换。|The chart changed.
谱面或 timinggroup 已切换。|The chart or timinggroup changed.
松开右键\n空白处取消|Release RMB\nBlank = cancel
选区操作 · 在按钮上松开右键|Selection / release RMB on a button
快捷菜单 · 在按钮上松开右键|Quick menu / release RMB on a button
点立得 · 停留 0.5 秒展开，松开选择|Quick Place / hover 0.5s to expand; release to select
Flick 方向 · 松开选择|Flick direction / release to select
默认宽度 · 仅中央四轨生效|Default width / four center lanes only
网格线|Beat grid
点立得|Quick Place
剪贴|Cut
退出|Exit
 轨宽| lanes
开启网格线|Grid ON
关闭网格线|Grid OFF
对齐网格线|Align to grid
不创建|No creation
向左 / 黄色|Left / Yellow
向右 / 绿色|Right / Green
1 轨宽|1 lane
2 轨宽|2 lanes
3 轨宽|3 lanes
4 轨宽|4 lanes
粘贴物件已变化。|The pasted notes changed.
节点编辑：开启 · |Node editing: ON · 
节点编辑：关闭 · |Node editing: OFF · 
 切换| to toggle
时间拖动 · |Drag time · 
位置拖动 · |Drag position · 
请选择 1～256 个 Flick / SkyArea，不要混选其他物件。|Select 1–256 Flick / SkyArea notes without mixing other note types.
已原样捕获 |Captured without changes: 
 个物件；宽度、缓动、时长和类型均保留。| notes; widths, easing, durations and types are preserved.
请选择工具要求的物件，不要混选其他类型。|Select the note types required by the tool without mixing other types.
段落范围：|Time range: 
尚未选择段落时间范围。|No time range selected.
在轨道上选择起止时间|Select start and end times on the track
请先使用段落选择点击开始和结束时间。|Use range selection to choose the start and end times first.
当前时间组在所选范围内没有匹配的 timing。|No matching timing events in this timinggroup and range.
请先选择时间范围。|Select a time range first.
当前时间组的选区没有 BPM 或 Track。|No BPM or track events in the selected range of this timinggroup.
请选择一根或多根 SkyArea，不要混选其他类型。|Select one or more SkyAreas without mixing other note types.
所选 SkyArea 时长或边缘无效。|The selected SkyArea has an invalid duration or edge.
总切分超过4096段，请缩小选择范围。|Splitting would exceed 4096 segments. Select fewer notes.
切分会产生小于 0.001ms 的段，已取消。|Split cancelled: it would create segments shorter than 0.001 ms.
所选紫条持续时间内没有所选时序事件，无需切分。|No selected timing events occur within the selected SkyAreas; no split is needed.
点击按键录入新组合，修改后自动保存。|Click a binding to record a new shortcut. Changes save automatically.
热键配置无效，已使用默认按键。|Invalid shortcut configuration; using defaults.
保留自定义热键；以下操作需重新绑定：|Custom shortcuts retained. Rebind these actions: 
热键已保存。|Shortcuts saved.
保存失败，保留原热键：|Could not save; previous shortcuts retained: 
按下新组合键；Esc 取消录入。|Press a new shortcut; Esc cancels recording.
热键|Shortcuts
已取消录入。|Recording cancelled.
‹ 返回操作设置|‹ Back to controls
请按键…|Press a key…
默认|Default
清除|Clear
固定交互|Fixed controls
Ctrl 多选 · 方向键微调\nEsc 取消\nEnter 确认输入 · F11 恢复窗口\n文字输入框保留常规文字编辑按键。|Ctrl: multi-select · Arrow keys: nudge\nEsc: cancel\nEnter: confirm input · F11: restore window\nText fields retain standard text-editing shortcuts.
恢复全部默认热键|Reset all shortcuts
基准 BPM · 视图|Reference BPM · View
快捷工具|Quick tools
应用本体 1.07b 视图|Apply original game 1.07b view
恢复原图像校准视图|Restore image-calibrated view
Flick 默认宽度|Default Flick width
宽度须大于 0 且不超过 100%|Width must be greater than 0 and at most 100%
Flick 默认方向|Default Flick direction
向左|Left
向右|Right
Ctrl + S 保存　·　Ctrl 多选　·　Alt 手柄　·　Alt+T 切换时间/位置拖动\n鼠标右键：点立得菜单　·　F11 恢复窗口|Ctrl+S: save · Ctrl: multi-select · Alt: handles · Alt+T: switch time/position dragging\nRight-click: Quick Place menu · F11: restore window
 按住预览，松开返回；|: hold to preview, release to return; 
 切换预览，不返回；|: toggle preview without returning; 
 选择工具；|: selection tool; 
 段落选择。点击播放进入正式播放。|: range selection. Click Play to enter full playback.
背景已应用到视图，设置将自动记忆。|Background applied; preferences are remembered automatically.
外观/窗口偏好：确认后自动记忆，不改歌曲谱面。|Confirmed appearance and window preferences are remembered automatically without editing the chart.
3. 坐标修复|3. Coordinate repair
坐标修复|Coordinate repair
按时间和首尾连接分配组号（含前后 2ms 容差）；新建 Sky 仍不指定组。|Assign groups by time and touching endpoints within ±2 ms. New SkyAreas still omit groups.
infstudioplus 最近三版更新；可在设置中随时查看。|Recent infstudioplus releases. Reopen from Settings.
ExternalTools 中没有可用的工具。|No tools available in ExternalTools.
请输入整数。|Enter an integer.
请输入有限数值。|Enter a finite number.
已重载工具及默认参数。|Tools and default parameters reloaded.
捕获所选 Flick / SkyArea|Capture selected Flick / SkyArea
捕获所选 SkyArea|Capture selected SkyArea
 个物件 / 路径| notes / paths
计算事件数|Count events
计算物件数|Count notes
返回|Back
主界面网格|Main editor grid
跟随主界面 · 1/|Follows main grid · 1/
请选择 1～256 个可见 SkyArea。|Select 1–256 visible SkyAreas.
所选路径须有效且时长大于 0 ms。|Selected paths must be valid and longer than 0 ms.
隧道生成至少需要两条所选路径。|Select at least two paths to generate a tunnel.
已捕获 |Captured 
 条路径；原物件未修改。| paths; original notes are unchanged.
请先在设置 → 操作开启天空超界。|Enable sky overflow in Settings → Controls first.
请选择 noinput / noclip 组或开启新建组。|Select a noinput / noclip group or enable creation of a new group.
正在生成…|Generating…
生成期间谱面或时间组发生变化，请重新执行。|The chart or timinggroup changed during generation. Run the tool again.
请暂停后重新执行。|Pause playback and run the tool again.
预计处理 |Estimated to process: 
已处理 |Processed: 
预计生成 |Estimated to generate: 
已生成 |Generated: 
 条黑线。| black lines.
 段 SkyArea。| SkyArea segments.
 行谱面操作。| chart operations.
谱面未修改。|The chart is unchanged.
Ctrl+Z 一次撤销；Ctrl+S 保存。|Ctrl+Z undoes the operation; Ctrl+S saves.
已取消生成，谱面未修改。|Generation cancelled; the chart is unchanged.
使用导出音频时，请将偏移设为 0。|Set the offset to 0 when using the exported audio.
取消导出|Cancel export
按当前偏移导出音频|Export audio with current offset
打开文件夹|Open folder
打开文件夹失败：|Could not open folder: 
正在导出 |Exporting 
请先加载音频。|Load audio first.
请先暂停预览。|Pause preview first.
音频偏移无效。|Invalid audio offset.
偏移为 0，无需导出重复文件。|The offset is 0; no duplicate export is needed.
请先保存谱面或打开歌曲工程。|Save the chart or open a song project first.
当前音频文件路径不可用。|The current audio file path is unavailable.
正偏移不小于音频长度，无法生成空音频。|The positive offset is at least the audio duration; an empty audio file cannot be exported.
导出文件超过 256 MiB 或音频格式无效。|The export would exceed 256 MiB or the audio format is invalid.
导出失败：|Export failed: 
导出已取消或工程、难度、音频、偏移发生变化。|Export cancelled, or the project, difficulty, audio or offset changed.
读取音频数据失败。|Could not read audio data.
工程或偏移已变化。|The project or offset changed.
WAV 文件校验失败。|WAV validation failed.
导出完成：|Export complete: 
。使用导出音频时偏移设为 0。|. Set the offset to 0 when using the exported audio.
导出未完成：|Export incomplete: 
已取消|Cancelled
 默认宽度：| default width: 
请选择 1–4 轨 Tap / Hold，或切换到对应放置工具。|Select Tap / Hold notes in lanes 1–4, or use the corresponding placement tool.
键宽已到可用边界。|Note width is already at its available limit.
选择已变化。|The selection changed.
键宽已调整：|Note width adjusted: 
谱面已修改，请重新检查。|The chart changed. Run the check again.
当前检查项没有同一时间组内需要移除的重叠物件。|This check contains no redundant overlapping notes within the same timinggroup.
已选择 |Selected 
 个重叠物件，保留不冲突的原物件；按 Delete 删除。| redundant overlapping notes, retaining non-conflicting originals; press Delete to remove them.
1. 等分切分（仅单条 SkyArea）|1. Split evenly (one SkyArea)
等分切分（单条 SkyArea）|Split evenly (one SkyArea)
分段数量 N|Segment count N
切成 N 条 SkyArea|Split into N SkyAreas
转成 N 个 Flick|Convert to N Flicks
记忆森林式拆解（等分）|Alternating segments (even split)
按当前网格切分（多条 SkyArea）|Split at current grid (multiple SkyAreas)
当前细分：1/|Current subdivision: 1/
 拍；两端不足一格也保留。| beat; partial segments at both ends are retained.
按网格切 SkyArea|Split SkyAreas at grid
按网格转 Flick|Convert to Flicks at grid
记忆森林式拆解（按网格）|Alternating segments (grid split)
生成 Flick 方向：|Generated Flick direction: 
分别采样原来的左右曲线，坐标最多 128 份；小 SkyArea 两侧为 Linear，不指定组。记忆森林式拆解从最开始保留紫条，隔一条空一条。Flick 在每段起点采样，不额外生成尾点。|Sample the original left and right curves separately with up to 128 coordinate divisions. New SkyArea edges are Linear and omit group IDs. Alternating mode keeps the first segment, then skips every other segment. Flicks use segment starts without an extra endpoint.
（记忆森林式拆解），可撤销；保存才写盘。| (alternating segments); undo is available and files change only on Save.
点击 Timing 工具后，在轨道上选择段落起止；复制会再选择目标时间。全部操作均可一次撤销。|Choose a Timing tool, then select the source range on the track. Copy also asks for a destination time. Each operation can be undone in one step.
请先暂停并打开谱面。|Pause playback and open a chart first.
无法开始段落选择。|Could not start range selection.
复制 Timing：依次点击来源段落的起点和终点。|Copy Timing: click the start and end of the source range.
删除 Timing：依次点击段落起点和终点。|Delete Timing: click the range start and end.
谱面已变化，请重新选择。|The chart changed. Select again.
无法选择目标时间。|Could not select a destination time.
复制 Timing：点击轨道上的目标起点；右键取消。|Copy Timing: click the destination start on the track; right-click cancels.
目标范围须落在 0–86400000 ms 内。|The destination range must be within 0–86400000 ms.
谱面、时间组或播放状态已变化，请重新执行。|The chart, timinggroup or playback state changed. Run the operation again.
参数改动已应用，请重新选择。|Property changes applied. Select again.
已复制 |Copied 
 条 Timing；一次撤销。| Timing events; undo in one step.
已删除 |Deleted 
快速 Timing 操作已取消。|Quick Timing operation cancelled.
制谱器根目录的 Background 文件夹缺失，请恢复安装目录。|The Background folder is missing from the editor root. Restore the installation folder.
选择背景 PNG / JPG|Choose background PNG / JPG
SkyArea 节点编辑已开启；H 切换重叠节点。|SkyArea node editing enabled; H cycles overlapping nodes.
SkyArea 节点编辑已关闭。|SkyArea node editing disabled.
节点联动：|Linked nodes: 
；时间差 |; time difference: 
节点源物件已变化。|The source notes for this node changed.
SkyArea 边缘交叉。|SkyArea edges cross.
节点超出天空边界。|The node is outside the sky bounds.
节点位置无效：|Invalid node position: 
切换共同节点候选 |Shared node candidate: 
节点 |Node 
  重叠候选键切换|  Use the overlap shortcut to cycle
未绑定|Unbound
鼠标中键|Middle mouse
鼠标侧键 |Mouse side button 
打开谱面 / 歌曲|Open chart / song
按住预览 · 松开返回|Hold to preview · release to return
播放与视图|Playback & view
播放 / 暂停预览 · 不返回|Play / pause preview · keep position
切换重叠候选|Cycle overlapping candidates
显示 / 隐藏界面|Show / hide UI
剪切|Cut
按住显示 / 拖动把手|Hold to show / drag handles
SkyArea 节点编辑切换|Toggle SkyArea node editing
时间 / 位置拖动切换|Toggle time / position dragging
增加 Tap / Hold 键宽|Increase Tap / Hold width
减小 Tap / Hold 键宽|Decrease Tap / Hold width
选择工具|Selection tool
放置左 Flick|Place left Flick
放置右 Flick|Place right Flick
未知操作。|Unknown action.
该按键用于基础交互或窗口操作，请换一个组合。|This key is reserved for basic controls or window actions. Choose another shortcut.
与“|Conflicts with “
”冲突，当前设置未修改。|”; current settings are unchanged.
热键配置包含冲突或保留按键。|Shortcut settings contain conflicts or reserved keys.
已应用本体 1.07b 视图|Original game 1.07b view applied
已恢复原图像校准视图|Image-calibrated view restored
没有未分组的 SkyArea；已有分组保持不变。|No ungrouped SkyAreas; existing groups are unchanged.
已将 |Assigned 
 条未分组 SkyArea 分为 | ungrouped SkyAreas to 
 组；保留 | groups; retained 
 条已有分组物件。可一次撤销，保存才写盘。| already grouped notes. Undo in one step; files change only on Save.
鼠标拖尾特效|Cursor trail effect
新增鼠标拖尾特效（可开关）|Added a cursor trail effect (toggleable)
网格线开关|Toggle beat grid
 轨。| lanes.
放置 Tap|Place Tap
放置 Hold|Place Hold
放置 SkyArea|Place SkyArea
";
   foreach(string row in rows.Split('\n')){var pair=row.TrimEnd('\r').Split('|');if(pair.Length==2)Words[pair[0].Replace("\\n","\n")]=pair[1].Replace("\\n","\n");}
  }
 }
}
