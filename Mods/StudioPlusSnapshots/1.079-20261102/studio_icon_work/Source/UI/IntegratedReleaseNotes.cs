using System;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using InFalsusStudio;
using InFalsusStudio.Core;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  internal const string AnnouncementBuild="1.0.7-build1030";
  ConfigEntry<string> releaseNotesSeen;StudioBootstrap announcementBoot;
  void InitReleaseAnnouncement(){
   releaseNotesSeen=Config.Bind("ReleaseNotes","SeenBuild","","Last InFalsus Studio+ announcement build acknowledged.");
   harmony.Patch(Method("InitializeReleaseNotes"),prefix:new HarmonyMethod(typeof(AnnouncementHooks).GetMethod("Initialize",F)));
   harmony.Patch(Method("CloseReleaseNotes"),prefix:new HarmonyMethod(typeof(AnnouncementHooks).GetMethod("Close",F)));
  }
  void InitializeAnnouncement(StudioBootstrap owner){announcementBoot=owner;Set(owner,"showReleaseNotes",releaseNotesSeen.Value!=AnnouncementBuild);Set(owner,"releaseNotesScroll",Vector2.zero);}
  void EnsureReleaseAnnouncement(StudioBootstrap owner){if(releaseNotesSeen!=null&&announcementBoot!=owner&&Val<bool>(owner,"initialized"))InitializeAnnouncement(owner);}
  static class AnnouncementHooks {
   static bool Initialize(StudioBootstrap __instance){if(I==null||I.releaseNotesSeen==null)return true;I.InitializeAnnouncement(__instance);return false;}
   static bool Close(StudioBootstrap __instance){
    if(I==null||I.releaseNotesSeen==null)return true;
    Set(__instance,"showReleaseNotes",false);var modifier=Get(__instance,"editModifier");modifier.GetType().GetMethod("Reset",F).Invoke(modifier,null);
    try{I.releaseNotesSeen.Value=AnnouncementBuild;SharedPreferences.Store(I.releaseNotesSeen);}catch(Exception e){I.Logger.LogWarning("Announcement acknowledgement retained for retry: "+e.Message);}return false;
   }
  }
  static readonly ReleaseNotesEntry[] IntegratedReleaseNotes={
   new ReleaseNotesEntry("1.0.7",
    "【Bug 修复】\n• 修复打击光效偏位及轨道定位线、段选线断点。\n• 修复点立得时间戳、预览滚轮及绿色放置框、天空结束线显示异常。\n• 修复 Tab 切网格时输入框抢焦点、悬浮提示换行及时间戳滞留。\n• 修复重叠物件误删全部物件问题，以及 Hold/SkyArea 候选轮选失效。\n• 修复非正式预览清除段选及复制、剪切、粘贴预览；Space、Q 均保留编辑进度。\n• 修复按住 Alt 时无法切换节点编辑。\n• 修复更新公告已读标记混用，首次启动本次更新可正常弹出。\n\n【操作与配置】\n• 配置独立记忆；根目录预置 Background 文件夹。\n• 新增段选、网格及键宽热键：Ctrl+R、Tab、I/O。\n• 空格按住预览、松开返回；Q 切换预览、不返回；选择工具默认 V。\n• 点立得选取不再同时放置；平滑播放显示固定开启。\n• 补全英文界面、提示与状态消息。\n\n【新增功能与工具】\n• 支持拖动相接 SkyArea 的公共节点。\n• 完善 hidegroup 组谱面检查，提示短物件和加载风险，可跳转定位。\n• 新增按偏移剪歌工具及外置工具编写说明。\n• 新增鼠标拖尾特效（可开关）。\n\n【性能与视觉优化】\n• 优化复杂谱面节点拖动，减少卡顿。\n• 还原轨道按键文字的字体、颜色与布局，支持开关。\n• 调整 Tap 贴图、轨道参数和相机视距，提高网格对比度。",
    "[Bug fixes]\n• Fixed misplaced hit effects and gaps in track crosshairs and range-selection lines.\n• Fixed Quick Place timestamps, preview scrolling, green placement guides and sky end guides.\n• Fixed Tab grid toggling stealing field focus, tooltip wrapping and lingering timestamps.\n• Fixed overlap removal deleting all notes and Hold/SkyArea candidate cycling.\n• Informal Space/Q preview now preserves range selections and pending copy, cut and paste operations.\n• Fixed toggling node editing while Alt is held.\n• Fixed announcement acknowledgement tracking so this update appears on first launch.\n\n[Controls & preferences]\n• Persistent preferences and a pre-created Background folder.\n• Added range, grid and width shortcuts: Ctrl+R, Tab and I/O.\n• Hold Space to preview and return on release; Q toggles preview without returning. V selects.\n• Quick Place selection no longer places another note; smooth playback stays enabled.\n• Completed missing English labels, tooltips and status messages.\n\n[New features & tools]\n• Drag shared SkyArea endpoints to edit both connected segments.\n• Improved hidegroup chart checks with short-note/load-risk details and jump-to-note actions.\n• Added offset-aware audio trimming and an external-tool authoring guide.\n• Added a cursor trail effect (toggleable).\n\n[Performance & visuals]\n• Reduced node-drag stutter in complex charts.\n• Restored native key-label font, color and layout, with a visibility toggle.\n• Adjusted Tap textures, tracks and camera distance; increased grid contrast."),
   new ReleaseNotesEntry("1.06",
    "• 全新编辑界面与原生风格预览，支持自定义显示与快捷键。\n• 点立得放置、多选复制／镜像、中键拖动，端点可切换时间／位置模式。\n• 快速工具提供 Timing 操作、SkyArea 切分与 Flick 转换；切分跟随主界面网格。\n• 优化复杂谱面预览及时间组／时序面板，支持超界天空物件与辅助定位。\n• 复制粘贴保留组号和时间组；修复 Hold 选中变暗，保留手动保存与撤销重做。\n• 通用外置工具接口；默认附带紫条雨、渐变 Timing、按时序切分。",
    "• Refreshed editing UI and native-style preview, with configurable display and shortcuts.\n• Quick Place, multi-select copy/mirror and middle-button dragging, with separate time/position modes.\n• Quick tools for Timing operations, SkyArea splitting and Flick conversion; splitting follows the main grid.\n• Preview and timing-panel optimizations, overflow air-note placement and placement guides.\n• Pasting preserves note groups and timing groups; fixes selected Hold dimming and retains manual save and undo/redo.\n• Extensible external tools; bundles MakeRain, GradientTiming and TimingSplit.")
  };
 }
}
