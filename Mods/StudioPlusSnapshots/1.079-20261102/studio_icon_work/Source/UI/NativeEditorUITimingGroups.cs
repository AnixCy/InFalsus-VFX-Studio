using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using InFalsusStudio.Core;
using InFalsusStudio.TimeGroups;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  sealed class TimingRow {internal string Name,Caption,NotesText,TimingText;internal int Notes,Timing;internal Action Select;}
  TimingRow[] timingRows=new TimingRow[0];EditHistory timingHistory;
  SpcDocument timingGroupsDocument;int timingGroupsRevision=-1;GroupModel timingGroupsModel;
  string frameTimingName="",timingRename="",timingErrors="";bool timingCanDelete,timingNoInput,timingNoClip;
  void CaptureTimingGroups(){
   GroupRuntime.Bind(boot);bool changed=!ReferenceEquals(timingHistory,boot.History);timingHistory=boot.History;
   if(timingHistory==null){timingRows=new TimingRow[0];timingGroupsDocument=null;timingGroupsModel=null;timingGroupsRevision=-1;timingCanDelete=false;timingErrors="";return;}
   string current=TimingGroups.Current;bool rebuild=changed||!ReferenceEquals(timingGroupsDocument,timingHistory.Document)||timingGroupsRevision!=timingHistory.Revision;
   if(!rebuild&&frameTimingName==current)return;
   if(changed||frameTimingName!=current)timingRename=current;frameTimingName=current;
   if(rebuild){
    timingGroupsDocument=timingHistory.Document;timingGroupsRevision=timingHistory.Revision;timingGroupsModel=TimingGroups.Model(timingGroupsDocument);var byName=new Dictionary<string,TimingRow>(StringComparer.Ordinal);var history=timingHistory;
    timingRows=new[]{""}.Concat(timingGroupsModel.Groups.Select(g=>g.Name)).Distinct(StringComparer.Ordinal).Select(name=>{var row=new TimingRow{Name=name,Caption=TimingGroupCaption(name),Select=()=>ActivateTiming(name,history)};byName.Add(name,row);return row;}).ToArray();
    foreach(var e in TimingGroups.Events(timingGroupsDocument)){TimingRow row;if(!byName.TryGetValue(timingGroupsModel.Of(e.SourceId),out row))continue;if(e.IsNote)row.Notes++;if(e.Kind==EventKind.Bpm||e.Kind==EventKind.Track||timingGroupsModel.Visibility.ContainsKey(e.SourceId))row.Timing++;}
    foreach(var row in timingRows){row.NotesText=row.Notes+" 个物件";row.TimingText=row.Timing+" 个时序事件";}
    timingErrors=string.Join("\n",timingGroupsModel.Errors.ToArray());
   }
   var model=timingGroupsModel;
   var block=model.Find(current);timingNoInput=block!=null&&!block.NoClip&&block.Properties.Contains("noinput");timingNoClip=block!=null&&block.NoClip;
   timingCanDelete=current!=""&&block!=null;
  }
  void GroupChange(EditHistory expected,Action<SpcDocument> edit){
   if(boot.History!=expected)throw new InvalidOperationException("谱面已切换，请重新操作。");
   if(Transport!=null&&Transport.Playing)throw new InvalidOperationException("请暂停播放后编辑。");
   Call(boot,"CancelGesture");if(!(bool)Call(boot,"Change",edit))throw new ArgumentException(Val<string>(boot,"status"));GroupRuntime.Sync(boot);
  }
  void ActivateTiming(string name,EditHistory expected){
   if(boot.History!=expected)throw new InvalidOperationException("谱面已切换。");if(!(bool)Call(boot,"CommitAllInputFieldsNow"))return;
   Call(boot,"CancelGesture");GroupRuntime.Activate(boot,name);GUI.FocusControl(null);
  }
  void StatementGuardGroup(EditHistory history,string name){if(!ReferenceEquals(boot.History,history)||TimingGroups.Current!=name)throw new InvalidOperationException("谱面或 timinggroup 已切换。");}
  void TimingNameField(string title,string key,string value,Action<string> commit){
   var row=ContentRow(65);timingRenameBounds=row;RowLine(row);UiText(new Rect(0,row.y,100*scale,row.height),title,16,Css("#a6c3d1"));UiInput(new Rect(108*scale,row.y+13*scale,297*scale,39*scale),key,value,commit,18,0,true,false,CNFace,false);RestoreTimingCaret(key);
  }
  void TimingGroupRow(TimingRow entry){
   var row=ContentRow(69);RowLine(row);bool current=entry.Name==frameTimingName;if(current)Fill(new Rect(0,row.y,405*scale,row.height),Css("#173743"));
   timingStaticCellText=true;try{
    if(FlatButton(new Rect(0,row.y+5*scale,240*scale,59*scale),entry.Caption,18,current,false))Queue(entry.Select);
    UiText(new Rect(245*scale,row.y+4*scale,160*scale,30*scale),entry.NotesText,14,Css("#b3d0da"),CNFace,2);UiText(new Rect(245*scale,row.y+31*scale,160*scale,30*scale),entry.TimingText,13,Css("#789eae"),CNFace,2);
   }finally{timingStaticCellText=false;}
  }
  void VisibleTimingGroupRows(){
   float start=contentY,height=69*scale;int first=Mathf.Clamp(Mathf.FloorToInt((timingViewTop-start)/height),0,timingRows.Length),end=Mathf.Clamp(Mathf.CeilToInt((timingViewTop+timingViewHeight-start)/height),first,timingRows.Length);
   contentY=start+first*height;for(int i=first;i<end;i++)TimingGroupRow(timingRows[i]);contentY=start+timingRows.Length*height;
  }
  void MatchedTimingGroups(){
   UiParagraph(new Rect(0,contentY+10*scale,405*scale,116*scale),"非官方扩展：官方本体不支持时间组。保存与另存为保留此语法；请使用支持该语法的非官方壳子。",15,Css("#edc48c"));contentY+=126*scale;
   if(timingHistory==null||!boot.HasDocument){UiParagraph(new Rect(0,contentY,405*scale,100*scale),"请先打开歌曲或谱面。",17,Css("#a6c3d1"));return;}
   var history=timingHistory;string active=frameTimingName;bool previous=GUI.enabled;
   VisibleTimingGroupRows();
   if(timingErrors.Length>0){UiParagraph(new Rect(0,contentY+10*scale,405*scale,110*scale),timingErrors,15,Css("#ee9294"));contentY+=120*scale;}
   GUI.enabled=previous&&(Transport==null||!Transport.Playing);try{
    var add=ContentRow(58);if(FlatButton(new Rect(0,add.y+8*scale,405*scale,42*scale),"+ 新建 timinggroup",16))Queue(delegate{if(!(bool)Call(boot,"CommitAllInputFieldsNow"))return;string name=TimingGroups.NextName(boot.History.Document);GroupChange(history,d=>TimingGroups.Add(d,name));ActivateTiming(name,history);timingRename=name;});
    MatchedSection("当前 timinggroup");
    if(active!=""){
     TimingNameField("当前组名称","field_timinggroup_rename_"+active,timingRename,delegate(string value){timingRename=value.Trim();});var row=ContentRow(58);
     if(FlatButton(new Rect(0,row.y+8*scale,197*scale,42*scale),"重命名当前组",16))Queue(delegate{if(!(bool)Call(boot,"CommitAllInputFieldsNow"))return;string next=timingRename;GroupChange(history,d=>TimingGroups.Rename(d,active,next));GroupRuntime.Activate(boot,next);});
     bool enabled=GUI.enabled;GUI.enabled=enabled&&timingCanDelete;try{if(FlatButton(new Rect(207*scale,row.y+8*scale,198*scale,42*scale),"删除组及内容",16))Queue(delegate{if(!(bool)Call(boot,"CommitAllInputFieldsNow"))return;StatementGuardGroup(history,active);GroupChange(history,d=>TimingGroups.DeleteGroup(d,active));GroupRuntime.Activate(boot,"");GUI.FocusControl(null);});}finally{GUI.enabled=enabled;}
     MatchedToggle("noinput · 不判定",timingNoInput,delegate(bool value){Queue(delegate{if(!(bool)Call(boot,"CommitAllInputFieldsNow"))return;StatementGuardGroup(history,active);GroupChange(history,d=>GroupProperties.SetProperty(d,active,"noinput",value));});});
     MatchedToggle("noclip · 不判定且不截断",timingNoClip,delegate(bool value){Queue(delegate{if(!(bool)Call(boot,"CommitAllInputFieldsNow"))return;StatementGuardGroup(history,active);GroupChange(history,d=>GroupProperties.SetProperty(d,active,"noclip",value));});});
     var help=ContentRow(96);UiParagraph(help,"两项互斥，选择一项会关闭另一项。noinput 不判定并截断；noclip 不判定且不截断。",14,Css("#789eae"));
     var deleteHelp=ContentRow(96);UiParagraph(deleteHelp,"删除组会一并删除组内全部物件和时序事件。Ctrl+Z 可一次撤销。",14,Css("#789eae"));
    }else{var row=ContentRow(65);RowLine(row);UiText(row,"默认group",17,Css("#789eae"));}
   }finally{GUI.enabled=previous;}
   MatchedLink("编辑当前组时序",delegate{Queue(delegate{ActivateTiming(active,history);panel="时序";Set(boot,"showInspector",true);panelScroll=Vector2.zero;});});
   contentY+=12*scale;UiParagraph(new Rect(0,contentY,405*scale,104*scale),"物件归属在参数弹窗底部的 timinggroup 中修改。BPM / track / hidegroup 请进入时序列表，使用行内 + / ×。",14,Css("#789eae"));contentY+=114*scale;
   RestoreTimingViewportFocus();
  }
 }
}
