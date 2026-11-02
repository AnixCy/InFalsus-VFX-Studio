using System;
using System.Linq;
using UnityEngine;
using InFalsusStudio.Core;
using InFalsusStudio.TimeGroups;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  GroupModel statementModel;EditHistory statementHistory;string statementGroup="",statementError="";string[] statementGroups=new string[0],statementNotices=new string[0];
  TimingStatement[] statementHeader=new TimingStatement[0],statementBpms=new TimingStatement[0],statementTracks=new TimingStatement[0],statementVisibility=new TimingStatement[0];
  bool statementGroupOpen,statementRawOpen,statementNoticesOpen,statementHelpOpen,statementConflicts;int statementGeneration,statementSelected=-1,statementFocus=-1;
  void CaptureTimingEditor(){
   GroupRuntime.Bind(boot);var history=boot.History;string name=TimingGroups.Current;
   bool changed=!ReferenceEquals(statementHistory,history)||statementGroup!=name;if(changed){statementGeneration++;statementGroupOpen=false;statementRawOpen=false;statementFocus=-1;}
   statementHistory=history;statementGroup=name;statementSelected=Val<int>(boot,"selectedId");
   if(history==null||!boot.HasDocument){statementModel=null;return;}
   var d=history.Document;bool rebuild=changed||!ReferenceEquals(timingDocument,d)||timingRevision!=history.Revision;double now=boot.CurrentMs;
   if(!rebuild&&timingCapturedAt==now)return;
   if(!rebuild){timingCapturedAt=now;if(statementError.Length==0)RefreshInheritedTiming(now);return;}
   var model=TimingGroups.Model(d);statementModel=model;timingDocument=d;timingRevision=history.Revision;timingCapturedAt=now;
   if(rebuild){statementGroups=new[]{""}.Concat(model.Groups.Select(g=>g.Name)).Distinct(StringComparer.Ordinal).ToArray();
   statementNotices=d.Diagnostics.Concat(TimingGroups.Events(d).Where(e=>e.Name=="icp_event").Take(8).Select(e=>TimingReferenceMerge.DescribeOpaque(d,e))).Distinct().ToArray();}
   statementError="";try{statementHeader=TimingStatements.Capture(d,name,EventKind.Chart,now,true);statementBpms=TimingStatements.Capture(d,name,EventKind.Bpm,now,true);statementTracks=TimingStatements.Capture(d,name,EventKind.Track,now,true);statementVisibility=TimingStatements.Capture(d,name,EventKind.Unknown,now,true);statementConflicts=statementBpms.Concat(statementTracks).Concat(statementVisibility).Where(r=>!r.Inherited).GroupBy(r=>new{r.Kind,r.Time}).Any(g=>g.Count()>1);IndexTimingRows();}
   catch(ArgumentException e){statementError=e.Message;}
  }
  void StatementGuard(EditHistory history,string name){if(!ReferenceEquals(boot.History,history)||TimingGroups.Current!=name)throw new InvalidOperationException("谱面或 timinggroup 已切换，请重新选择。");}
  void StatementEdit(EditHistory history,string name,Action<SpcDocument> edit){StatementGuard(history,name);GroupChange(history,edit);}
  void SelectStatement(EditHistory history,string name,int id){
   if(!(bool)Call(boot,"CommitAllInputFieldsNow"))return;StatementGuard(history,name);if(!history.Document.Contains(id))return;Call(boot,"Select",Val<int>(boot,"selectedId")==id?-1:id);
  }
  void DuplicateStatement(EditHistory history,string name,TimingStatement row){
   if(!(bool)Call(boot,"CommitAllInputFieldsNow"))return;StatementGuard(history,name);int added=-1;bool snap=Val<bool>(boot,"showGrid");int division=(int)Prop(boot,"Division");double now=boot.CurrentMs;
   StatementEdit(history,name,d=>added=TimingStatements.Duplicate(d,name,row.Id,row.Kind,snap,division,now));Call(boot,"Select",added);statementFocus=added;
  }
  void DeleteStatement(EditHistory history,string name,TimingStatement row){
   if(!(bool)Call(boot,"CommitAllInputFieldsNow"))return;int next=-1;StatementEdit(history,name,d=>next=TimingStatements.Delete(d,name,row.Id,row.Kind));Call(boot,"Select",next);statementFocus=-1;
  }
  TimingStatement SelectedStatement(){TimingStatement row;return timingById.TryGetValue(statementSelected,out row)?row:null;}
  float TimingEditorHeight(){
   if(statementHistory==null)return 260;
   float height=663+170+(statementGroupOpen?38*statementGroups.Length:0)+(statementHeader.Length>0?87:0)+timingRowsHeight+(statementConflicts?64:0)+(statementHelpOpen?110:0);
   if(statementError.Length>0)height+=140;if(statementRawOpen&&SelectedStatement()!=null)height+=120;
   if(statementNoticesOpen)foreach(string text in statementNotices)height+=Math.Max(36,UiFontRaster.Wrap(T(text),CNFace,FontPixels(13),405*scale).Length*(FontPixels(13)+5)/scale+12);
   return height;
  }
  void TimingGroupSelector(){
   var row=ContentRow(58);RowLine(row);UiText(new Rect(0,row.y,111*scale,row.height),"timinggroup",15,Css("#a6c3d1"));
   string text=UiFontRaster.Ellipsize(T(TimingGroupCaption(statementGroup)),CNFace,FontPixels(16),264*scale);
   if(FlatButton(new Rect(118*scale,row.y+10*scale,287*scale,38*scale),text+"  ▾",16,statementGroupOpen))Queue(delegate{statementGroupOpen=!statementGroupOpen;});
   if(!statementGroupOpen)return;var history=statementHistory;
   float start=contentY,height=38*scale;int first=Mathf.Clamp(Mathf.FloorToInt((timingViewTop-start)/height),0,statementGroups.Length),end=Mathf.Clamp(Mathf.CeilToInt((timingViewTop+timingViewHeight-start)/height),first,statementGroups.Length);
   contentY=start+first*height;timingStaticCellText=true;try{for(int i=first;i<end;i++){string target=statementGroups[i];var option=ContentRow(38);if(FlatButton(new Rect(8*scale,option.y+3*scale,397*scale,32*scale),TimingGroupCaption(target),15,target==statementGroup))Queue(delegate{ActivateTiming(target,history);statementGroupOpen=false;panelScroll=Vector2.zero;});}}finally{timingStaticCellText=false;}contentY=start+statementGroups.Length*height;
  }
  void TimingTable(string title,TimingStatement[] rows,bool chart=false){
   MatchedSection(title);var head=ContentRow(31);
   UiText(new Rect(5*scale,head.y,120*scale,head.height),"时间 / ms",13,Css("#789eae"));
   bool track=rows.Length>0&&(rows[0].Kind==EventKind.Track||rows[0].Kind==EventKind.Unknown);
   if(track)UiText(new Rect(170*scale,head.y,140*scale,head.height),rows[0].Kind==EventKind.Unknown?"0 显示 / 1 隐藏":"倍率",13,Css("#789eae"));
   else {UiText(new Rect(138*scale,head.y,90*scale,head.height),"BPM",13,Css("#789eae"));UiText(new Rect(238*scale,head.y,73*scale,head.height),"拍数",13,Css("#789eae"));}
   if(rows.Length>0)timingTableY[rows[0].Kind]=contentY;VisibleTimingRows(rows);
  }
  void TimingStatementRow(TimingStatement item){
   var view=TimingView(item);var row=ContentRow(item.Inherited?70:49);view.Y=row.y;view.Height=row.height;RowLine(row);bool selected=!item.Inherited&&statementSelected==item.Id;
   if(selected){Fill(new Rect(0,row.y+2*scale,405*scale,row.height-4*scale),Css("#173743"));Border(new Rect(0,row.y+2*scale,405*scale,row.height-4*scale),Css("#82cfd6"),scale);}
   if(item.Inherited){
    UiText(new Rect(5*scale,row.y+2*scale,305*scale,30*scale),view.InheritedTitle,14,Css("#789eae"));
    UiText(new Rect(5*scale,row.y+31*scale,305*scale,34*scale),view.InheritedText,16,Css("#a6c3d1"));
   }else{
    if(Event.current.type==EventType.MouseDown&&Event.current.button==0&&TimingPointerVisible()&&new Rect(0,row.y,311*scale,row.height).Contains(Event.current.mousePosition))Queue(view.Select);
    for(int column=0;column<item.Cells.Length;column++){
     int c=column;float x=(item.Kind==EventKind.Track||item.Kind==EventKind.Unknown)?(c==0?0:165):(c==0?0:c==1?133:233);float w=(item.Kind==EventKind.Track||item.Kind==EventKind.Unknown)?(c==0?157:146):(c==0?125:c==1?92:78);
     Rect field=new Rect((x+4)*scale,row.y+8*scale,(w-8)*scale,33*scale);string key=view.Keys[c];
     if(item.Kind==EventKind.Chart&&c==0)UiText(field,"0",18,Css("#789eae"),NumFace,0);
     else TimingCell(field,view,c);
    }
   }
   if(FlatButton(new Rect(321*scale,row.y+(row.height/scale-33)*.5f*scale,36*scale,33*scale),"+",22))Queue(view.Duplicate);
   bool enabled=GUI.enabled;GUI.enabled=enabled&&!item.Inherited&&item.Kind!=EventKind.Chart;
   try{if(FlatButton(new Rect(363*scale,row.y+(row.height/scale-33)*.5f*scale,36*scale,33*scale),"×",22))Queue(view.Delete);}finally{GUI.enabled=enabled;}
  }
  void MatchedTimingEditor(){
   if(statementHistory==null||!boot.HasDocument){UiParagraph(new Rect(0,contentY+15*scale,405*scale,110*scale),"请先打开歌曲或谱面。",17,Css("#a6c3d1"));return;}
   TimingGroupSelector();contentY+=10*scale;UiParagraph(new Rect(0,contentY,405*scale,64*scale),"非官方 timinggroup · 组内 BPM / track 独立编辑",14,Css("#c9ad83"));contentY+=64*scale;
   if(statementError.Length>0){UiParagraph(new Rect(0,contentY,405*scale,140*scale),statementError,15,Css("#ee9294"));return;}
   bool enabled=GUI.enabled;GUI.enabled=enabled&&(Transport==null||!Transport.Playing);
   try{
    if(statementHeader.Length>0)TimingTable("全谱初始 BPM / 拍号",statementHeader,true);
    TimingTable("BPM",statementBpms);TimingTable("track",statementTracks);TimingTable("hidegroup",statementVisibility);
    var help=ContentRow(80);UiParagraph(help,"hidegroup 只控制当前组物件的显示，判定光效与音效照常；0 显示，1 隐藏。",13,Css("#789eae"));
    contentY+=10*scale;UiParagraph(new Rect(0,contentY,405*scale,64*scale),"+ 复制本行，时间后移一格；× 删除本行。继承行只读，+ 创建本组事件。",13,Css("#789eae"));contentY+=64*scale;
    if(statementConflicts){UiParagraph(new Rect(0,contentY,405*scale,64*scale),"同刻存在多条事件；保留所有语句，按列表顺序以后者生效。",13,Css("#c9ad83"));contentY+=64*scale;}
    var history=statementHistory;string name=statementGroup;
    MatchedLink("从配对明文补入 BPM / 拍号…",delegate{Queue(delegate{if(!(bool)Call(boot,"CommitAllInputFieldsNow"))return;StatementGuard(history,name);Call(boot,"ImportPairedTiming");});});
    var selected=SelectedStatement();bool previous=GUI.enabled;GUI.enabled=previous&&selected!=null;
    try{MatchedLink(statementRawOpen?"收起原始语句":"原始语句 / 高级参数",delegate{Queue(delegate{statementRawOpen=!statementRawOpen;});});}finally{GUI.enabled=previous;}
    if(statementRawOpen&&selected!=null){TimingStatements.Materialize(selected);var row=ContentRow(120);UiText(new Rect(0,row.y,405*scale,30*scale),"原始语句 · Enter / 失焦应用",14,Css("#a6c3d1"));UiInput(new Rect(0,row.y+36*scale,405*scale,66*scale),"field_timing_raw_"+statementGeneration+"_"+selected.Id,selected.Editable,delegate(string value){StatementEdit(history,name,d=>TimingStatements.Source(d,name,selected.Id,selected.Kind,selected.Raw,value));},14,0,true,false,CNFace,false);}
   }finally{GUI.enabled=enabled;}
   MatchedLink("track 用法说明",delegate{Queue(delegate{statementHelpOpen=!statementHelpOpen;});});
   if(statementHelpOpen){UiParagraph(new Rect(0,contentY,405*scale,110*scale),"track 只改变本组物件滚速，不改变歌曲播放速度。0 为停流，负数为反向滚动。BPM 控制本组节拍网格；统一流速可辅助编辑停流和反向段。",14,Css("#789eae"));contentY+=110*scale;}
   MatchedLink("高级：未识别内容提示（"+statementNotices.Length+"）",delegate{Queue(delegate{statementNoticesOpen=!statementNoticesOpen;});});
   if(statementNoticesOpen)foreach(string text in statementNotices){float height=Math.Max(36,UiFontRaster.Wrap(T(text),CNFace,FontPixels(13),405*scale).Length*(FontPixels(13)+5)/scale+12);var row=ContentRow(height);UiParagraph(row,text,13,Css("#789eae"));}
   RestoreTimingViewportFocus();
  }
 }
}
