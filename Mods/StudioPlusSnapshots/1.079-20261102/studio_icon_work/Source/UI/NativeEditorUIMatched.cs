using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Globalization;
using UnityEngine;
using InFalsusStudio;
using InFalsusStudio.Core;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  const string CNFace="Chiron Hei HK N",NumFace="Bai Jamjuree Medium";
  bool audioDetails,onlyCurrentGroup;readonly NumericSelectionGesture numericPointer=new NumericSelectionGesture();
  float contentY,displayContentHeight;
  string PathForIcon(string key){return Path.Combine(Path.GetDirectoryName(GetType().Assembly.Location),"Icons",key+".png");}
  static readonly Dictionary<string,Color> timingPalette=new Dictionary<string,Color>();
  static Color Css(string code){Color color;bool timing=I!=null&&I.timingPaintActive;if(timing&&timingPalette.TryGetValue(code,out color))return color;ColorUtility.TryParseHtmlString(code,out color);if(timing&&timingPalette.Count<64)timingPalette[code]=color;return color;}
  void UiText(Rect rect,string text,float size,Color color,string face=CNFace,int align=0){
   if(timingPaintActive&&!TimingRectVisible(rect))return;
   RasterText(rect,text,size,color,face,align);
  }
  void UiParagraph(Rect rect,string text,float size,Color color){
   if(timingPaintActive&&!TimingRectVisible(rect))return;
   if(Event.current.type!=EventType.Repaint)return;text=T(text);int pixels=FontPixels(size);float y=rect.y,lineHeight=Mathf.Max(pixels+4,size*1.8f*scale);
   foreach(string line in UiFontRaster.Wrap(text,CNFace,pixels,rect.width)){RasterLine(new Rect(rect.x,y,rect.width,lineHeight),line,CNFace,pixels,color);y+=lineHeight;}
  }
  bool FlatButton(Rect rect,string text,float size=16,bool active=false,bool boxed=true){
   if(timingPaintActive&&!TimingRectVisible(rect))return false;
   bool hover=rect.Contains(Event.current.mousePosition);
   if(!timingPaintActive||Event.current.type==EventType.Repaint){
    if(boxed){Fill(rect,Css(hover?"#173743":"#102b36"));Border(rect,Css(active?"#82cfd6":"#375665"),scale);}
    else if(hover)Fill(rect,Css("#173743"));
   }
   bool pressed=FeedbackButton(rect,text);UiText(rect,text,size,Css(active?"#bbf7f5":"#9dbfcd"),CNFace,1);return pressed;
  }
  string UiInput(Rect rect,string key,string value,Action<string> commit,float size=26,int align=2,bool boxed=true,bool empty=false,string face=NumFace,bool selectAllOnFocus=true,int pixelSize=0,float? inkTop=null){
   int inputPixels=pixelSize>0?pixelSize:FontPixels(size);
   rect.position=PixelOrigin(rect.x,rect.y);rect.width=Mathf.Round(rect.width);rect.height=Mathf.Round(rect.height);
   if(boxed){Fill(rect,Css("#091923"));Fill(new Rect(rect.x,rect.yMax-scale,rect.width,scale),Css("#4b7481"));}
   var previous=GUI.skin.textField;int padding=boxed?(int)(5*scale):0;var field=InputStyle(inputPixels,padding,align);
   var eventType=Event.current.type;var mouse=Event.current.mousePosition;bool shift=Event.current.shift,wasFocused=GUI.GetNameOfFocusedControl()==key;int clicks=Event.current.clickCount;int oldAnchor=wasFocused?((TextEditor)GUIUtility.GetStateObject(typeof(TextEditor),GUIUtility.keyboardControl)).selectIndex:0;
   Color ink=Css("#d3edf0"),previousCaret=GUI.skin.settings.cursorColor,previousSelection=GUI.skin.settings.selectionColor;GUI.skin.textField=field;GUI.skin.settings.cursorColor=Color.clear;GUI.skin.settings.selectionColor=Color.clear;string entered;
   try{entered=Edit(rect,key,value,commit,empty);}finally{GUI.skin.textField=previous;GUI.skin.settings.cursorColor=previousCaret;GUI.skin.settings.selectionColor=previousSelection;}
   bool focused=GUI.GetNameOfFocusedControl()==key;TextEditor editor=null;if(focused){int control=GUIUtility.keyboardControl;editor=(TextEditor)GUIUtility.GetStateObject(typeof(TextEditor),control);numericControls.Add(control);
    if(numericControl!=control||numericKey!=key){if(selectAllOnFocus){editor.SelectAll();if(eventType==EventType.MouseDown)numericSelectOnMouseUp=control;}else if(eventType!=EventType.MouseDown){editor.cursorIndex=(entered??value??"").Length;editor.selectIndex=editor.cursorIndex;}numericControl=control;numericKey=key;}
    if(!selectAllOnFocus&&eventType==EventType.MouseDown&&clicks==1&&rect.Contains(mouse)){int index=InputHitIndex(rect,entered,face,inputPixels,align,padding,key,mouse.x);if(!shift)editor.selectIndex=index;editor.cursorIndex=index;inputDrag[key]=editor.selectIndex;}
    if(!selectAllOnFocus&&eventType==EventType.MouseDown&&rect.Contains(mouse)&&clicks>1){if(clicks>=3)editor.SelectAll();else{var span=InputTextSelection.Word(entered,InputHitIndex(rect,entered,face,inputPixels,align,padding,key,mouse.x));editor.selectIndex=span[0];editor.cursorIndex=span[1];}inputDrag[key]=editor.selectIndex;}
    if(wasFocused&&eventType==EventType.MouseDrag&&numericSelectOnMouseUp!=control&&inputDrag.ContainsKey(key)){editor.selectIndex=inputDrag[key];editor.cursorIndex=InputHitIndex(rect,entered,face,inputPixels,align,padding,key,mouse.x);}
    if(selectAllOnFocus){
     if(eventType==EventType.MouseDown&&rect.Contains(mouse)){numericPointer.Begin(key,wasFocused,shift,oldAnchor,InputHitIndex(rect,entered,face,inputPixels,align,padding,key,mouse.x),(entered??value??"").Length);numericSelectOnMouseUp=0;}
     if(numericPointer.Is(key)&&(eventType==EventType.MouseDown||eventType==EventType.MouseDrag||eventType==EventType.MouseUp)){
      if(eventType==EventType.MouseDrag)numericPointer.Drag(InputHitIndex(rect,entered,face,inputPixels,align,padding,key,mouse.x));
      if(numericPointer.All)editor.SelectAll();else{editor.selectIndex=numericPointer.Anchor;editor.cursorIndex=numericPointer.Cursor;}
      if(eventType==EventType.MouseUp)numericPointer.End();
     }
    }
   }
   if(Event.current.type==EventType.Repaint){Fill(rect,Css(boxed?"#091923":"#091c25"));if(boxed)Fill(new Rect(rect.x,rect.yMax-scale,rect.width,scale),Css("#4b7481"));}
   PaintInput(rect,key,entered??value,face,inputPixels,align,padding,focused,editor,ink,inkTop);
   return entered;
  }
  Rect ContentRow(float height){Rect row=new Rect(0,contentY,405*scale,height*scale);contentY+=height*scale;return row;}
  void RowLine(Rect row){Fill(new Rect(row.x,row.yMax-scale,row.width,scale),Css("#2c4652"));}
  void MatchedField(string title,string key,string value,Action<string> commit,string unit="",bool empty=false){
   Rect row=ContentRow(79);RowLine(row);UiText(new Rect(0,row.y,245*scale,row.height),title,17,Css("#a6c3d1"));
   float unitWidth=unit.Length==0?0:41.2f*scale;
   Rect input=new Rect(row.xMax-unitWidth-95*scale,row.y+19*scale,95*scale,40*scale);UiInput(input,key,value,commit,26,2,true,empty);
   if(unit.Length>0)UiText(new Rect(input.xMax+12*scale,row.y,unitWidth-12*scale,row.height),unit,14,Css("#7b9cae"),"Supreme");
  }
  void MatchedToggle(string title,bool current,Action<bool> change){
   Rect row=ContentRow(62.796875f);RowLine(row);bool clicked=FeedbackButton(row,title);UiText(new Rect(0,row.y,row.width-48*scale,row.height),title,17,Css("#a6c3d1"));
   Rect toggle=new Rect(row.xMax-32*scale,row.center.y-8.5f*scale,32*scale,17*scale);Border(toggle,Css("#6aacb4"),scale);
   Fill(new Rect(current?toggle.xMax-11*scale:toggle.x+3*scale,toggle.y+3*scale,8*scale,9*scale),Css(current?"#bcf7f5":"#577582"));
   if(clicked)change(!current);
  }
  void MatchedFlag(string title,string field){MatchedToggle(title,Val<bool>(boot,field),delegate(bool value){Queue(delegate{Set(boot,field,value);});});}
  void MatchedLink(string title,Action action){
   Rect row=ContentRow(62.796875f);RowLine(row);bool clicked=FeedbackButton(row,title);UiText(new Rect(0,row.y,row.width-30*scale,row.height),title,17,Css("#a6c3d1"));UiText(new Rect(row.xMax-20*scale,row.y,20*scale,row.height),"›",21,Css("#83a9ba"),CNFace,1);if(clicked)action();
  }
  void MatchedSection(string title){contentY+=24*scale;Rect row=ContentRow(32);UiText(row,title,14,Css("#7c9faf"));Fill(new Rect(0,row.yMax-scale,row.width,scale),Css("#2c4652"));}
  void MatchedSlider(string title,float current,Action<float> change){
   Rect row=ContentRow(79);RowLine(row);UiText(new Rect(0,row.y,245*scale,40*scale),title,17,Css("#a6c3d1"));UiText(new Rect(row.xMax-85*scale,row.y,85*scale,40*scale),Mathf.RoundToInt(current*100)+"%",22,Css("#d3edf0"),NumFace,2);
   float next=GUI.HorizontalSlider(new Rect(0,row.y+49*scale,row.width,17*scale),current,0,1);if(next!=current)change(next);
  }
  void DrawMatchedPanel(){
   float layoutScale=scale;scale=Mathf.Max(.8f,scale);try{DrawMatchedPanelContent();}finally{scale=layoutScale;}
  }
  void DrawMatchedPanelContent(){
   if(panel=="时间组"&&Event.current.type==EventType.Layout)CaptureTimingGroups();
   if(panel=="时序"&&Event.current.type==EventType.Layout)CaptureTimingEditor();
   if(panel=="检查"||panel=="工程"||panel=="帮助"||(panel=="工具"&&toolsLegacy)||(panel=="参数高级")||(panel=="设置"&&(settingsTab=="相机参数"||settingsTab=="背景参数"||settingsTab=="操作参数"))){
    DrawLegacyMatched();return;
   }
   Fill(panelRect,Css("#0c202bfa"));Fill(new Rect(panelRect.x,panelRect.y,scale,panelRect.height),Css("#48636f"));
   GUI.BeginGroup(panelRect);try{
    string title=panel=="渲染"?"视频渲染":panel=="网格"?"网格与吸附":panel=="放置"?"放置默认值":panel=="波形"?"音频波形":panel=="参数"?"物件参数":panel=="编辑"?"编辑操作":panel=="工具"?"谱面工具":panel=="选取过滤"?"选中过滤":panel=="段落过滤"?"段落选中过滤":panel=="外置脚本"?"外置工具":panel=="快捷工具"?"快速工具":panel;
    UiText(new Rect(28*scale,27*scale,360*scale,39.390625f*scale),title,24,Css("#c9e9ee"));
    if(FlatButton(new Rect(398*scale,27*scale,32*scale,39.390625f*scale),"×",26,false,false))Open(panel);
    Fill(new Rect(28*scale,87.390625f*scale,405*scale,scale),Css("#35515c"));
    float top=88.390625f*scale;
    if(panel=="设置"){
     string[] tabs={"显示","音频","相机","操作"};for(int i=0;i<4;i++){string tab=tabs[i];Rect r=new Rect((28+i*105)*scale,top+24*scale,83*scale,28*scale);if(FlatButton(r,tab,15,(settingsTab==tab||(tab=="操作"&&(settingsTab=="热键"||settingsTab=="游玩模式"))),false))Queue(delegate{settingsTab=tab;panelScroll=Vector2.zero;});if((settingsTab==tab||(tab=="操作"&&(settingsTab=="热键"||settingsTab=="游玩模式"))))Fill(new Rect(r.x,r.yMax+6*scale,r.width,2*scale),Css("#b4efef"));}
     top+=80*scale;
    }
    Rect area=new Rect(28*scale,top,417*scale,panelRect.height-top-27*scale);
    float expected=MatchedContentHeight();if(panel=="时序"||panel=="时间组")PrepareTimingViewport(area);
    panelScroll=GUI.BeginScrollView(area,panelScroll,new Rect(0,0,405*scale,Mathf.Max(area.height,expected*scale)),false,false);
    try{contentY=0;if(panel=="时间组")DrawTimingGroupsViewport();else if(panel=="时序")DrawTimingEditorViewport();else if(panel=="网格")MatchedGrid();else if(panel=="组浏览")MatchedGroups();else if(panel=="设置")MatchedSettings();else if(panel=="放置")MatchedPlacement();else if(panel=="参数")MatchedNotes();else if(panel=="编辑")MatchedEditing();else if(panel=="波形")MatchedWave();else if(panel=="渲染")MatchedRender();else if(panel=="工具")MatchedTools();else if(panel=="快捷工具")MatchedQuickTools();else if(panel=="外置脚本")MatchedRain();else if(panel=="选取过滤"||panel=="段落过滤")DrawSelectionFilters();}
    finally{GUI.EndScrollView();}
   }finally{GUI.EndGroup();}
  }
  float MatchedContentHeight(){
   if(panel=="时间组")return timingHistory==null?260:69*timingRows.Length+530+(frameTimingName==""?0:58+318)+(timingErrors.Length>0?120:0);
   if(panel=="时序")return TimingEditorHeight();
   if(panel=="渲染")return 1410;
   if(panel=="网格")return 490;
   if(panel=="组浏览")return 330;
   if(panel=="放置")return 620;
   if(panel=="编辑")return 440;
   if(panel=="快捷工具")return quickToolsContentHeight;
   if(panel=="波形")return 142;
   if(panel=="工具")return 410;
   if(panel=="外置脚本")return externalContentHeight;
   if(panel=="选取过滤"||panel=="段落过滤")return 750;
   if(panel=="设置")return settingsTab=="显示"?displayContentHeight:settingsTab=="音频"?(audioDetails?950:748):settingsTab=="相机"?190:settingsTab=="热键"?hotkeyPageHeight:settingsTab=="游玩模式"?1590:operationContentHeight;
   var form=Get(boot,"guiNoteForm") as NoteProperties;if(form==null)return 95;
   return form.Kind==EventKind.SkyArea?1183:form.Kind==EventKind.Flick?685:520;
  }
  void MatchedGrid(){
   MatchedToggle("显示节拍网格",Val<bool>(boot,"showGrid"),delegate(bool value){Command("SetBeatGrid",value);});
   MatchedFlag("小节线","showMeasureLines");
   MatchedToggle("时间吸附",Val<bool>(boot,"showGrid"),delegate(bool value){Command("SetBeatGrid",value);});GridRangeFields();
  }
  void MatchedGroups(){
   MatchedField("浏览组号","field_current_sky_group",Get(boot,"currentSkyGroup").ToString(),delegate(string value){Call(boot,"SetCurrentSkyGroup",value);});
   Rect row=ContentRow(81);RowLine(row);if(FlatButton(new Rect(0,row.y+20*scale,195*scale,41*scale),"‹ 上一组"))Command("JumpWithinSkyGroup",-1);if(FlatButton(new Rect(210*scale,row.y+20*scale,195*scale,41*scale),"下一组 ›"))Command("JumpWithinSkyGroup",1);
   MatchedToggle("仅显示当前组",onlyCurrentGroup,delegate(bool value){onlyCurrentGroup=value;});
   UiParagraph(new Rect(0,contentY+14*scale,405*scale,80*scale),"浏览切换只用于查看物件；物件的 groupid 在物件参数中编辑。",13,Css("#7c9dad"));
  }
  void MatchedSettings(){
   if(settingsTab=="游玩模式"){DrawGameplaySettings();return;}
   if(settingsTab=="热键"){DrawHotkeySettings();return;}
   if(settingsTab=="显示"){
    MatchedFlag("显示物件","showNotes");MatchedFlag("裁去已播放物件","hidePast");MatchedFlag("空域地面投影","showAirShadows");
    MatchedToggle("统一流速",Val<bool>(boot,"linearTimeLayout"),delegate(bool value){Queue(delegate{Call(boot,"CancelGesture");Set(boot,"linearTimeLayout",value);});});MatchedSection("其他显示");
    MatchedLink("背景与窗口",delegate{Queue(delegate{settingsTab="背景参数";panelScroll=Vector2.zero;});});
    MatchedToggle("编辑时暂停钻石动画",InFalsusStudio.NativeVisual.NativeVisualPlugin.PauseCenterInEditor.Value,delegate(bool value){Queue(delegate{InFalsusStudio.NativeVisual.NativeVisualPlugin.PauseCenterInEditor.Value=value;});});
    MatchedFlag("连击数字","showCenterCombo");ReadabilityChoices();SideGuideChoice();HitEffectChoice();CharacterEffectChoices();TrackLineChoices();MatchedToggle("自动游玩光标",cursorVisibleConfig.Value,delegate(bool value){Queue(delegate{cursorVisibleConfig.Value=value;});});ProgressStyleChoice();AntialiasingChoice();MatchedToggle("显示帧率",frameRateConfig.Value,delegate(bool value){Queue(delegate{frameRate.Reset();frameRateConfig.Value=value;});});
    MatchedToggle("显示帧时间波动",frameDetailsConfig.Value,delegate(bool value){Queue(delegate{frameDetailsConfig.Value=value;framePacing.Reset();});});TargetFrameRateChoice();
    MatchedToggle("播放时分摊内存回收",InFalsusStudio.Performance.PlaybackGcBudget.Enabled.Value,delegate(bool value){Queue(delegate{InFalsusStudio.Performance.PlaybackGcBudget.Enabled.Value=value;});});
    Rect gcHelp=ContentRow(90);UiParagraph(new Rect(0,gcHelp.y+8*scale,405*scale,82*scale),"将自动内存回收分摊到更多帧；内存增长时自动加大预算。暂停或关闭选项后恢复默认。",13,Css("#7c9dad"));
    // Layout measures every row before Repaint; keep new options inside the scroll range.
    displayContentHeight=contentY/scale+12;
   }else if(settingsTab=="音频"){
    MatchedSlider("音乐音量",Transport.Volume,delegate(float v){Queue(delegate{Transport.Volume=v;});});
    object sounds=Get(boot,"keySounds");if(sounds==null)return;
    MatchedSection("打击音");MatchedToggle("启用打击音",Read<bool>(sounds,"Enabled"),delegate(bool v){Queue(delegate{Write(sounds,"Enabled",v);});});
    MatchedSlider("总体音量",Read<float>(sounds,"MasterVolume"),delegate(float v){Queue(delegate{Write(sounds,"MasterVolume",v);});});
    MatchedSlider("攻击音量",Read<float>(sounds,"ShotVolume"),delegate(float v){Queue(delegate{Write(sounds,"ShotVolume",v);});});
    MatchedSlider("持续音量",Read<float>(sounds,"HoldVolume"),delegate(float v){Queue(delegate{Write(sounds,"HoldVolume",v);});});
    MatchedSection("头音");Rect row=ContentRow(63);RowLine(row);UiText(new Rect(0,row.y,155*scale,row.height),"SkyArea 头音",17,Css("#a6c3d1"));
    int policy=Convert.ToInt32(Get(boot,"skyAttackPolicy"));for(int i=0;i<2;i++){int p=i+1;Rect r=new Rect((165+i*120)*scale,row.y+16*scale,112*scale,31*scale);if(FlatButton(r,i==0?"每组首段":"关闭头音",14,policy==p))Queue(delegate{Set(boot,"skyAttackPolicy",Enum.ToObject(Field("skyAttackPolicy").FieldType,p));Call(boot,"RebuildKeySoundPlan");});}
    MatchedToggle("Hold 头部攻击音",Val<bool>(boot,"groundHoldAttacks"),delegate(bool v){Queue(delegate{Set(boot,"groundHoldAttacks",v);Call(boot,"RebuildKeySoundPlan");});});
    MatchedLink(audioDetails?"收起音频诊断":"音频诊断",delegate{audioDetails=!audioDetails;});
    if(audioDetails){string diagnostic=Convert.ToString(Prop(sounds,"Diagnostic"));UiParagraph(new Rect(0,contentY+10*scale,405*scale,130*scale),diagnostic,13,Css("#7c9dad"));contentY+=140*scale;MatchedLink("复制诊断",delegate{GUIUtility.systemCopyBuffer=Convert.ToString(Call(boot,"RuntimeStabilityReport010"));});}
   }else if(settingsTab=="相机"){MatchedLink("打开相机参数",delegate{Queue(delegate{settingsTab="相机参数";});});MatchedLink("应用本体 1.07b 视图",delegate{Queue(delegate{ApplyTrackView(true);});});MatchedLink("恢复原图像校准视图",delegate{Queue(delegate{ApplyTrackView(false);});});}
   else{MatchedLink("内置游玩模式",delegate{Queue(delegate{settingsTab="游玩模式";panelScroll=Vector2.zero;});});MatchedLink("热键",delegate{Queue(delegate{settingsTab="热键";panelScroll=Vector2.zero;});});MatchedToggle("允许天空物件超界",AirOverflow.Enabled,SetAirOverflow);MatchedLink("滚轮与导航参数",delegate{Queue(delegate{settingsTab="操作参数";});});LanguageChoice();MatchedLink("采样性能（10秒）",delegate{Queue(delegate{InFalsusStudio.Performance.P1Profiler.Begin("manual-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"),Path.Combine(Path.GetDirectoryName(Application.dataPath),"VisualUIData","Diagnostics"),10);});});MatchedLink("操作帮助",delegate{Open("帮助");});MatchedLink("更新公告",delegate{Command("OpenReleaseNotes");});DrawBrandAbout();}
  }
  static T Read<T>(object target,string name){return (T)target.GetType().GetField(name,F).GetValue(target);}
  static void Write(object target,string name,object value){target.GetType().GetField(name,F).SetValue(target,value);}
  void MatchedPlacement(){
   MatchedField("空域细分","field_air_split",Prop(Get(boot,"airGrid"),"Division").ToString(),OriginalCommit("<DrawToolDrawer>b__296_8"),"1 / N");
   foreach(string name in new[]{"tapWidth","holdWidth"}){string field=name;MatchedField(name=="tapWidth"?"Tap 默认宽度":"Hold 默认宽度","field_nativeui_"+name,Get(boot,name).ToString(),delegate(string value){int n;if(!int.TryParse(value,out n)||n<1||n>4)throw new FormatException(T("宽度须为 1～4"));Set(boot,field,n);},"轨");}
   MatchedField("SkyArea 默认宽度","field_sky_new_width",Num(Val<double>(boot,"skyWidthPercent")),OriginalCommit("<DrawToolDrawer>b__296_7"),"%");
   MatchedEase("左缓动","skyLeftEase");MatchedEase("右缓动","skyRightEase");
   MatchedField("Flick 默认宽度","field_flick_new_width",Num(flickWidthPercent),delegate(string value){double n;if(!double.TryParse(value,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out n)||!InRange(n,.0001,100))throw new FormatException(T("宽度须大于 0 且不超过 100%"));flickWidthPercent=n;Call(boot,"ResetPointDraft");},"%");
   Rect row=ContentRow(63);RowLine(row);UiText(new Rect(0,row.y,155*scale,row.height),"Flick 默认方向",17,Css("#a6c3d1"));
   for(int i=0;i<2;i++){int direction=i==0?16:4;Rect r=new Rect((165+i*120)*scale,row.y+16*scale,112*scale,31*scale);if(FlatButton(r,i==0?"向左":"向右",14,Val<int>(boot,"lastFlickDirection")==direction))Queue(delegate{Set(boot,"lastFlickDirection",direction);string tool=Get(boot,"tool").ToString();if(tool=="FlickLeft"||tool=="FlickRight")Call(boot,"UseTool",Enum.ToObject(Field("tool").FieldType,direction==16?4:5));});}
  }
  void MatchedEase(string title,string field){Rect row=ContentRow(63);RowLine(row);UiText(new Rect(0,row.y,120*scale,row.height),title,17,Css("#a6c3d1"));int old=Val<int>(boot,field);for(int i=0;i<3;i++){int next=i;Rect r=new Rect((140+i*87)*scale,row.y+16*scale,78*scale,31*scale);if(FlatButton(r,new[]{"Linear","SineOut","SineIn"}[i],13,old==i))Queue(delegate{Set(boot,field,next);});}}
  void MatchedNotes(){
   var form=Get(boot,"guiNoteForm") as NoteProperties;var values=Get(boot,"batchValues") as Dictionary<string,string>;if(form==null||values==null){UiParagraph(new Rect(0,10*scale,405*scale,70*scale),"选中物件后显示参数。Ctrl 多选，Alt 调整手柄。",17,Css("#a6c3d1"));return;}
   bool previous=GUI.enabled;GUI.enabled=previous&&!Transport.Playing;
   try{
    MatchedSection("时间");NoteField(values,"start","开始时间","ms");if(form.Kind==EventKind.Hold||form.Kind==EventKind.SkyArea)NoteField(values,"end","结束时间","ms");
    if(form.Kind==EventKind.SkyArea){MatchedSection("几何");NoteField(values,"sx","起点位置","%");NoteField(values,"ex","终点位置","%");NoteField(values,"sw","起点宽度","%");NoteField(values,"ew","终点宽度","%");NoteField(values,"bothWidths","统一宽度","%");NoteField(values,"leftEase","左缓动");NoteField(values,"rightEase","右缓动");MatchedSection("分组");NoteField(values,"group","Group ID");}
    else if(form.Kind==EventKind.Flick){NoteField(values,"x","中心位置","%");NoteField(values,"width","宽度","%");NoteField(values,"direction","方向");}
    else{MatchedLink("轨道与宽度",delegate{Queue(delegate{advancedNote=true;});});}
    MatchedSection("操作");MatchedLink("复制",delegate{Command("CopySelection",false);});MatchedLink("删除",delegate{Command("DeleteSelected");});MatchedLink("原始语句与高级参数",delegate{Queue(delegate{advancedNote=true;});});
   }finally{GUI.enabled=previous;}
  }
  void NoteField(Dictionary<string,string> values,string key,string title,string unit=""){string value;values.TryGetValue(key,out value);MatchedField(title,"nativeui_note_"+key,DisplayPrecision.Coordinate(key,value??""),delegate(string next){Call(boot,"QueueBatchField",key,next);},unit,true);}
  void MatchedEditing(){MatchedLink("复制",delegate{Command("CopySelection",false);});MatchedLink("粘贴",delegate{Command("PasteSelection",false);});MatchedLink("复制副本",delegate{Command("PasteSelection",true);});MatchedLink("镜像",delegate{Command("MirrorSelection");});MatchedLink("删除选中物件",delegate{Command("DeleteSelected");});MatchedLink("段落选择",delegate{Command("BeginRangeSelection");});MatchedLink("对齐到网格",delegate{Command("AlignSelection");});}
  void MatchedWave(){MatchedFlag("显示波形","showDetailTimeline");MatchedSlider("音乐音量",Transport.Volume,delegate(float v){Queue(delegate{Transport.Volume=v;});});}
  void MatchedTools(){MatchedLink("相接 SkyArea 分组（仅未分组）",delegate{Command("AutoGroupAllSky");});MatchedLink("外置工具",delegate{Open("外置脚本");});MatchedLink("组浏览",delegate{Open("组浏览");});MatchedLink("分组与坐标修复",delegate{Queue(delegate{toolsLegacy=true;Set(boot,"authoringToolsOpen",true);});});UiParagraph(new Rect(0,contentY+14*scale,405*scale,130*scale),"仅为未指定 groupid（含旧 -1）的 SkyArea 分组；首尾 ±2ms 且横向接触。保留已有分组，不跨时间组连接。",13,Css("#7c9dad"));}
  void MatchedLoop(){GUILayout.BeginArea(new Rect(0,0,405*scale,500*scale));try{Call(boot,"DrawLoopControls");}finally{GUILayout.EndArea();}}
  void DrawLegacyMatched(){
   DrawUnifiedLegacyPanel();
  }
  void DrawDockedFooter(){
   DockFill(footerRect,false);float y=footerRect.y;
   for(int strip=0;strip<46;strip++)Fill(new Rect(0,y+strip*2*scale,104*scale,2*scale),Color.Lerp(Css("#164550"),Css("#112f39"),strip/45f));Fill(new Rect(103*scale,y,scale,92*scale),Css("#426570"));
   if(IconButton(new Rect(0,y,104*scale,92*scale),"play","正式播放 · 隐藏编辑界面",false))RequestPlay(false);
   HistoryButton(new Rect(104*scale,y,60*scale,92*scale),false);
   HistoryButton(new Rect(164*scale,y,60*scale,92*scale),true);
   if(IconButton(new Rect(224*scale,y,60*scale,92*scale),"save","保存 · Ctrl + S",false))Command("SaveWorkingCopy",false);
   if(Transport==null||Profile==null)return;double bpm=Convert.ToDouble(Get(boot,"viewReferenceBpm"));if(bpm<=0)bpm=Convert.ToDouble(Prop(boot,"InitialBpm"));
   MatchedFooterCell(309,"bpm","基准 BPM","field_chrome_reference",Num(bpm),OriginalCommit("<DrawExternalChrome>b__55_1"),75,"");
   MatchedFooterCell(529,"offset","音频偏移","field_chrome_delay",Num(Transport.AudioOffsetMs),OriginalCommit("<DrawExternalChrome>b__55_2"),96,"ms");
   MatchedFooterCell(719,"clock","当前时间","field_chrome_time",Math.Truncate((double)Prop(boot,"CurrentMs")).ToString("0",CultureInfo.InvariantCulture),OriginalCommit("<DrawExternalChrome>b__55_3"),128,"ms");
   MatchedFooterCell(969,"speed","视速","field_chrome_scroll",Num(Profile.scrollMultiplier),OriginalCommit("<DrawExternalChrome>b__55_0"),49,"");
   MatchedFooterCell(1174,"rate","播放速度","field_nativeui_rate",Num(Transport.Rate*100),delegate(string v){double n=double.Parse(v,CultureInfo.InvariantCulture);if(double.IsNaN(n)||double.IsInfinity(n)||n<10||n>400)throw new ArgumentOutOfRangeException("播放速度");Transport.SetRate(n/100);},56,"%");
   foreach(float x in new[]{296f,1161f,1396f})Fill(new Rect(x*scale,y+24*scale,scale,44*scale),Css("#355360"));
   FilterFooterButton(1409,"range","选中过滤","选取过滤",PickMask);FilterFooterButton(1495,"copy","段落选中过滤","段落过滤",RangeMask);
   if(IconButton(new Rect(1581*scale,y,131*scale,92*scale),"tools",RainL("快速工具","Quick tools"),panel=="快捷工具"))Open("快捷工具");
   Fill(new Rect(1712*scale,y+24*scale,scale,44*scale),Css("#355360"));
   MatchedFooterCell(1770,"grid","节拍细分","field_division07",Prop(boot,"Division").ToString(),delegate(string value){int n;if(!int.TryParse(value,out n))throw new FormatException(T("细分须为整数"));Call(boot,"SetSubdivision",n);},64,"1 / N",50);
   Fill(new Rect(Screen.width-100*scale,y+44*scale,4*scale,4*scale),Css("#7ccab7"));UiText(new Rect(Screen.width-85*scale,y+32*scale,75*scale,28*scale),(bool)Prop(boot,"HasUnsavedEdits")?"待保存":"已保存",13,Css("#7eabb8"));
  }
  void MatchedFooterCell(float x,string icon,string title,string key,string value,Action<string> commit,float inputWidth,string unit,float unitWidth=27){
   float y=footerRect.y;var previous=GUI.color;GUI.color=previous*Css("#77a5b9");PaintFooterIcon(new Rect((x+25)*scale,y+33*scale,27*scale,27*scale),icon);GUI.color=previous;
   UiText(new Rect((x+67)*scale,y+14*scale,170*scale,Mathf.Max(17,23*scale)),title,13,Css("#adc9d6"));
   Rect input=new Rect((x+67)*scale,y+38.59375f*scale,inputWidth*scale,35*scale);UiInput(input,key,value,commit,26,0,false);
   if(unit.Length>0)UiText(new Rect(input.xMax+6*scale,y+43*scale,Mathf.Max(22,unitWidth*scale),Mathf.Max(17,23*scale)),unit,13,Css("#9fbfce"),"Supreme");
  }
 }
}
