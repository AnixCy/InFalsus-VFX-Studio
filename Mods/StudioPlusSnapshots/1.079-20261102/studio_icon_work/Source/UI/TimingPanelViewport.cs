using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using InFalsusStudio.Core;
using InFalsusStudio.TimeGroups;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  sealed class TimingRowView {
   internal TimingStatement Item;internal string[] Keys,Display;internal Action<string>[] Commit;
   internal Action Select,Duplicate,Delete;internal string InheritedTitle,InheritedText;internal float Y,Height;
  }
  readonly Dictionary<int,TimingRowView> timingRowViews=new Dictionary<int,TimingRowView>();
  readonly Dictionary<int,TimingStatement> timingById=new Dictionary<int,TimingStatement>();
  sealed class TimingCellSlot {internal TimingStatement Item;internal int Column,Row;internal string Key;}
  readonly List<TimingCellSlot> timingCells=new List<TimingCellSlot>();readonly Dictionary<string,int> timingCellIndex=new Dictionary<string,int>();
  readonly Dictionary<EventKind,float> timingTableY=new Dictionary<EventKind,float>();string timingEditKey;
  float timingViewTop,timingViewHeight=1000,timingRowsHeight,timingLastScale;bool timingViewReady;
  string timingRestoreFocus;int timingRestoreCursor,timingRestoreSelection;double timingCapturedAt;int timingRevision=-1;
  string timingCaretKey;int timingCaretCursor,timingCaretSelection;
  SpcDocument timingDocument;
  int timingBpmPoint,timingTrackPoint;SpcEvent[] timingInheritedTracks=new SpcEvent[0];
  bool timingPaintActive;
  Rect timingRenameBounds;
  bool TimingRectVisible(Rect rect){return rect.yMax>=timingViewTop&&rect.y<=timingViewTop+timingViewHeight;}
  void DrawTimingEditorViewport(){timingPaintActive=true;try{MatchedTimingEditor();}finally{timingPaintActive=false;}}
  void DrawTimingGroupsViewport(){timingPaintActive=true;try{MatchedTimingGroups();}finally{timingPaintActive=false;}}
  TimingRowView TimingView(TimingStatement item){
   TimingRowView view;if(timingRowViews.TryGetValue(item.Id,out view)&&ReferenceEquals(view.Item,item))return view;
   TimingStatements.Materialize(item);
   var history=statementHistory;string name=statementGroup;
   view=new TimingRowView{Item=item,Keys=new string[item.Cells.Length],Display=new string[item.Cells.Length],Commit=new Action<string>[item.Cells.Length]};
   for(int column=0;column<item.Cells.Length;column++){
    int c=column;view.Keys[c]="field_timing_list_"+statementGeneration+"_"+item.Id+"_"+c;
    view.Display[c]=c==1&&item.Kind!=EventKind.Unknown?DisplayPrecision.Number(item.Cells[c],4):item.Cells[c];
    view.Commit[c]=value=>StatementEdit(history,name,d=>TimingStatements.SetCell(d,name,item.Id,item.Kind,c,value));
   }
   view.Select=()=>SelectStatement(history,name,item.Id);view.Duplicate=()=>DuplicateStatement(history,name,item);view.Delete=()=>DeleteStatement(history,name,item);
   if(item.Inherited){view.InheritedTitle=item.Kind==EventKind.Unknown?"尚无 hidegroup 事件":"继承默认group";view.InheritedText=item.Kind==EventKind.Unknown?"默认显示 · + 添加事件":view.Display[1]+(item.Kind==EventKind.Bpm?" BPM · "+item.Cells[2]+" 拍":" ×");}
   timingRowViews[item.Id]=view;return view;
  }
  static float TimingRowsHeight(TimingStatement[] rows){return rows.Length==0?0:rows.Length*(rows[0].Inherited?70:49);}
  void IndexTimingRows(){
   timingById.Clear();timingRowViews.Clear();timingRowsHeight=0;timingCells.Clear();timingCellIndex.Clear();
   foreach(var table in new[]{statementHeader,statementBpms,statementTracks,statementVisibility}){timingRowsHeight+=TimingRowsHeight(table);for(int i=0;i<table.Length;i++){var row=table[i];if(row.Inherited)continue;timingById[row.Id]=row;
    int columns=row.Kind==EventKind.Track||row.Kind==EventKind.Unknown?2:3;for(int c=row.Kind==EventKind.Chart?1:0;c<columns;c++){var key="field_timing_list_"+statementGeneration+"_"+row.Id+"_"+c;timingCellIndex[key]=timingCells.Count;timingCells.Add(new TimingCellSlot{Item=row,Column=c,Row=i,Key=key});}
   }}
   if(timingEditKey!=null&&!timingCellIndex.ContainsKey(timingEditKey)){timingEditKey=timingRestoreFocus=null;GUI.FocusControl(null);}
   timingBpmPoint=TimingBpmPoint(timingCapturedAt);
   timingInheritedTracks=statementTracks.Length>0&&statementTracks[0].Inherited?TimingStatements.Tracks(statementModel,statementGroup):new SpcEvent[0];timingTrackPoint=TimingTrackPoint(timingCapturedAt);
   if(statementFocus>=0){var cell=timingCells.FirstOrDefault(c=>c.Item.Id==statementFocus);statementFocus=-1;if(cell!=null)Queue(()=>ActivateTimingCell(cell));}
  }
  void RefreshInheritedTiming(double now){
   if(statementBpms.Length>0&&statementBpms[0].Inherited){int point=TimingBpmPoint(now);if(point!=timingBpmPoint){statementBpms=TimingStatements.Capture(timingDocument,statementGroup,EventKind.Bpm,now);timingBpmPoint=point;}}
   if(statementTracks.Length>0&&statementTracks[0].Inherited){int point=TimingTrackPoint(now);if(point!=timingTrackPoint){statementTracks=TimingStatements.Capture(timingDocument,statementGroup,EventKind.Track,now);timingTrackPoint=point;}}
  }
  int TimingBpmPoint(double now){var points=statementModel.Beat(statementGroup).Points;int lo=0,hi=points.Count;now=Math.Max(0,now);while(lo<hi){int mid=(lo+hi)/2;if(points[mid].Time<=now)lo=mid+1;else hi=mid;}return lo-1;}
  int TimingTrackPoint(double now){int lo=0,hi=timingInheritedTracks.Length;now=Math.Max(0,now);while(lo<hi){int mid=(lo+hi)/2;if(timingInheritedTracks[mid].TimeMs<=now)lo=mid+1;else hi=mid;}return lo-1;}
  void PrepareTimingViewport(Rect area){
   if(Event.current.type!=EventType.Layout)return;
   if(timingEditKey!=null&&timingRestoreFocus==null&&GUI.GetNameOfFocusedControl()!=timingEditKey)timingEditKey=null;
   bool changed=timingViewReady&&(timingViewTop!=panelScroll.y||timingViewHeight!=area.height||timingLastScale!=scale);
   if(changed){
    string focus=GUI.GetNameOfFocusedControl();TimingRowView owner=null;
    int focusedCell;if(focus.StartsWith("field_timing_list_",StringComparison.Ordinal)&&timingCellIndex.TryGetValue(focus,out focusedCell))timingRowViews.TryGetValue(timingCells[focusedCell].Item.Id,out owner);
    bool rename=panel=="时间组"&&focus.StartsWith("field_timinggroup_rename_",StringComparison.Ordinal);
    if(owner!=null||rename){
     var editor=(TextEditor)GUIUtility.GetStateObject(typeof(TextEditor),GUIUtility.keyboardControl);
     float y=rename?timingRenameBounds.y:owner.Y,height=rename?timingRenameBounds.height:owner.Height;bool visible=y+height>panelScroll.y&&y<panelScroll.y+area.height;
     if(visible){timingRestoreFocus=focus;timingRestoreCursor=editor.cursorIndex;timingRestoreSelection=editor.selectIndex;}
     else {Call(boot,"QueueFieldCommit",focus);timingRestoreFocus=timingEditKey=null;}
     GUI.FocusControl(null);Set(boot,"activeField","");numericPointer.End();numericControl=0;numericSelectOnMouseUp=0;
    }
   }
   timingViewTop=panelScroll.y;timingViewHeight=area.height;timingLastScale=scale;timingViewReady=true;
  }
  void RestoreTimingViewportFocus(){
   if(Event.current.type!=EventType.Repaint||timingRestoreFocus==null)return;
   string key=timingRestoreFocus;timingRestoreFocus=null;GUI.FocusControl(key);
   if(GUI.GetNameOfFocusedControl()!=key){Call(boot,"QueueFieldCommit",key);return;}
   var editor=(TextEditor)GUIUtility.GetStateObject(typeof(TextEditor),GUIUtility.keyboardControl);
   int length=(editor.text??"").Length;if(timingRestoreCursor<0)editor.SelectAll();else{editor.cursorIndex=Mathf.Clamp(timingRestoreCursor,0,length);editor.selectIndex=Mathf.Clamp(timingRestoreSelection,0,length);}Set(boot,"activeField",key);numericControl=GUIUtility.keyboardControl;numericKey=key;numericControls.Add(numericControl);
   if(timingRestoreCursor>=0){timingCaretKey=key;timingCaretCursor=timingRestoreCursor;timingCaretSelection=timingRestoreSelection;}
  }
  void VisibleTimingRows(TimingStatement[] rows){
   if(rows.Length==0)return;float start=contentY,height=(rows[0].Inherited?70:49)*scale;
   int first=Mathf.Clamp(Mathf.FloorToInt((timingViewTop-start)/height),0,rows.Length);
   int end=Mathf.Clamp(Mathf.CeilToInt((timingViewTop+timingViewHeight-start)/height),first,rows.Length);
   contentY=start+first*height;for(int i=first;i<end;i++)TimingStatementRow(rows[i]);contentY=start+rows.Length*height;
  }
  bool TimingPointerVisible(){return new Rect(0,panelScroll.y,405*scale,timingViewHeight).Contains(Event.current.mousePosition);}
  void ActivateTimingCell(TimingCellSlot cell){
   if(!(bool)Call(boot,"CommitAllInputFieldsNow"))return;StatementGuard(statementHistory,statementGroup);
   // A time edit may reorder the table before Tab focuses the next column.
   // Resolve the captured SourceId/column again against the committed snapshot.
   if(!ReferenceEquals(timingDocument,statementHistory.Document)||timingRevision!=statementHistory.Revision)CaptureTimingEditor();
   int current;if(!timingCellIndex.TryGetValue(cell.Key,out current))return;cell=timingCells[current];
   if(Val<int>(boot,"selectedId")!=cell.Item.Id)Call(boot,"Select",cell.Item.Id);
   timingEditKey=timingRestoreFocus=cell.Key;timingCaretKey=null;timingRestoreCursor=timingRestoreSelection=-1;GUI.FocusControl(null);Set(boot,"activeField","");
   float start;if(timingTableY.TryGetValue(cell.Item.Kind,out start)){float y=start+cell.Row*49*scale;if(y<panelScroll.y)panelScroll.y=Mathf.Max(0,y-16*scale);else if(y+49*scale>panelScroll.y+timingViewHeight)panelScroll.y=Mathf.Max(0,y+65*scale-timingViewHeight);}
  }
  void TimingCell(Rect rect,TimingRowView view,int column){
   string key=view.Keys[column];
   if(timingEditKey==key){UiInput(rect,key,view.Display[column],view.Commit[column],18,0,true,false,NumFace,true);
    // Native TextEditor selects all on the first event after FocusControl.
    // Restore a scrolled editor's caret after that focus transition has run.
    RestoreTimingCaret(key);
    return;
   }
   if(Event.current.type==EventType.Repaint){Fill(rect,Css("#091923"));Fill(new Rect(rect.x,rect.yMax-scale,rect.width,scale),Css("#4b7481"));timingStaticCellText=true;try{UiText(new Rect(rect.x+5*scale,rect.y,rect.width-10*scale,rect.height),view.Display[column],18,Css("#d3edf0"),NumFace,0);}finally{timingStaticCellText=false;}}
   if(GUI.enabled&&Event.current.type==EventType.MouseDown&&Event.current.button==0&&TimingPointerVisible()&&rect.Contains(Event.current.mousePosition)){
    int at;if(timingCellIndex.TryGetValue(key,out at)){var cell=timingCells[at];Queue(()=>ActivateTimingCell(cell));Event.current.Use();}
   }
  }
  void RestoreTimingCaret(string key){if(timingCaretKey==key&&GUI.GetNameOfFocusedControl()==key){var editor=(TextEditor)GUIUtility.GetStateObject(typeof(TextEditor),GUIUtility.keyboardControl);int length=(editor.text??"").Length;editor.cursorIndex=Mathf.Clamp(timingCaretCursor,0,length);editor.selectIndex=Mathf.Clamp(timingCaretSelection,0,length);timingCaretKey=null;}}
  void InitTimingViewport(){harmony.Patch(Method("HandleCommittedInput"),new HarmonyMethod(typeof(TimingViewportInput).GetMethod("Before",F)){priority=Priority.First+200});}
  static class TimingViewportInput {
   static bool Before(Event __0,ref bool __result){
    if(I==null||!I.ready||I.panel!="时序"||__0.type!=EventType.KeyDown||__0.keyCode!=KeyCode.Tab||I.timingEditKey==null)return true;
    if(GUI.GetNameOfFocusedControl()!=I.timingEditKey)return true;int at;if(!I.timingCellIndex.TryGetValue(I.timingEditKey,out at)||I.timingCells.Count==0)return true;
    var next=I.timingCells[(at+(__0.shift?-1:1)+I.timingCells.Count)%I.timingCells.Count];I.Queue(()=>I.ActivateTimingCell(next));__0.Use();__result=true;return false;
   }
  }
 }
}
