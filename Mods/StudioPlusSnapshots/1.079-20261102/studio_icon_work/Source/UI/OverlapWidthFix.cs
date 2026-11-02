using System;
using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using InFalsusStudio.Core;
using InFalsusStudio.TimeGroups;
using UnityEngine;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  void InitOverlapAndWidth(){harmony.Patch(Method("JumpToChartIssue"),prefix:new HarmonyMethod(typeof(OverlapWidthHooks).GetMethod("Jump",F)));}
  static bool Conflict(SpcEvent a,SpcEvent b,ChartIssueKind kind){
   if(kind==ChartIssueKind.FlickDuplicate)return true;
   if(Math.Max(a.Lane,b.Lane)>=Math.Min(a.Lane+a.GroundWidth,b.Lane+b.GroundWidth))return false;
   if(kind==ChartIssueKind.TapOverlap)return Math.Abs(a.TimeMs-b.TimeMs)<3;
   return Math.Min(a.EndMs,b.EndMs)>Math.Max(a.TimeMs,b.TimeMs);
  }
  internal static int[] OverlapExtras(SpcDocument document,ChartIssue issue){
   var model=TimingGroups.Model(document);var ids=new HashSet<int>(issue.SourceIds);var notes=document.ReadEvents().Where(e=>ids.Contains(e.SourceId)).OrderBy(e=>e.TimeMs).ThenBy(e=>document.SourceIndex(e.SourceId)).ToArray();
   var kept=new List<SpcEvent>();var extra=new List<int>();foreach(var note in notes){string group=model.Of(note.SourceId);if(kept.Any(k=>model.Of(k.SourceId)==group&&Conflict(k,note,issue.Kind)))extra.Add(note.SourceId);else kept.Add(note);}
   return extra.ToArray();
  }
  void ChangeGroundWidth(int step){
   if(boot==null||!boot.HasDocument||Transport.Playing||formal||boot.History.InTransaction||(bool)Prop(boot,"WorkflowActive"))return;
   if(!(bool)Call(boot,"CommitAllInputFieldsNow")||!(bool)Call(boot,"CommitPendingNoteNow"))return;
   string tool=Get(boot,"tool").ToString();var draft=Val<PointPlacement>(boot,"pointPlacement");
   if(tool=="Tap"||tool=="Hold"){
    string field=tool=="Tap"?"tapWidth":"holdWidth";int width=Math.Max(1,Math.Min(4,Val<int>(boot,field)+step));Set(boot,field,width);
    if(draft.Pending&&draft.Kind==EventKind.Hold&&draft.Lane>=1&&draft.Lane<=4){int allowed=Math.Min(width,5-draft.Lane);typeof(PointPlacement).GetProperty("Width",F).SetValue(draft,allowed,null);}
    Set(boot,"status",tool+" 默认宽度："+width+" 轨。");return;
   }
   if(draft.Pending)return;var selected=Val<HashSet<int>>(boot,"selection");var notes=boot.Events.Where(e=>selected.Contains(e.SourceId)&&(e.Kind==EventKind.Tap||e.Kind==EventKind.Hold)&&e.Lane>=1&&e.Lane<=4&&ChartMath.InvalidReason(e)==null).ToArray();
   if(notes.Length==0){Set(boot,"status","请选择 1–4 轨 Tap / Hold，或切换到对应放置工具。");return;}
   var changes=notes.Where(n=>n.GroundWidth+step>=1&&n.GroundWidth+step<=5-n.Lane).ToArray();if(changes.Length==0){Set(boot,"status","键宽已到可用边界。");return;}
   var history=boot.History;var current=changes.ToDictionary(n=>n.SourceId,n=>history.Document.SourceLine(n.SourceId));
   bool ok=(bool)Call(boot,"Change",(Action<SpcDocument>)(d=>{foreach(var n in changes){if(!d.Contains(n.SourceId)||d.SourceLine(n.SourceId)!=current[n.SourceId])throw new InvalidOperationException("选择已变化。");AuthoredNumber.Set(d,n.SourceId,n.Kind==EventKind.Tap?1:2,n.GroundWidth+step);}}));
   if(ok)Set(boot,"status","键宽已调整："+changes.Length+" 个物件；Ctrl+Z 撤销。");
  }
  static class OverlapWidthHooks {
   static bool Jump(StudioBootstrap __instance,ChartIssue __0){
    var ui=I;if(ui==null||!ui.ready||__0==null)return true;if(__0.Kind!=ChartIssueKind.TapOverlap&&__0.Kind!=ChartIssueKind.HoldOverlap&&__0.Kind!=ChartIssueKind.FlickDuplicate)return true;
    if(Get(__instance,"checkHistory")!=__instance.History||(int)Get(__instance,"checkRevision")!=__instance.History.Revision){Set(__instance,"status","谱面已修改，请重新检查。");return false;}
    var ids=OverlapExtras(__instance.History.Document,__0);if(ids.Length==0){Set(__instance,"status","当前检查项没有同一时间组内需要移除的重叠物件。");return false;}
    Call(__instance,"CancelGesture");((StudioTransport)Get(__instance,"transport")).Pause();Call(__instance,"SetSelection",ids,ids[0]);
    double time=__instance.Events.Where(e=>e.SourceId==ids[0]).Select(e=>e.TimeMs).First();((StudioTransport)Get(__instance,"transport")).Seek(time);Set(__instance,"exactGroupJump",time);Set(__instance,"timeText",time.ToString("0.##########",System.Globalization.CultureInfo.InvariantCulture));Set(__instance,"showNotes",true);Set(__instance,"chartCheckOpen",false);Set(__instance,"status","已选择 "+ids.Length+" 个重叠物件，保留不冲突的原物件；按 Delete 删除。");return false;
   }
  }
 }
}
