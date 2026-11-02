using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using InFalsusStudio.Core;
using InFalsusStudio.TimeGroups;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  bool notePopupOpen;Rect notePopupRect;Vector2 popupScroll;int[] popupIds=new int[0];float popupY;
  NoteProperties popupForm;Dictionary<string,string> popupValues=new Dictionary<string,string>();EditHistory popupHistory;int popupRevision=-1,popupGeneration;
  const int PopupId=0x49465350;
  int popupNumericControl,popupNumericSelectOnMouseUp;string popupNumericKey="";
  bool popupKeyboardOwner,popupClearFocus;string popupFocusedField="";
  bool PopupOwnsTextFocus{get{return notePopupOpen&&popupKeyboardOwner&&popupFocusedField.StartsWith("field_",StringComparison.Ordinal);}}
  void RefreshPopupSelection(){
   // A closed popup does not need source forms until it is shown again. Keep
   // its revision stale so reopening always captures the accepted document.
   if(!notePopupOpen&&ReferenceEquals(boot.History,popupHistory)&&Val<HashSet<int>>(boot,"selection").SetEquals(popupIds))return;
   RefreshFilterNotes();var history=(EditHistory)Prop(boot,"History");
   var ids=history==null?new int[0]:NoteFilterPolicy.Filter(Val<HashSet<int>>(boot,"selection"),filterNotes,NoteFilterMask.All).OrderBy(i=>i).ToArray();
   bool changed=!ids.SequenceEqual(popupIds)||!ReferenceEquals(history,popupHistory);
   if(changed){popupIds=ids;popupGeneration++;popupScroll=Vector2.zero;advancedNote=false;popupTimingExpanded=false;notePopupOpen=ids.Length>0;popupNumericControl=0;popupNumericSelectOnMouseUp=0;popupNumericKey="";popupKeyboardOwner=false;popupFocusedField="";}
   if(ids.Length==0){notePopupOpen=false;popupForm=null;popupValues.Clear();popupHistory=history;popupRevision=history==null?-1:history.Revision;popupTimingNames=new string[0];return;}
   if(changed||popupRevision!=history.Revision){
    popupHistory=history;popupRevision=history.Revision;popupForm=null;popupValues.Clear();
    var notes=ids.Select(i=>filterNotes[i]).ToArray();
    var model=TimingGroups.Model(history.Document);popupTimingNames=new[]{""}.Concat(model.Groups.Select(g=>g.Name)).Distinct(StringComparer.Ordinal).ToArray();
    var memberships=ids.Select(model.Of).Distinct(StringComparer.Ordinal).ToArray();popupTimingValue=memberships.Length==1?memberships[0]:null;
    if(notes.All(n=>n.Kind==notes[0].Kind)){
     var forms=notes.Select(n=>NoteProperties.Capture(history.Document,n)).ToArray();int primary=Val<int>(boot,"selectedId");
     popupForm=forms.FirstOrDefault(f=>f.SourceId==primary)??forms[0];
     foreach(var kv in forms[0].Values)popupValues[kv.Key]=forms.All(f=>f.Values.ContainsKey(kv.Key)&&f.Values[kv.Key]==kv.Value)?kv.Value:"";
    }
   }
   if(notePopupRect.width<=0)notePopupRect=new Rect(220*scale,240*scale,420*scale,450*scale);
  }
  Action<string> PopupCommit(string key){
   var history=popupHistory;var ids=(int[])popupIds.Clone();var kind=popupForm.Kind;
   return delegate(string value){
    var current=(EditHistory)Prop(boot,"History");RefreshFilterNotes();
    var selected=Val<HashSet<int>>(boot,"selection").Where(i=>filterNotes.ContainsKey(i)).OrderBy(i=>i).ToArray();
    if(!ReferenceEquals(current,history)||!ids.SequenceEqual(selected)||ids.Any(i=>!filterNotes.ContainsKey(i)||filterNotes[i].Kind!=kind))throw new InvalidOperationException(T("源语句已变化，请重新选择。"));
    // The editor's existing transaction preserves source tokens, undo and manual Save.
    if(!(bool)Call(boot,"Change",new Action<SpcDocument>(delegate(SpcDocument document){SelectionAuthoring.Field(document,ids,kind,key,value);})))throw new ArgumentException(Convert.ToString(Get(boot,"status")));
    Set(boot,"noteForm",null);
   };
  }
  void LayoutPopup(){
   if(!notePopupOpen)return;
   float popupScale=Mathf.Max(.8f,scale),width=420*popupScale,limit=WaveVisible?waveRect.y:footerRect.y;
   notePopupRect.width=Mathf.Min(width,Screen.width-2*leftRect.width);
   var form=popupForm;
   float desired=form!=null&&form.Kind==EventKind.SkyArea?620:form!=null&&form.Kind==EventKind.Hold?360:form!=null&&form.Kind==EventKind.Flick?355:300;
   notePopupRect.height=Mathf.Min((desired+48)*popupScale,Mathf.Max(220*popupScale,limit-205*scale));
   notePopupRect.x=Mathf.Clamp(notePopupRect.x,leftRect.width,Mathf.Max(leftRect.width,Screen.width-rightRect.width-notePopupRect.width));
   notePopupRect.y=Mathf.Clamp(notePopupRect.y,205*scale,Mathf.Max(205*scale,limit-notePopupRect.height));
  }
  void DrawNotePopup(){
   if(!notePopupOpen||popupIds.Length==0||formal)return;LayoutPopup();
   notePopupRect=GUI.Window(PopupId,notePopupRect,DrawPopupBody,GUIContent.none,GUIStyle.none);
  }
  void DrawPopupBody(int id){
   // Unity windows have separate keyboard scopes. Keep their first-focus token
   // separate too: the root footer may retain its own focused numeric field.
   float layoutScale=scale;int rootControl=numericControl,rootMouseUp=numericSelectOnMouseUp;string rootKey=numericKey;
   numericControl=popupNumericControl;numericSelectOnMouseUp=popupNumericSelectOnMouseUp;numericKey=popupNumericKey;
   scale=Mathf.Max(.8f,scale);try{if(popupClearFocus){GUI.FocusControl(null);popupClearFocus=false;}DrawPopupContent(id);popupFocusedField=GUI.GetNameOfFocusedControl();}finally{popupNumericControl=numericControl;popupNumericSelectOnMouseUp=numericSelectOnMouseUp;popupNumericKey=numericKey;numericControl=rootControl;numericSelectOnMouseUp=rootMouseUp;numericKey=rootKey;scale=layoutScale;}
  }
  void DrawPopupContent(int id){
   var width=notePopupRect.width;
   Fill(new Rect(0,0,width,notePopupRect.height),Css("#0c202bf5"));Border(new Rect(0,0,width,notePopupRect.height),Css("#486773"),scale);
   var form=popupForm;
   string kind=form==null?"多类型选择":form.Kind.ToString();
   UiText(new Rect(22*scale,12*scale,width-64*scale,36*scale),kind+"  ·  "+popupIds.Length+" 个物件",18,Css("#d3edf0"));
   if(FlatButton(new Rect(width-54*scale,13*scale,34*scale,40*scale),"×",24,false,false))Queue(delegate{Call(boot,"CommitAllInputFieldsNow");Call(boot,"CommitPendingNoteNow");notePopupOpen=false;popupTimingExpanded=false;GUI.FocusControl(null);});
   Fill(new Rect(20*scale,54*scale,width-40*scale,scale),Css("#35515c"));
   string hint="拖动标题移动 · Enter / 失焦应用 · Ctrl 多选";
   float hintHeight=UiFontRaster.Wrap(T(hint),CNFace,FontPixels(11),width-44*scale).Length*(FontPixels(11)+3);
   Rect hintRect=new Rect(22*scale,notePopupRect.height-hintHeight-12*scale,width-44*scale,hintHeight);
   Rect area=new Rect(20*scale,64*scale,width-40*scale,Mathf.Max(40*scale,hintRect.y-74*scale));
   float height=form!=null&&form.Kind==EventKind.SkyArea?480:form!=null&&form.Kind==EventKind.Hold?240:form!=null&&form.Kind==EventKind.Flick?225:185;
   if(form==null)height=Mathf.Max(220,popupIds.Length>1?350:220);
   height+=42+(popupTimingExpanded?popupTimingNames.Length*38:0);
   popupScroll=GUI.BeginScrollView(area,popupScroll,new Rect(0,0,360*scale,Mathf.Max(area.height,(height+(advancedNote?215:0))*scale)),false,false);
   bool enabled=GUI.enabled;GUI.enabled=enabled&&(Transport==null||!Transport.Playing);
   try{
    popupY=0;var values=popupValues;
    if(form==null||values==null){DrawMixedSelection();DrawPopupTimingGroup();}else{
     PopupField(values,"start","开始时间","ms");if(form.Kind==EventKind.Hold||form.Kind==EventKind.SkyArea)PopupField(values,"end","结束时间","ms");
     if(form.Kind==EventKind.SkyArea){
      PopupField(values,"sx","起点位置","%");PopupField(values,"ex","终点位置","%");PopupField(values,"sw","起点宽度","%");PopupField(values,"ew","终点宽度","%");PopupField(values,"bothWidths","统一宽度","%");
      PopupChoices(values,"leftEase","左缓动",new[]{"线性","SineOut","SineIn"},new[]{"0","1","2"});
      PopupChoices(values,"rightEase","右缓动",new[]{"线性","SineOut","SineIn"},new[]{"0","1","2"});
      PopupField(values,"group","groupid","",true);
     }else if(form.Kind==EventKind.Flick){
      PopupField(values,"x","中心位置","%");PopupField(values,"width","宽度","%");
      PopupChoices(values,"direction","方向",new[]{"左 · 黄","右 · 绿"},new[]{"16","4"});
     }else{
      PopupField(values,"lane","起始轨道","0～5");
      PopupChoices(values,"width","中央轨宽度",new[]{"1","2","3","4"},new[]{"1","2","3","4"});
     }
     DrawPopupTimingGroup();popupY+=8*scale;Rect row=new Rect(0,popupY,360*scale,40*scale);
     if(FlatButton(new Rect(0,row.y,170*scale,row.height),"跳到开始时间",13)){int source=form.SourceId;Queue(delegate{RefreshFilterNotes();SpcEvent note;if(filterNotes.TryGetValue(source,out note)){Transport.Pause();Transport.Seek(note.TimeMs);}});}
     if(FlatButton(new Rect(182*scale,row.y,178*scale,row.height),advancedNote?"收起原始语句":"原始语句 / 高级参数",13))Queue(delegate{advancedNote=!advancedNote;});
     if(advancedNote){popupY+=65*scale;DrawPopupSource(form);}
    }
   }finally{GUI.enabled=enabled;GUI.EndScrollView();}
   UiText(hintRect,hint,11,Css("#a6c3d1"));
   GUI.DragWindow(new Rect(0,0,width-65*scale,54*scale));
  }
  void PopupField(Dictionary<string,string> values,string key,string title,string unit,bool empty=false){
   Rect row=new Rect(0,popupY,360*scale,42*scale);popupY+=42*scale;Fill(new Rect(0,row.yMax-scale,row.width,scale),Css("#2c4652"));
   UiText(new Rect(0,row.y,145*scale,row.height),title,15,Css("#a6c3d1"));string value;values.TryGetValue(key,out value);if(key=="bothWidths"){string sw,ew;if(values.TryGetValue("sw",out sw)&&values.TryGetValue("ew",out ew)&&sw==ew)value=sw;}
   UiInput(new Rect(155*scale,row.y+6*scale,145*scale,30*scale),"field_nativeui_note_"+popupGeneration+"_"+key,DisplayPrecision.Coordinate(key,value??""),PopupCommit(key),19,2,true,empty||popupIds.Length>1);
   UiText(new Rect(309*scale,row.y,51*scale,row.height),unit,13,Css("#7c9faf"));
  }
  void PopupChoices(Dictionary<string,string> values,string key,string title,string[] labels,string[] choices){
   Rect row=new Rect(0,popupY,360*scale,45*scale);popupY+=45*scale;Fill(new Rect(0,row.yMax-scale,row.width,scale),Css("#2c4652"));
   UiText(new Rect(0,row.y,122*scale,row.height),title,15,Css("#a6c3d1"));string current;values.TryGetValue(key,out current);
   float w=228f/labels.Length;var commit=PopupCommit(key);for(int i=0;i<labels.Length;i++){string next=choices[i];if(FlatButton(new Rect((132+i*w)*scale,row.y+8*scale,(w-6)*scale,29*scale),labels[i],13,current==next))Queue(delegate{commit(next);});}
  }
  void DrawPopupSource(NoteProperties form){
   if(popupIds.Length!=1){UiParagraph(new Rect(0,popupY,360*scale,70*scale),"原始语句编辑仅支持单个物件。",14,Css("#7c9faf"));return;}
   var history=(EditHistory)Prop(boot,"History");int id=form.SourceId;string expected=form.OriginalSource;
   UiText(new Rect(0,popupY,360*scale,30*scale),"原始语句 · Enter / 失焦应用",14,Css("#9dbfcd"));popupY+=35*scale;
   GUILayout.BeginArea(new Rect(0,popupY,360*scale,175*scale));try{
    Call(boot,"CommittedSource","field_note_raw_"+id,expected,new Action<string>(delegate(string value){
     var current=(EditHistory)Prop(boot,"History");
     if(!ReferenceEquals(current,history)||!history.Document.Contains(id)||history.Document.SourceLine(id)!=expected)throw new InvalidOperationException(T("源语句已变化，请重新选择。"));
     if(!(bool)Call(boot,"Change",new Action<SpcDocument>(delegate(SpcDocument document){document.ReplaceSourceLine(id,value);})))throw new ArgumentException(Convert.ToString(Get(boot,"status")));
     Set(boot,"noteForm",null);
    }),142*scale);
   }finally{GUILayout.EndArea();}
  }
  void DrawMixedSelection(){
   UiParagraph(new Rect(0,popupY,360*scale,90*scale),"已选中多种类型。选择一类物件后编辑其参数；不会更改其他类型的物件。",16,Css("#a6c3d1"));popupY+=100*scale;RefreshFilterNotes();
   foreach(var kind in new[]{EventKind.Tap,EventKind.Hold,EventKind.SkyArea,EventKind.Flick}){
    var ids=popupIds.Where(i=>filterNotes.ContainsKey(i)&&filterNotes[i].Kind==kind).ToArray();if(ids.Length==0)continue;
    if(FlatButton(new Rect(0,popupY,360*scale,43*scale),kind+"  ·  "+ids.Length+" 个",16)){var events=Val<HashSet<int>>(boot,"selection").Where(i=>!filterNotes.ContainsKey(i));Command("SetSelection",ids.Concat(events).ToArray(),ids[0]);}popupY+=53*scale;
   }
  }
 }
}
