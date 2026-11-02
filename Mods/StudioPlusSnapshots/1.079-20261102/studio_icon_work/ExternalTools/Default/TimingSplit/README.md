# 按时序切分 / Split SkyArea by Timing

在制谱器选中一根或多根 SkyArea，工具 → 外置工具 → 按时序切分 SkyArea。可选择 track、BPM 或两者。总范围取所选物件最早起点至最晚终点，每根仅切分自身持续时间；空隙不填补，不延长物件。只使用各自所属时间组的有效时序，包含本组尚未覆盖的基础组事件；同刻事件去重，首尾不重复切。

切点先按原左右边缘的缓动计算，再沿用编辑器的整数坐标采样（最多 128 份）；保留源首尾坐标原 token。子段用线性连接这些采样点，不重新播放原完整缓动。原 groupid 保留；无 groupid 时每根原紫条分配独立空闲组号，其子段相同。无内部切点的物件完全保留原语句。原时间组及其他物件不变。小于 0.001ms 的切分会整次拒绝，禁止生成 0ms 段。执行仅替换所选物件，一次撤销，手动保存。运行期间修改谱面会取消结果。

Select one or more SkyAreas, then Tools → External Tools → Split SkyArea by Timing. Choose track, BPM or both. The overall range is the earliest start to the latest end; split each note only inside its own duration, without filling gaps or extending notes. Notes without internal cuts stay unchanged. Uses effective timing in each source note's timinggroup, including inherited base events until local overrides. Duplicate timestamps are one cut; endpoints are never duplicated.

The editor samples both original eased edges at each cut using its integer coordinate fitting (up to 128 parts). Original endpoint tokens stay exact. Pieces connect samples linearly rather than restarting the original easing. Keeps groupid or assigns a separate unused ID per ungrouped source, shared by its pieces; retains timinggroup and all unrelated source. Rejects intervals below 0.001ms atomically; never generates a zero-duration SkyArea. One undo step, manual Save. Chart changes during execution cancel the result.

Compatibility: base-chart track/BPM and SkyArea groupid retain native semantics. timinggroup/noinput/noclip support follows the previously integrated nonofficial 1.0.6 shell extension; these markers are not original game syntax.
