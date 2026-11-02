using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using InFalsusStudio.Core;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  bool managedLegacy,diagnosticsExpanded,legacyStacked;float legacyWidth;Rect legacyBodyRect;int legacyEventId=-1;
  void InitLegacyPanels(){
   InitReleaseAnnouncement();
   foreach(string name in LegacyAdaptedMethods)harmony.Patch(Method(name),transpiler:new HarmonyMethod(typeof(LegacyHooks).GetMethod("AdaptControls",F)){priority=Priority.Last});
   harmony.Patch(Method("CommittedRow"),new HarmonyMethod(typeof(LegacyHooks).GetMethod("FieldRow",F)){priority=Priority.Last});
   harmony.Patch(Method("DrawReleaseNotes"),new HarmonyMethod(typeof(LegacyHooks).GetMethod("ReleaseNotes",F)){priority=Priority.Last});
  }
  void DrawUnifiedLegacyPanel(){
   Fill(panelRect,Css("#0c202bfa"));Fill(new Rect(panelRect.x,panelRect.y,scale,panelRect.height),Css("#48636f"));
   bool secondary=panel=="设置"||panel=="工具"||panel=="参数高级";
   string title=LegacyPanelTitle(panel,settingsTab);
   if(panel=="时序"&&InFalsusStudio.TimeGroups.TimingGroups.Current!="")title+=" · "+InFalsusStudio.TimeGroups.TimingGroups.Current;
   GUI.BeginGroup(panelRect);try{
    if(secondary&&FlatButton(new Rect(22*scale,27*scale,32*scale,39.390625f*scale),"‹",26,false,false))ReturnFromLegacy();
    Rect titleRect=new Rect((secondary?65:28)*scale,27*scale,(secondary?325:310)*scale,39.390625f*scale);
    UiText(titleRect,UiFontRaster.Ellipsize(T(title),CNFace,FontPixels(secondary?20:24),titleRect.width),secondary?20:24,Css("#c9e9ee"));
    if(panel=="时序"&&FlatButton(new Rect(351*scale,27*scale,32*scale,39.390625f*scale),"?",20,Val<bool>(boot,"showTrackHelp"),false))Queue(delegate{Set(boot,"showTrackHelp",!Val<bool>(boot,"showTrackHelp"));});
    if(FlatButton(new Rect(398*scale,27*scale,32*scale,39.390625f*scale),"×",26,false,false))Open(panel);
    Fill(new Rect(28*scale,87.390625f*scale,405*scale,scale),Css("#35515c"));
    if(panel=="工程"){ProjectFolderButton(new Rect(28*scale,103*scale,405*scale,48*scale));DrawProjectAudioExport();}
   }finally{GUI.EndGroup();}
   string[] rectNames={"inspectorRect","songPanelRect","calibrationRect","notePanelRect"};var previousRects=rectNames.Select(n=>Val<Rect>(boot,n)).ToArray();var previousBox=GUI.skin.box;
   float bodyTop=panel=="工程"?245:103;
   Rect body=new Rect(panelRect.x+28*scale,panelRect.y+bodyTop*scale,panelRect.width-46*scale,Mathf.Max(30,panelRect.height-(bodyTop+15)*scale));
   legacyBodyRect=body;legacyWidth=body.width-26;legacyEventId=-1;managedLegacy=true;legacy=true;legacyStacked=secondary;GUI.skin.box=new GUIStyle(GUIStyle.none);
   foreach(string name in rectNames)Set(boot,name,body);
   try{
    string method=LegacyPanelMethod(panel,settingsTab);
    if(panel=="参数高级"){Set(boot,"guiNoteSourceExpanded",true);Set(boot,"noteSourceExpanded",true);}
    if(panel=="设置"&&settingsTab!="相机参数")DrawLegacyFragment(body,method);else Call(boot,method);
   }
   finally{for(int i=0;i<rectNames.Length;i++)Set(boot,rectNames[i],previousRects[i]);GUI.skin.box=previousBox;managedLegacy=false;legacy=false;legacyStacked=false;}
  }
  bool IsLegacyHeader(string text){return text=="时序 / 滚速事件"||text=="Timing / track"||text=="谱面检查"||text=="CHART CHECK"||text=="歌曲工程 / 四个难度"||text=="Song project / four difficulties"||text.EndsWith(" · 操作帮助")||text.EndsWith(" · Help")||(legacyStacked&&(text=="工具"||text=="TOOLS"||text=="背景 / 窗口"||text=="Background / window"||text.StartsWith("CALIBRATION  /")));}
  Rect LegacyRow(string text,float design,float padding=10,float reserve=16){int pixels=FontPixels(design),lines=Mathf.Max(1,UiFontRaster.Wrap(T(text??""),CNFace,pixels,legacyWidth-reserve*scale).Length);float lineHeight=Mathf.Max(pixels+4,design*1.8f*scale);return GUILayoutUtility.GetRect(0,Mathf.Max(37*scale,lines*lineHeight+padding*scale),GUILayout.ExpandWidth(true));}
  static bool LegacyActive{get{return I!=null&&I.ready&&I.managedLegacy;}}
  bool SongDiagnosticHidden(string text){return text!=null&&(text.StartsWith("指针状态：")||text.StartsWith("Cursor: ")||text=="实测物量证据（不是对象数）"||text=="Measured combo evidence (not object count)"||text==Val<string>(boot,"guiComboText")||text==Val<string>(boot,"guiComboWarning")||text==Val<string>(boot,"scoreEvidenceStatus"));}
  string DiagnosticText(string text){
   if(!Val<bool>(boot,"chinese")||string.IsNullOrEmpty(text))return text;
   if(text=="Fitted totals; tick timestamps and endpoint policy are not uniquely determined.")return "拟合总物量；判定时刻与首尾判定规则尚未完全确定。";
   if(text=="Select a note for candidate ticks / start BPM.")return "选中物件以查看候选判定数与起始 BPM。";
   if(text=="Export count report at playhead...")return "导出当前播放头估算报告…";
   if(text.StartsWith("Profile A:"))return "估算模型 A：Hold 按头部、周期判定与尾部计数；Sky 按开始与周期判定计数。具体判定时刻及跨 BPM 行为尚未确定。track 只影响视觉滚速。";
   return text.Replace("PROVISIONAL COUNT: ","估算总物量：").Replace("MEASURED REFERENCE MATCH: ","匹配实测参考：").Replace("EST. COMBO @ ","播放头估算 @ ").Replace("At playhead: ","播放头分项：").Replace("Selected ID ","选中 ID ").Replace("onset BPM ","起始 BPM ").Replace("interval ","间隔 ").Replace(" | crosses BPM (start-BPM hypothesis)","｜跨 BPM（按起始 BPM 估算）");
  }
  void DrawAttachedDiagnostics(){
   GUILayout.Space(16*scale);Rect row=LegacyRow("附加",17,14);
   if(FlatButton(new Rect(row.x,row.y+4*scale,row.width,row.height-8*scale),diagnosticsExpanded?"附加 · 收起":"附加",17,diagnosticsExpanded))Queue(delegate{diagnosticsExpanded=!diagnosticsExpanded;});
   if(!diagnosticsExpanded)return;
   LegacyLabel(T("物量估算与诊断（实验性）"),new GUILayoutOption[0]);
   LegacyLabel(T("仅供参考：长音符判定数、时间点及跨 BPM 规则尚未完全确定。"),new GUILayoutOption[0]);
   var cursor=Val<CursorPose>(boot,"cursorPose");LegacyLabel(T("指针状态：")+cursor.Phase+"  X="+(cursor.X*100).ToString("0.##",System.Globalization.CultureInfo.InvariantCulture),new GUILayoutOption[0]);
   LegacyLabel(Val<string>(boot,"guiComboText"),new GUILayoutOption[0]);LegacyLabel(Val<string>(boot,"guiComboWarning"),new GUILayoutOption[0]);
   Call(boot,"DrawComboDiagnostics");LegacyLabel(T("实测物量证据（不是对象数）"),new GUILayoutOption[0]);LegacyLabel(Val<string>(boot,"scoreEvidenceStatus"),new GUILayoutOption[0]);
   var evidence=Get(boot,"scoreEvidence") as ScoreEvidence;string result=LegacyFieldRow(T("实测总物量，未知填 -1"),"field_measuredcombo",evidence!=null?evidence.TotalCombo.ToString(System.Globalization.CultureInfo.InvariantCulture):"-1",delegate(string value){Call(boot,"SetMeasuredTotalCombo",value);},false);Set(boot,"measuredComboInput",result);
  }
  static void LegacyComboDiagnostics(InFalsusStudio.StudioBootstrap owner){if(!LegacyActive||I.panel!="工程")Call(owner,"DrawComboDiagnostics");}
  static void LegacyEndPanel(InFalsusStudio.StudioBootstrap owner){if(LegacyActive&&I.panel=="检查")I.DrawAttachedDiagnostics();Call(owner,"EndPanelContent");}
  static void LegacyLabel(string text,GUILayoutOption[] options){
   if(!LegacyActive){GUILayout.Label(text,options);return;}if(string.IsNullOrEmpty(text)||I.IsLegacyHeader(text))return;
   if(I.panel=="工程"&&I.SongDiagnosticHidden(text))return;if(I.panel=="检查"&&I.diagnosticsExpanded)text=I.DiagnosticText(text);
   if(I.panel=="检查")text=InFalsusStudio.TimeGroups.GroupDataPatches.HealthLabel(I.boot,text);
   text=I.SecondaryText(text);Rect row=I.LegacyRow(text,17,6);I.UiParagraph(new Rect(row.x+2,row.y+3,row.width-4,row.height-6),text,17,Css("#a6c3d1"));
  }
  static bool LegacyButton(string text,GUILayoutOption[] options){
   if(!LegacyActive)return GUILayout.Button(text,options);if(text=="×"||text=="关闭"||text=="Close")return false;
   if(I.panel=="检查"&&I.diagnosticsExpanded)text=I.DiagnosticText(text);
   text=I.SecondaryText(text);bool active=text.StartsWith("• ")||text.StartsWith("● ");if(active)text=text.Substring(2);
   var row=I.LegacyRow(text,16,14);return I.FlatButton(new Rect(row.x,row.y+4*I.scale,row.width,row.height-8*I.scale),text,16,active);
  }
  static bool LegacyContentButton(GUIContent content,GUILayoutOption[] options){if(!LegacyActive)return GUILayout.Button(content,options);if(content.text=="?")return false;return LegacyButton(content.text,options);}
  static string LegacyEventText(object value){var ev=value as SpcEvent;if(LegacyActive&&ev!=null)I.legacyEventId=ev.SourceId;return value.ToString();}
  static bool TimingRowSelected(SpcEvent selected,int sourceId){return selected!=null&&!selected.IsNote&&selected.SourceId==sourceId;}
  static bool LegacyStyledButton(string text,GUIStyle style,GUILayoutOption[] options){
   if(!LegacyActive)return GUILayout.Button(text,style,options);
   var selected=Get(I.boot,"guiPrimary") as SpcEvent;bool active=I.panel=="时序"&&TimingRowSelected(selected,I.legacyEventId);I.legacyEventId=-1;
   var row=I.LegacyRow(text,16,14);return I.FlatButton(new Rect(row.x,row.y+4*I.scale,row.width,row.height-8*I.scale),text,16,active);
  }
  static bool LegacyToggle(bool current,string text,GUILayoutOption[] options){
   if(!LegacyActive)return GUILayout.Toggle(current,text,options);
   text=I.SecondaryText(text);Rect row=I.LegacyRow(text,17,16,54);bool clicked=I.FeedbackButton(row,text);I.UiParagraph(new Rect(row.x,row.y,row.width-46*I.scale,row.height),text,17,Css("#a6c3d1"));
   var toggle=new Rect(row.xMax-32*I.scale,row.center.y-8.5f*I.scale,32*I.scale,17*I.scale);I.Border(toggle,Css("#6aacb4"),I.scale);Fill(new Rect(current?toggle.xMax-11*I.scale:toggle.x+3*I.scale,toggle.y+3*I.scale,8*I.scale,9*I.scale),Css(current?"#bcf7f5":"#577582"));
   return clicked?!current:current;
  }
  static bool LegacyStyledToggle(bool current,string text,GUIStyle style,GUILayoutOption[] options){return LegacyActive?LegacyToggle(current,text,options):GUILayout.Toggle(current,text,style,options);}
  static Rect LegacyInputRect(float minWidth,float height,GUILayoutOption[] options){return GUILayoutUtility.GetRect(minWidth,LegacyActive?Mathf.Max(31,40*I.scale):height,options);}
  static string LegacyCommittedField(InFalsusStudio.StudioBootstrap owner,Rect rect,string name,string live,Action<string> commit,bool area,bool allowEmpty){
   if(!LegacyActive||area)return (string)Call(owner,"CommittedField",rect,name,live,commit,area,allowEmpty);
   return I.UiInput(rect,name,live,commit,19,0,true,allowEmpty,CNFace);
  }
  static string LegacyFieldRow(string label,string key,string value,Action<string> commit,bool empty){
   label=I.SecondaryText(label);Rect row=I.LegacyRow(label,17,4);I.UiParagraph(row,label,17,Css("#a6c3d1"));Rect input=GUILayoutUtility.GetRect(0,Mathf.Max(31,40*I.scale),GUILayout.ExpandWidth(true));string result=I.UiInput(input,key,value,commit,19,0,true,empty,CNFace);GUILayout.Space(8*I.scale);return result;
  }
  internal static class LegacyHooks {
   static bool ReleaseNotes(InFalsusStudio.StudioBootstrap __instance){if(I==null||!I.ready)return true;I.Bind(__instance);I.DrawReadableReleaseNotes();return false;}
   static bool FieldRow(string __0,string __1,string __2,Action<string> __3,bool __4,ref string __result){
    if(!LegacyActive)return true;if(I.panel=="工程"&&__1=="field_measuredcombo"){__result=__2;return false;}
    __result=LegacyFieldRow(__0,__1,__2,__3,__4);return false;
   }
   static IEnumerable<CodeInstruction> AdaptControls(IEnumerable<CodeInstruction> instructions,MethodBase __originalMethod){
    // Source identity is needed only for timing-event rows. In particular, never
    // replace a constrained value-type ToString call: its receiver is a managed
    // pointer, and its constrained prefix cannot precede our static helper.
    bool timingRows=__originalMethod!=null&&__originalMethod.DeclaringType==typeof(InFalsusStudio.StudioBootstrap)&&__originalMethod.Name=="DrawTimingBrowser";
    bool constrainedReceiver=false;
    foreach(var code in instructions){bool constrained=constrainedReceiver;constrainedReceiver=code.opcode==OpCodes.Constrained;var method=code.operand as MethodInfo;
     if(method!=null){string helper=null;
      if(timingRows&&!constrained&&code.opcode==OpCodes.Callvirt&&method.DeclaringType==typeof(object)&&method.Name=="ToString"&&method.GetParameters().Length==0)helper="LegacyEventText";
      if(method.DeclaringType==typeof(InFalsusStudio.StudioBootstrap)&&method.Name=="CommittedField")helper="LegacyCommittedField";
      if(method.DeclaringType==typeof(InFalsusStudio.StudioBootstrap)&&method.Name=="DrawComboDiagnostics")helper="LegacyComboDiagnostics";
      if(method.DeclaringType==typeof(InFalsusStudio.StudioBootstrap)&&method.Name=="EndPanelContent")helper="LegacyEndPanel";
      if(method.DeclaringType==typeof(GUILayoutUtility)&&method.Name=="GetRect"&&method.GetParameters().Select(p=>p.ParameterType).SequenceEqual(new[]{typeof(float),typeof(float),typeof(GUILayoutOption[])}))helper="LegacyInputRect";
      if(helper!=null){code.operand=typeof(NativeEditorUIPlugin).GetMethod(helper,F);code.opcode=OpCodes.Call;}
     }
     if(method!=null&&method.DeclaringType==typeof(GUILayout)){
      var p=method.GetParameters().Select(x=>x.ParameterType).ToArray();string replacement=null;
      if(method.Name=="Label"&&p.SequenceEqual(new[]{typeof(string),typeof(GUILayoutOption[])}))replacement="LegacyLabel";
      if(method.Name=="Button"){
       if(p.SequenceEqual(new[]{typeof(string),typeof(GUILayoutOption[])}))replacement="LegacyButton";
       else if(p.SequenceEqual(new[]{typeof(GUIContent),typeof(GUILayoutOption[])}))replacement="LegacyContentButton";
       else if(p.SequenceEqual(new[]{typeof(string),typeof(GUIStyle),typeof(GUILayoutOption[])}))replacement="LegacyStyledButton";
      }
      if(method.Name=="Toggle"){
       if(p.SequenceEqual(new[]{typeof(bool),typeof(string),typeof(GUILayoutOption[])}))replacement="LegacyToggle";
       else if(p.SequenceEqual(new[]{typeof(bool),typeof(string),typeof(GUIStyle),typeof(GUILayoutOption[])}))replacement="LegacyStyledToggle";
      }
      if(method.Name=="BeginHorizontal"&&p.SequenceEqual(new[]{typeof(GUILayoutOption[])}))replacement="LegacyBeginHorizontal";
      if(method.Name=="EndHorizontal"&&p.Length==0)replacement="LegacyEndHorizontal";
      if(method.Name=="HorizontalSlider"&&p.SequenceEqual(new[]{typeof(float),typeof(float),typeof(float),typeof(GUILayoutOption[])}))replacement="LegacySlider";
      if(replacement!=null){code.operand=typeof(NativeEditorUIPlugin).GetMethod(replacement,F);code.opcode=OpCodes.Call;}
     }yield return code;
    }
   }
  }
 }
}
