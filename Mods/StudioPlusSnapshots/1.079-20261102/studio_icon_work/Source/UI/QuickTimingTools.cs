using System;
using System.Collections;
using System.Linq;
using System.Text;
using UnityEngine;
using InFalsusStudio.Core;
using InFalsusStudio.TimeGroups;

namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  enum QuickTimingPhase { None, RangeDelete, RangeCopy, Target, Apply }
  QuickTimingPhase quickTimingPhase;
  EditHistory quickTimingHistory;
  int quickTimingRevision;
  ToolTimingData quickTimingData;
  bool quickAwaitingTarget;

  void MatchedQuickTools(){
   MatchedLink("删除 Timing",delegate{StartQuickTiming(false);});
   MatchedLink("复制 Timing",delegate{StartQuickTiming(true);});
   var help=ContentRow(145);UiParagraph(new Rect(0,help.y+14*scale,405*scale,125*scale),
    RainL("点击 Timing 工具后，在轨道上选择段落起止；复制会再选择目标时间。全部操作均可一次撤销。","Timing tools pick a source range on the track; copying also picks a destination. Each action is one undo step."),14,Css("#8caab8"));
   MatchedQuickSkyTools();quickToolsContentHeight=contentY/scale+16;
  }
  void QuickStatus(string message){if(boot!=null)Set(boot,"status",message);}
  void QuickError(Exception error){QuickStatus(error.Message);Logger.LogWarning("Quick timing: "+error);quickTimingPhase=QuickTimingPhase.None;quickTimingData=null;quickAwaitingTarget=false;}
  void StartQuickTiming(bool copy){
   Queue(delegate{try{
    if(boot==null||!boot.HasDocument||Transport.Playing)throw new InvalidOperationException(RainL("请先暂停并打开谱面。","Pause and open a chart first."));
    if(!(bool)Call(boot,"CommitAllInputFieldsNow"))return;
    Call(boot,"BeginRangeSelection");
    if(!Val<bool>(boot,"rangeActive"))throw new InvalidOperationException(RainL("无法开始段落选择。","Could not start range selection."));
    quickTimingHistory=boot.History;quickTimingRevision=boot.History.Revision;
    quickTimingPhase=copy?QuickTimingPhase.RangeCopy:QuickTimingPhase.RangeDelete;
    quickTimingData=null;quickAwaitingTarget=false;panel="";
    QuickStatus(copy?RainL("复制 Timing：依次点击来源段落的起点和终点。","Copy Timing: pick source start and end.") :
     RainL("删除 Timing：依次点击段落起点和终点。","Delete Timing: pick range start and end."));
   }catch(Exception e){QuickError(e);}});
  }
  void QuickRangeCompleted(){
   if(quickTimingPhase!=QuickTimingPhase.RangeCopy&&quickTimingPhase!=QuickTimingPhase.RangeDelete)return;
   try{
    if(!ReferenceEquals(boot.History,quickTimingHistory)||boot.History.Revision!=quickTimingRevision)throw new InvalidOperationException(RainL("谱面已变化，请重新选择。","Chart changed; select again."));
    quickTimingData=CaptureQuickTiming(quickTimingPhase==QuickTimingPhase.RangeCopy?"copy":"delete");
    if(quickTimingPhase==QuickTimingPhase.RangeDelete){quickTimingPhase=QuickTimingPhase.Apply;Queue(ApplyQuickTiming);return;}
    quickTimingPhase=QuickTimingPhase.Target;quickAwaitingTarget=true;
    Queue(delegate{try{
     if(!ReferenceEquals(boot.History,quickTimingHistory)||boot.History.Revision!=quickTimingRevision)throw new InvalidOperationException(RainL("谱面已变化，请重新选择。","Chart changed; select again."));
     Call(boot,"BeginRangeSelection");
     if(!Val<bool>(boot,"rangeActive"))throw new InvalidOperationException(RainL("无法选择目标时间。","Could not select target time."));
     quickAwaitingTarget=false;
     QuickStatus(RainL("复制 Timing：点击轨道上的目标起点；右键取消。","Copy Timing: pick the target start; right-click cancels."));
    }catch(Exception e){QuickError(e);}});
   }catch(Exception e){QuickError(e);}
  }
  void QuickTargetChosen(){
   if(quickTimingPhase!=QuickTimingPhase.Target||quickAwaitingTarget||!Val<bool>(boot,"rangeActive")||!Val<bool>(boot,"rangeHasStart"))return;
   try{
    double target=Val<double>(boot,"rangeStart");
    if(quickTimingData==null||target<0||target+quickTimingData.end-quickTimingData.start>86400000)
     throw new ArgumentException(RainL("目标范围须落在 0–86400000 ms 内。","Target range must fit within 0–86400000 ms."));
    quickTimingData.target=target;
    Call(boot,"CancelWorkflow",false);
    quickTimingPhase=QuickTimingPhase.Apply;Queue(ApplyQuickTiming);
   }catch(Exception e){QuickError(e);}
  }
  void ApplyQuickTiming(){
   try{
    if(quickTimingPhase!=QuickTimingPhase.Apply||quickTimingData==null)return;
    if(!ReferenceEquals(boot.History,quickTimingHistory)||boot.History.Revision!=quickTimingRevision||TimingGroups.Current!=quickTimingData.scope||Transport.Playing)
     throw new InvalidOperationException(RainL("谱面、时间组或播放状态已变化，请重新执行。","Chart, timinggroup or playback changed; run the tool again."));
    if(!(bool)Call(boot,"CommitAllInputFieldsNow"))return;
    if(boot.History.Revision!=quickTimingRevision)throw new InvalidOperationException(RainL("参数改动已应用，请重新选择。","A field edit was applied; select again."));
    var request=new ToolRequest{timingEdit=quickTimingData};
    string[] lines=quickTimingData.action=="copy"?quickTimingData.rows.Select(row=>CopyTimingLine(row,quickTimingData)).ToArray():new string[0];
    ValidateTimingEdit(lines,request);
    var history=quickTimingHistory;var data=quickTimingData;
    bool previous=TimingGroups.ExplicitEdit;TimingGroups.ExplicitEdit=true;
    try{GroupChange(history,document=>InsertTimingEdit(document,lines,request));}
    finally{TimingGroups.ExplicitEdit=previous;}
    QuickStatus(data.action=="copy"?RainL("已复制 ","Copied ")+data.rows.Length+RainL(" 条 Timing；一次撤销。"," timing events; one undo step.") :
     RainL("已删除 ","Deleted ")+data.rows.Length+RainL(" 条 Timing；一次撤销。"," timing events; one undo step."));
    quickTimingPhase=QuickTimingPhase.None;quickTimingData=null;
   }catch(Exception e){QuickError(e);}
  }
  void TickQuickTiming(){
   if(quickTimingPhase==QuickTimingPhase.None||boot==null)return;
   if(!ReferenceEquals(boot.History,quickTimingHistory)||Transport==null||Transport.Playing){quickTimingPhase=QuickTimingPhase.None;quickTimingData=null;return;}
   if((quickTimingPhase==QuickTimingPhase.RangeCopy||quickTimingPhase==QuickTimingPhase.RangeDelete||quickTimingPhase==QuickTimingPhase.Target&&!quickAwaitingTarget)&&!Val<bool>(boot,"rangeActive")){
    quickTimingPhase=QuickTimingPhase.None;quickTimingData=null;
    QuickStatus(RainL("快速 Timing 操作已取消。","Quick timing action cancelled."));
   }
  }
  internal IEnumerator ProbeQuickWorkflow(SpcDocument source,StringBuilder output){
   if(boot==null)throw new InvalidOperationException("Quick workflow has no editor.");
   Call(boot,"LoadDocument",source);
   var history=boot.History;string original=history.Document.ToText();
   var model=TimingGroups.Model(history.Document);
   var sample=TimingGroups.Events(history.Document).First(e=>e.Kind==EventKind.Bpm||e.Kind==EventKind.Track);
   GroupRuntime.Activate(boot,model.Of(sample.SourceId));
   double time=sample.TimeMs;
   StartQuickTiming(false);
   for(int i=0;i<12&&quickTimingPhase!=QuickTimingPhase.RangeDelete;i++)yield return null;
   if(quickTimingPhase!=QuickTimingPhase.RangeDelete)throw new InvalidOperationException("Delete tool did not start range selection.");
   Set(boot,"rangeStart",time);Set(boot,"hoverTime",time);Set(boot,"rangeActive",false);
   RememberTimingRange();QuickRangeCompleted();
   for(int i=0;i<12&&history.Revision==0;i++)yield return null;
   if(history.Revision==0||history.Document.Contains(sample.SourceId))throw new InvalidOperationException("Quick Delete did not apply.");
   if(!history.Undo()||history.Document.ToText()!=original)throw new InvalidOperationException("Quick Delete undo failed.");
   Call(boot,"AfterDocumentEdit");
   StartQuickTiming(true);
   for(int i=0;i<12&&quickTimingPhase!=QuickTimingPhase.RangeCopy;i++)yield return null;
   if(quickTimingPhase!=QuickTimingPhase.RangeCopy)throw new InvalidOperationException("Copy tool did not start source selection.");
   Set(boot,"rangeStart",time);Set(boot,"hoverTime",time);Set(boot,"rangeActive",false);
   RememberTimingRange();QuickRangeCompleted();
   for(int i=0;i<12&&quickAwaitingTarget;i++)yield return null;
   if(quickTimingPhase!=QuickTimingPhase.Target||!Val<bool>(boot,"rangeActive"))throw new InvalidOperationException("Copy tool did not request target time.");
   Set(boot,"rangeStart",600000d);Set(boot,"rangeHasStart",true);QuickTargetChosen();
   int before=history.Revision;
   for(int i=0;i<12&&history.Revision==before;i++)yield return null;
   if(history.Revision==before||history.Document.ToText()==original)throw new InvalidOperationException("Quick Copy did not apply.");
   if(!history.Undo()||history.Document.ToText()!=original)throw new InvalidOperationException("Quick Copy undo failed.");
   output.Append("quick_timing_click_flow=PASS\n");
  }
 }
}
