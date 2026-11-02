using System;
using System.IO;
using System.Reflection;
using System.Linq.Expressions;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using BepInEx;
using BepInEx.Unity.Mono;
using HarmonyLib;
using UnityEngine;
using InFalsusStudio;
using InFalsusStudio.Core;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin:MonoBehaviour {
 internal BepInEx.Configuration.ConfigFile Config;internal BepInEx.Logging.ManualLogSource Logger;
 internal void Initialize(BepInEx.Configuration.ConfigFile config,BepInEx.Logging.ManualLogSource logger){Config=config;Logger=logger;InitializeEngine();}

  internal static NativeEditorUIPlugin I; internal static readonly BindingFlags F=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
  Harmony harmony; internal StudioBootstrap boot; internal bool ready,formal,previewRequest,legacy; int keyboardDepth; float alpha=1;  internal string panel="",settingsTab="显示"; Vector2 panelScroll; bool advancedNote; GUISkin skin; Font textFont; GUIStyle label,caption,heading,button,selected,number; Texture2D normalTex,hoverTex,activeTex,fieldTex,boxTex; float skinScale=-1;
  internal Rect viewport,leftRect,paramRect,rightRect,footerRect,panelRect; internal float scale=1;
  static Dictionary<string,FieldInfo> fields=new Dictionary<string,FieldInfo>(); static Dictionary<string,MethodInfo> methods=new Dictionary<string,MethodInfo>();
  internal static FieldInfo Field(string n){FieldInfo r;if(!fields.TryGetValue(n,out r)){r=typeof(StudioBootstrap).GetField(n,F);if(r==null)throw new MissingFieldException(n);fields[n]=r;}return r;}
  static readonly Dictionary<string,Func<object,object>> fieldReaders=new Dictionary<string,Func<object,object>>(); static readonly Dictionary<string,Action<object,object>> fieldWriters=new Dictionary<string,Action<object,object>>();
  internal static object Get(object o,string n){Func<object,object> read;if(!fieldReaders.TryGetValue(n,out read)){var input=Expression.Parameter(typeof(object));read=Expression.Lambda<Func<object,object>>(Expression.Convert(Expression.Field(Expression.Convert(input,typeof(StudioBootstrap)),Field(n)),typeof(object)),input).Compile();fieldReaders.Add(n,read);}return read(o);} internal static T Val<T>(object o,string n){return (T)Get(o,n);}
  internal static void Set(object o,string n,object v){Action<object,object> write;if(!fieldWriters.TryGetValue(n,out write)){write=InFalsusStudio.Performance.CompatibleFieldWrites.Create(Field(n));fieldWriters.Add(n,write);}write(o,v);}
  internal static MethodInfo Method(string n){MethodInfo r;if(!methods.TryGetValue(n,out r)){r=typeof(StudioBootstrap).GetMethod(n,F);if(r==null)throw new MissingMethodException(n);methods[n]=r;}return r;}
  internal static object Call(object o,string n,params object[] args){try{return Method(n).Invoke(o,args);}catch(TargetInvocationException e){throw e.InnerException??e;}}
  internal static object Prop(object o,string n){return o.GetType().GetProperty(n,F).GetValue(o,null);} internal static string Num(double d){return d.ToString("0.###",CultureInfo.InvariantCulture);}
  internal StudioTransport Transport{get{return Val<StudioTransport>(boot,"transport");}} internal StudioProfile Profile{get{return Val<StudioProfile>(boot,"Profile");}}
  void InitializeEngine(){I=this;try{
   foreach(string n in new[]{"DrawExternalChrome","LayoutWorkspace","LayoutChrome","BeginGuiFrame","TogglePlayback07","HandleKeyboard","CommittedField","LiveRow","LiveChoices","DrawTimingBrowser","QueueGui","UseStudioSkin","AllowedCanvas","UseTool"})Method(n);
   foreach(string n in new[]{"guiShowUi","showUi","transport","Profile","tool","dockRect","topRect","bottomRect","notePanelRect","guiNoteForm","batchValues","currentSkyGroup"})Field(n);
   harmony=new Harmony("local.infalsusstudio.nativeeditorui");
   Patch("DrawExternalChrome","Chrome",null);Patch("LayoutWorkspace",null,"Layout");Patch("LayoutChrome",null,"LayoutChrome");Patch("BeginGuiFrame",null,"Frame");Patch("TogglePlayback07","BeforePlay","AfterPlay");Patch("HandleKeyboard","BeforeKeys","AfterKeys");Patch("UseStudioSkin",null,"Skin");Patch("OnGUI","BeforeGui","AfterGui");Patch("WorkspaceBlocksInput","BlockWorkspace",null);Patch("ChromeBlocks",null,"BlockChrome");Patch("get_NotePanelVisible","NoteVisible",null);Patch("get_HelpRect",null,"HelpRect");Patch("InPlayButton","PlayHit",null);Patch("Select",null,"Selected");
   Patch("DrawSongProgress","BeforeFormalProgress","AfterFormalProgress");Patch("HandleSceneEvent","FormalSceneEvent",null);
   Patch("DrawRadialOverlay","StyledRadial",null);Patch("AddRadial",null,"CompactRadialButton");
   foreach(string n in new[]{"DrawSideDock","DrawToolDrawer","DrawToolbar","DrawTimeline","DrawInspector","DrawNotePanel","DrawAuthoringTools","DrawChartCheck","DrawCalibration","DrawSongPanel","DrawHelp07"})Patch(n,"HideLegacy",null);
   foreach(string n in new[]{"DrawEditingOverlay","DrawWorkflowOverlay","DrawAuthoringOverlay","DrawPointPreview","DrawRadialOverlay"})Patch(n,"EditOverlay",null);
   harmony.Patch(typeof(NoteRenderer).GetMethod("Draw",F),new HarmonyMethod(typeof(Hooks).GetMethod("GroupView",F)){priority=Priority.First});
   InitInteraction();InitRefinements();InitTypography();InitLegacyPanels();InitQuickSkyTools();InitHudMetadataInput();InitFrameRate();InitSpacePreview();
   Patch("AutoGroupAllSky","GroupUngroupedOnly",null);InitPopupInput();InitAntialiasing();InitOperationPreferences();InitHotkeys();InitSkyNode();InitOverlapAndWidth();InitTimingViewport();InitAirOverflow();InitQuickPlaceGuides();InitRain();InitVideoRender();InFalsusStudio.Gameplay.GameplaySession.Install(harmony);ready=true;Logger.LogInfo("Native Editor Sidebar ready; original commands/fields retained; native visual files untouched.");
  }catch(Exception e){Fail(e);}}
  void Patch(string n,string pre,string post){harmony.Patch(Method(n),pre==null?null:new HarmonyMethod(typeof(Hooks).GetMethod(pre,F)){priority=Priority.Last},post==null?null:new HarmonyMethod(typeof(Hooks).GetMethod(post,F)){priority=Priority.Last});}
  internal void Fail(Exception e){ready=false;InFalsusStudio.Performance.NativePointerMessages.Dispose();if(harmony!=null)harmony.UnpatchSelf();Logger.LogError("Native editor UI disabled; original UI restored: "+e);}
  void Update(){if(!ready||videoBusy)return;TickPreferenceMemory();TickCrosshairMode();InFalsusStudio.Performance.NativePointerMessages.PassiveMotion=!InFalsusStudio.Gameplay.GameplaySession.Active&&boot!=null&&(Val<RadialMenuState>(boot,"radial").Pressed||Transport!=null&&Transport.Playing&&!formalScrubbing&&!pointerUiCaptured&&!Val<bool>(boot,"progressDragging"));InFalsusStudio.Performance.NativePointerMessages.Tick(Logger);TickFrameRate();TickRain();TickQuickTiming();TickSpacePreview();TickAntialiasing();TickFormalNavigation();if(formal&&!InFalsusStudio.Gameplay.GameplaySession.Active&&!formalScrubbing&&boot!=null&&Transport!=null&&!Transport.Playing)formal=false;alpha=Mathf.MoveTowards(alpha,formal?0:1,Time.unscaledDeltaTime/0.22f);}
  void OnDestroy(){CancelOffsetAudioExport();CancelVideoRender();InFalsusStudio.VideoRender.RenderClock.Dispose();InFalsusStudio.Gameplay.GameplaySession.Exit();InFalsusStudio.Performance.NativePointerMessages.Dispose();DisposeBrand();PlaybackPresentationClock.Dispose();TargetFrameRatePolicy.Dispose();CancelRain(false);if(spacePreview.Active)StopSpacePreview();if(harmony!=null)harmony.UnpatchSelf();InFalsusStudio.NativeVisual.AntialiasingSettings.Dispose();DisposeInputFonts();DisposeTypography();DisposeLayoutFixes();DisposeRadialPlates();foreach(var texture in icons.Values)if(texture)Destroy(texture);icons.Clear();if(I==this)I=null;}
  internal void Queue(Action a){Call(boot,"QueueGui",a);} internal void Command(string name,params object[] a){Queue(delegate{Call(boot,name,a);});}
  internal void RequestPlay(bool preview){Queue(delegate{if(spacePreview.Active)StopSpacePreview();previewRequest=preview;try{if(!preview&&Transport.Playing&&!formal){Transport.Pause();}Call(boot,"TogglePlayback07");}finally{previewRequest=false;}});}
  internal void Bind(StudioBootstrap b){boot=b;EnsureReleaseAnnouncement(b);}
  internal void FrameState(){if(boot==null)return;RestoreOperationPreferences();ResetInputFocus();bool ui=Val<bool>(boot,"showUi")&&!formal;Set(boot,"guiShowUi",ui); foreach(string n in new[]{"guiToolsExpanded","guiAuthoringToolsOpen","guiChartCheckOpen","guiShowInspector","guiShowCalibration","guiShowSongPanel","guiShowHelp"})Set(boot,n,false);
   Set(boot,"pickFilter",0);if(chineseConfig!=null)Set(boot,"chinese",chineseConfig.Value);LayoutState();RefreshPopupSelection();
   if(panel=="工具"&&!Val<bool>(boot,"authoringToolsOpen"))panel="";if(panel=="检查"&&!Val<bool>(boot,"chartCheckOpen"))panel="";if(panel=="工程"&&!Val<bool>(boot,"showSongPanel"))panel="";if(panel=="时序"&&!Val<bool>(boot,"showInspector"))panel="";if(panel=="帮助"&&!Val<bool>(boot,"showHelp"))panel="";
  }
  internal void LayoutState(){InFalsusStudio.TimeGroups.GroupRuntime.Bind(boot);LayoutDocked();
   Set(boot,"dockRect",leftRect);Set(boot,"topRect",Rect.zero);Set(boot,"bottomRect",formal?Rect.zero:footerRect);foreach(string n in new[]{"inspectorRect","calibrationRect","songPanelRect"})Set(boot,n,panelRect);LayoutWave();LayoutPopup();Set(boot,"notePanelRect",notePopupRect);
  }
  internal Rect R(float x,float y,float w,float h){return new Rect(viewport.x+x*scale,viewport.y+y*scale,w*scale,h*scale);}
  internal bool Blocked(Vector2 p){return !formal&&Val<bool>(boot,"showUi")&&(leftRect.Contains(p)||paramRect.Contains(p)||(notePopupOpen&&notePopupRect.Contains(p))||rightRect.Contains(p)||footerRect.Contains(p)||(InFalsusStudio.NativeVisual.NativePresentation.Hud&&InFalsusStudio.NativeVisual.NativeProgress.Hit(boot).Contains(p))||(!string.IsNullOrEmpty(panel)&&panelRect.Contains(p))||(Val<bool>(boot,"showDetailTimeline")&&Val<Rect>(boot,"detailRect").Contains(p)));}
  internal void Open(string name){Queue(delegate{bool close=panel==name;Call(boot,"CommitAllInputFieldsNow");panel=close?"":name;panelScroll=Vector2.zero;advancedNote=false;toolsLegacy=false; foreach(string n in new[]{"toolsExpanded","authoringToolsOpen","chartCheckOpen","showCalibration","showInspector","showSongPanel","showHelp"})Set(boot,n,false);
   if(!close){if(name=="工具")Set(boot,"authoringToolsOpen",true);if(name=="检查")Call(boot,"OpenChartCheck");if(name=="工程")Set(boot,"showSongPanel",true);if(name=="时序")Set(boot,"showInspector",true);if(name=="帮助")Set(boot,"showHelp",true);}});}
  internal void SetupSkin(){if(skin!=null&&Mathf.Abs(skinScale-scale)<.001f){GUI.skin=skin;return;} if(skin==null){skin=UnityEngine.Object.Instantiate(GUI.skin);textFont=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei UI","Microsoft YaHei","Segoe UI"},22);normalTex=Tex(new Color(.07f,.16f,.205f));hoverTex=Tex(new Color(.13f,.29f,.34f));activeTex=Tex(new Color(.16f,.34f,.395f));fieldTex=BoxTexture(new Color(.035f,.105f,.15f),new Color(.29f,.43f,.5f));boxTex=Tex(new Color(.055f,.13f,.175f,.98f));}
   skinScale=scale;skin.font=textFont;int size=Mathf.Max(12,Mathf.RoundToInt(17*scale));foreach(var s in new[]{skin.label,skin.button,skin.textField,skin.textArea,skin.toggle,skin.box}){s.font=textFont;s.fontSize=size;s.normal.textColor=new Color(.79f,.9f,.94f);s.hover.textColor=Color.white;s.active.textColor=new Color(.85f,1,1);s.focused.textColor=Color.white;s.wordWrap=true;s.margin=new RectOffset(3,3,Mathf.Max(2,(int)(5*scale)),Mathf.Max(2,(int)(5*scale)));}
   skin.label.padding=new RectOffset(0,0,2,2);skin.button.normal.background=normalTex;skin.button.hover.background=hoverTex;skin.button.active.background=activeTex;skin.button.onNormal.background=activeTex;skin.button.focused.background=hoverTex;skin.button.padding=new RectOffset(10,10,7,7);skin.button.fixedHeight=Mathf.Max(30,43*scale);skin.button.fixedWidth=0;skin.textField.normal.background=fieldTex;skin.textField.focused.background=fieldTex;skin.textField.border=new RectOffset(1,1,1,1);skin.textField.normal.textColor=new Color(.89f,.96f,1);skin.textField.wordWrap=false;skin.textField.padding=new RectOffset(9,9,5,5);skin.textArea.normal.background=fieldTex;skin.box.normal.background=boxTex;skin.box.padding=new RectOffset(Mathf.RoundToInt(18*scale),Mathf.RoundToInt(18*scale),Mathf.RoundToInt(18*scale),Mathf.RoundToInt(18*scale));skin.toggle.onNormal.textColor=new Color(.7f,1,1);skin.toggle.padding=new RectOffset(Mathf.Max(17,(int)(24*scale)),4,5,5);
   skin.horizontalSlider.normal.background=Tex(new Color(.22f,.35f,.42f));skin.horizontalSlider.fixedHeight=Mathf.Max(3,4*scale);skin.horizontalSlider.border=new RectOffset(0,0,0,0);skin.horizontalSliderThumb.normal.background=Tex(new Color(.73f,.94f,.96f));skin.horizontalSliderThumb.hover.background=skin.horizontalSliderThumb.normal.background;skin.horizontalSliderThumb.active.background=skin.horizontalSliderThumb.normal.background;skin.horizontalSliderThumb.fixedWidth=Mathf.Max(6,8*scale);skin.horizontalSliderThumb.fixedHeight=Mathf.Max(10,14*scale);SetupFineBars();skin.settings.cursorColor=Css("#c7faff");skin.settings.selectionColor=Css("#275b6e");GUI.skin=skin;label=new GUIStyle(skin.label){wordWrap=false,alignment=TextAnchor.MiddleLeft,fontSize=Mathf.Max(12,Mathf.RoundToInt(17*scale))};caption=new GUIStyle(label){fontSize=Mathf.Max(10,Mathf.RoundToInt(13*scale))};caption.normal.textColor=new Color(.48f,.66f,.74f);heading=new GUIStyle(label){fontSize=Mathf.Max(16,Mathf.RoundToInt(24*scale))};button=new GUIStyle(skin.button){fixedHeight=0,alignment=TextAnchor.MiddleCenter,fontSize=Mathf.Max(10,Mathf.RoundToInt(18*scale)),padding=new RectOffset(2,2,2,2)};selected=new GUIStyle(button);selected.normal.background=activeTex;selected.normal.textColor=new Color(.85f,1,1);number=new GUIStyle(skin.textField){fontSize=Mathf.Max(12,Mathf.RoundToInt(25*scale))};ApplyReadableStyles();
  }
  static Texture2D BoxTexture(Color fill,Color edge){var t=new Texture2D(3,3,TextureFormat.RGBA32,false);for(int y=0;y<3;y++)for(int x=0;x<3;x++)t.SetPixel(x,y,x==1&&y==1?fill:edge);t.Apply();t.filterMode=FilterMode.Point;t.hideFlags=HideFlags.HideAndDontSave;return t;}
  static Texture2D Tex(Color c){var t=new Texture2D(1,1,TextureFormat.RGBA32,false);t.SetPixel(0,0,c);t.Apply();t.hideFlags=HideFlags.HideAndDontSave;return t;}
  static void Fill(Rect r,Color c){if(I!=null&&I.timingPaintActive&&(Event.current.type!=EventType.Repaint||!I.TimingRectVisible(r)))return;var old=GUI.color;GUI.color=old*c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
  void FrameBox(Rect r){Fill(r,new Color(.045f,.105f,.145f,.96f));Border(r,new Color(.25f,.4f,.46f),Mathf.Max(1,scale));Fill(new Rect(r.x,r.y,38*scale,Mathf.Max(1,2*scale)),new Color(.66f,.95f,1));}
  void Border(Rect r,Color c,float t){if(timingPaintActive&&PaintTimingBorder(r,c,t))return;Fill(new Rect(r.x,r.y,r.width,t),c);Fill(new Rect(r.x,r.yMax-t,r.width,t),c);Fill(new Rect(r.x,r.y,t,r.height),c);Fill(new Rect(r.xMax-t,r.y,t,r.height),c);}
  void Text(float x,float y,float w,float h,string t,GUIStyle s){GUI.Label(R(x,y,w,h),t,s);} bool Btn(float x,float y,float w,float h,string t,bool active){return GUI.Button(R(x,y,w,h),t,active?selected:button);}
  internal void DrawChrome(){Call(boot,"LayoutChrome");if(!formal)Call(boot,"DrawIntegratedPlay");Call(boot,"DrawSongProgress");if(!Val<bool>(boot,"showUi")||alpha<=.001f)return;var oldSkin=GUI.skin;Color oldColor=GUI.color;bool oldEnable=GUI.enabled;
   try{LayoutState();SetupSkin();GUI.color=new Color(1,1,1,alpha);GUI.enabled=oldEnable&&!formal;DrawDockedRails();DrawDockedFooter();if(!string.IsNullOrEmpty(panel))DrawMatchedPanel();DrawNotePopup();DrawHover();DrawEndpointDragMode();}
   finally{GUI.skin=oldSkin;GUI.color=oldColor;GUI.enabled=oldEnable;}
  }
  Action<string> OriginalCommit(string name){return delegate(string v){Call(boot,name,v);};}
  string Edit(Rect rect,string key,string current,Action<string> commit,bool empty){return (string)Call(boot,"CommittedField",rect,key,current,commit,false,empty);}
  void SideField(float y,string labelText,string unit,string key,string current,Action<string> commit){Text(159,y,252,26,labelText,label);Rect r=R(159,y+31,254,44);Border(r,new Color(.3f,.43f,.5f),Mathf.Max(1,scale));Edit(r,key,current,commit,false);if(unit.Length>0)Text(365,y+39,42,23,unit,caption);}
  internal static partial class Hooks {
   static bool Active(StudioBootstrap b){if(I==null||!I.ready)return false;I.Bind(b);return true;}
   static void BeforeGui(StudioBootstrap __instance,bool __runOriginal){if(__runOriginal&&Active(__instance)){I.FrameState();}}
   static void AfterGui(StudioBootstrap __instance,bool __runOriginal){if(I!=null&&I.ready&&ReferenceEquals(I.boot,__instance))I.pointerUiCaptured=GUIUtility.hotControl!=0;if(__runOriginal&&Active(__instance)){I.FrameState();I.DrawPreviewPlacement();I.DrawFrameRate();}}
   static void Frame(StudioBootstrap __instance){if(Active(__instance))I.FrameState();}
   static void Layout(StudioBootstrap __instance){if(Active(__instance))I.LayoutState();}
   static void LayoutChrome(StudioBootstrap __instance){if(!Active(__instance))return;Set(__instance,"chromeRect",Rect.zero);Set(__instance,"difficultyRect",Rect.zero);}
   static void Skin(StudioBootstrap __instance){if(Active(__instance)){I.LayoutState();I.SetupSkin();}}
   static bool Chrome(StudioBootstrap __instance){if(!Active(__instance))return true;try{I.DrawChrome();return false;}catch(Exception e){if(e is ExitGUIException)throw;I.Fail(e);return true;}}
   static void GroupView(ref IList<SpcEvent> __0){
    if(I==null||!I.ready||I.formal||!I.onlyCurrentGroup||I.boot==null)return;
    int group=Val<int>(I.boot,"currentSkyGroup");var filtered=new List<SpcEvent>(__0.Count);
    var groupOf=typeof(SpcEvent).Assembly.GetType("InFalsusStudio.Core.SkyGroups").GetMethod("GroupOf",F);
    foreach(var note in __0)if(note.Kind!=EventKind.SkyArea||(int)groupOf.Invoke(null,new object[]{note})==group)filtered.Add(note);
    __0=filtered; // Temporary render list; never modifies the document or input handlers.
   }
   static bool HideLegacy(){return I==null||!I.ready||I.legacy;}
   static bool EditOverlay(){return I==null||!I.ready||!I.formal;}
   static bool NoteVisible(ref bool __result){if(I==null||!I.ready||I.legacy)return true;__result=false;return false;}
   static bool PlayHit(ref bool __result){if(I!=null&&I.ready&&I.formal){__result=false;return false;}return true;}
   static void Selected(StudioBootstrap __instance){if(Active(__instance)&&!I.formal){I.RefreshPopupSelection();I.notePopupOpen=I.popupIds.Length>0;}}
   static void HelpRect(ref Rect __result){if(I!=null&&I.ready&&I.legacy)__result=I.managedLegacy?I.legacyBodyRect:I.panelRect;}
   static bool BlockWorkspace(StudioBootstrap __instance,Vector2 __0,ref bool __result){if(!Active(__instance)||Val<bool>(__instance,"guiShowReleaseNotes"))return true;__result=I.Blocked(__0);return false;}
   static void BlockChrome(StudioBootstrap __instance,Vector2 __0,ref bool __result){if(Active(__instance))__result=__result||I.Blocked(__0);}
   static void BeforePlay(StudioBootstrap __instance,out bool __state){__state=false;if(Active(__instance))__state=I.Transport.Playing;}
   static void AfterPlay(StudioBootstrap __instance,bool __state){if(!Active(__instance))return;if(!I.Transport.Playing)I.formal=false;else if(!__state&&I.keyboardDepth==0&&!I.previewRequest)I.formal=true;}
   static bool BeforeKeys(StudioBootstrap __instance,Event __0,ref bool __1,out bool __state){__state=false;if(InFalsusStudio.Gameplay.GameplaySession.HandleEvent(__0))return false;if(!Active(__instance))return true;__1=__1||I.PopupOwnsTextFocus||I.NativeTextInputFocused;if(I.RouteHotkey(__0,__1))return false;if(I.SpaceKey(__0,__1))return false;if(I.formal){if(__0.type==EventType.KeyDown&&__0.keyCode==KeyCode.Escape){I.CancelFormalNavigation();I.Transport.Pause();I.formal=false;GUI.FocusControl(null);__0.Use();}return false;}if(InFalsusStudio.Performance.EndpointDragMode.HandleKey(__instance,__0,__1))return false;I.keyboardSelectAll=__0.type==EventType.KeyDown&&(__0.control||__0.command)&&__0.keyCode==KeyCode.A;I.keyboardDepth++;__state=true;return true;}
   static void AfterKeys(bool __state){if(__state&&I!=null){I.keyboardDepth--;I.keyboardSelectAll=false;}}
  }
 }
}
