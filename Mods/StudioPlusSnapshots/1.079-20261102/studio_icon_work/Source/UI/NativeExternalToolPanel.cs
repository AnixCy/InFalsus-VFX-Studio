using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using InFalsusStudio.Core;
using InFalsusStudio.TimeGroups;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  void MatchedRain(){
   bool enabled=GUI.enabled;GUI.enabled=enabled&&rainProcess==null;
   try{
    var row=ContentRow(63);RowLine(row);UiText(new Rect(0,row.y,80*scale,row.height),RainL("工具","Tool"),16,Css("#a6c3d1"));
    if(FlatButton(new Rect(85*scale,row.y+12*scale,269*scale,38*scale),externalTool==null?"ExternalTools":ToolL(externalTool.Manifest.name,externalTool.Manifest.nameEn),15,true))Queue(delegate{externalMenu=!externalMenu;externalChoice="";});
    if(FlatButton(new Rect(366*scale,row.y+12*scale,39*scale,38*scale),"↻",23))Queue(delegate{try{if(!(bool)Call(boot,"CommitAllInputFieldsNow"))return;DiscoverExternalTools();if(externalTool!=null){LoadToolDefaults(externalTool);SaveTool(externalTool);}externalMessage=RainL("已重载工具及默认参数。","Tools and defaults reloaded.");}catch(Exception e){ToolError(e);}});
    if(externalMenu)foreach(var candidate in externalTools){var tool=candidate;var r=ContentRow(44);if(FlatButton(new Rect(0,r.y+3*scale,405*scale,38*scale),ToolL(tool.Manifest.name,tool.Manifest.nameEn),15,tool==externalTool))Queue(delegate{if(!(bool)Call(boot,"CommitAllInputFieldsNow"))return;externalTool=tool;externalMenu=false;externalChoice="";externalMessage="";GUI.FocusControl(null);});}
    var state=externalTool;if(state!=null){
     if(state.Manifest.capture){var capture=ContentRow(75);if(FlatButton(new Rect(0,capture.y+12*scale,267*scale,40*scale),state.Manifest.captureKind=="air"?RainL("捕获所选 Flick / SkyArea","Capture Flick / SkyArea"):RainL("捕获所选 SkyArea","Capture selected SkyArea"),15))Queue(delegate{CaptureExternalPattern(state);});UiText(new Rect(280*scale,capture.y,125*scale,capture.height),(state.Manifest.captureKind=="air"?state.Sources.Length:state.Segments.Select(s=>s.path).Distinct().Count())+RainL(" 个物件 / 路径"," notes / paths"),13,Css("#7c9dad"));}
     if(state.Manifest.output=="timingedit")TimingRangePanel();
     foreach(var field in state.Manifest.fields)DrawToolField(state,field);
     if(ToolGroupOption(state.Manifest))MatchedToggle(RainL("新建 noinput 组","Create noinput group"),state.CreateGroup,v=>Queue(delegate{state.CreateGroup=v;SaveTool(state);}));
     var target=ContentRow(38);UiText(target,RainL("当前时间组：","Current timinggroup: ")+TimingGroupCaption(TimingGroups.Current),13,Css("#789eae"));
     string description=ToolL(state.Manifest.description,state.Manifest.descriptionEn);float helpHeight=Math.Max(173,UiFontRaster.Wrap(description,CNFace,FontPixels(13),405*scale).Length*Math.Max(FontPixels(13)+4,13*1.8f*scale)/scale+24);var explanation=ContentRow(helpHeight);UiParagraph(new Rect(0,explanation.y+8*scale,405*scale,(helpHeight-16)*scale),description,13,Css("#789eae"));
    }
   }finally{GUI.enabled=enabled;}
   var buttons=ContentRow(64);GUI.enabled=enabled&&rainProcess==null&&externalTool!=null&&boot.HasDocument&&!Transport.Playing;
   try{if(FlatButton(new Rect(0,buttons.y+10*scale,116*scale,42*scale),RainL("执行","Run"),17))Queue(delegate{StartExternalTool(false);});if(FlatButton(new Rect(126*scale,buttons.y+10*scale,153*scale,42*scale),externalTool!=null&&externalTool.Manifest.output=="timingedit"?RainL("计算事件数","Count only"):RainL("计算物件数","Count only"),15))Queue(delegate{StartExternalTool(true);});}finally{GUI.enabled=enabled;}
   if(FlatButton(new Rect(289*scale,buttons.y+10*scale,116*scale,42*scale),rainProcess==null?RainL("返回","Back"):RainL("取消生成","Cancel"),15))Queue(delegate{if(rainProcess!=null)CancelRain(true);else Open("工具");});
   var message=ContentRow(190);if(externalMessage.Length>0)UiParagraph(new Rect(0,message.y+10*scale,405*scale,180*scale),externalMessage,13,Css("#b3d0da"));externalContentHeight=contentY/scale+12;
  }
  void DrawToolField(ToolState state,ToolField f){
   if(f.key==state.Manifest.gridField){
    var gridRow=ContentRow(66);RowLine(gridRow);UiText(new Rect(0,gridRow.y,182*scale,gridRow.height),RainL("主界面网格","Main editor grid"),15,Css("#a6c3d1"));
    UiText(new Rect(188*scale,gridRow.y,217*scale,gridRow.height),RainL("跟随主界面 · 1/","Follows main grid · 1/")+Prop(boot,"Division"),13,Css("#8caab8"));return;
   }
   string value=state.Values[f.key];if(f.type=="bool"){bool on;bool.TryParse(value,out on);MatchedToggle(ToolL(f.label,f.labelEn),on,v=>Queue(delegate{ToolCommit(state,f,v?"true":"false");}));return;}
   var row=ContentRow(66);RowLine(row);UiText(new Rect(0,row.y,182*scale,row.height),ToolL(f.label,f.labelEn),15,Css("#a6c3d1"));
   if(f.type=="choice"){
    var current=f.options.FirstOrDefault(o=>o.value==value)??f.options[0];if(FlatButton(new Rect(188*scale,row.y+15*scale,217*scale,36*scale),ToolL(current.label,current.labelEn)+"  ›",14))Queue(delegate{externalChoice=externalChoice==f.key?"":f.key;});
    if(externalChoice==f.key)for(int i=0;i<f.options.Length;i+=2){var choices=ContentRow(42);for(int j=i;j<Math.Min(i+2,f.options.Length);j++){var option=f.options[j];if(FlatButton(new Rect((j%2)*208*scale,choices.y+3*scale,197*scale,36*scale),ToolL(option.label,option.labelEn),13,option.value==value))Queue(delegate{ToolCommit(state,f,option.value);externalChoice="";});}}return;
   }
   bool extra=f.type=="time"||f.type=="file";UiInput(new Rect(188*scale,row.y+15*scale,(extra?171:217)*scale,36*scale),"field_external_"+state.Manifest.id+"_"+f.key,value,v=>ToolCommit(state,f,v),19,2,true,f.type=="text"||f.type=="file");
   if(extra&&FlatButton(new Rect(366*scale,row.y+15*scale,39*scale,36*scale),f.type=="time"?"+":"…",20))Queue(delegate{if(!(bool)Call(boot,"CommitAllInputFieldsNow"))return;if(f.type=="time")ToolCommit(state,f,Math.Max(0,Math.Min(86400000,Math.Round(Transport.TimeMs))).ToString(CultureInfo.InvariantCulture));else{var type=typeof(StudioBootstrap).Assembly.GetType("InFalsusStudio.StudioFileDialogs");string path=(string)type.GetMethod("Open",F).Invoke(null,new object[]{"Choose TTF / OTF / TTC font",Path.Combine(state.Folder,"Fonts"),""});if(path.Length>0)ToolCommit(state,f,path);}GUI.FocusControl(null);});
  }
  void CaptureExternalPattern(ToolState state){
   try{
    if(!(bool)Call(boot,"CommitAllInputFieldsNow")||!boot.HasDocument||Transport.Playing)return;
    if(state.Manifest.captureKind=="air"){CaptureUniversalPattern(state);return;}
    var ids=Val<HashSet<int>>(boot,"selection");var source=boot.Events.Where(e=>ids.Contains(e.SourceId)&&e.Kind==EventKind.SkyArea&&Math.Abs(e.Get(1)/e.Get(2))<=20&&Math.Abs(e.Get(4)/e.Get(5))<=20).ToArray();
    if(source.Length<1||source.Length>256)throw new ArgumentException(RainL("请选择 1～256 个可见 SkyArea。","Select 1..256 visible SkyArea paths."));if(source.Any(e=>e.DurationMs<1||ChartMath.InvalidReason(e)!=null))throw new ArgumentException(RainL("所选路径须有效且时长大于 0 ms。","Selected paths must be valid with positive duration."));
    var captured=new List<ToolSegment>();foreach(var e in source){int count=(e.Get(7)==0&&e.Get(8)==0)?1:Math.Min(16,(int)e.DurationMs);for(int i=0;i<count;i++){int a=(int)Math.Round(e.TimeMs+e.DurationMs*i/count,MidpointRounding.AwayFromZero),b=(int)Math.Round(e.TimeMs+e.DurationMs*(i+1)/count,MidpointRounding.AwayFromZero);if(b<=a)continue;captured.Add(new ToolSegment{start=a,end=b,path=e.SourceId,x1=ChartMath.SkyAt(e,a).Center,x2=ChartMath.SkyAt(e,b).Center,ease=0});}}
    if(state.Manifest.id=="TunnelGenerator"&&source.Length<2)throw new ArgumentException(RainL("隧道生成至少需要两条所选路径。","Tunnel generation needs at least two selected paths."));state.Segments=captured.ToArray();File.WriteAllText(Path.Combine(state.Folder,"pattern.json"),ToolJson.Write(new ToolCapture{segments=state.Segments}),new UTF8Encoding(false));
    if(state.Manifest.id=="TunnelGenerator"){state.Values["start"]=source.Min(e=>e.TimeMs).ToString("0",CultureInfo.InvariantCulture);state.Values["end"]=source.Max(e=>e.EndMs).ToString("0",CultureInfo.InvariantCulture);SaveTool(state);}externalMessage=RainL("已捕获 "+source.Length+" 条路径；原物件未修改。","Captured "+source.Length+" paths; original notes retained.");
   }catch(Exception e){ToolError(e);}
  }
 }
}
