using InFalsusStudio.VideoRender;
// Pending render-only modules 1 / 2 / 3 / 6. No chart or transport mutations.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
namespace InFalsusStudio.NativeVisual {
public sealed partial class NativeVisualPlugin {
 internal static BepInEx.Configuration.ConfigEntry<bool> HudEnabled,CenterEnabled,ComboEnabled,PauseCenterInEditor,ApGauge;
 private void InitPresentation(){
  HudEnabled=Config.Bind("Visual","NativeHud",true,"Native score/song HUD; original input handlers retained.");
  ApGauge=Config.Bind("Display","NativeApGauge",false,"Show the native life/clear-state gauge during gameplay and previews. Gameplay uses live base gauge rules.");
  CenterEnabled=Config.Bind("Visual","NativeCenter",true,"Native dynamic central hexagon, expanding outlines and triangle dispersion.");
  PauseCenterInEditor=Config.Bind("Display","PauseDiamondWhileEditing",true,"Keep the diamond animation still while playback is paused; resume from the same phase when playing. Does not alter music, chart time or other effects.");
  ComboEnabled=Config.Bind("Visual","NativeCombo",true,"Native combo font; original combo timeline retained.");
  InitProgressStyle();
  NativePresentation.Load(Path.Combine(folder,"Presentation"));
  harmony.Patch(T("StudioBootstrap").GetMethod("DrawSongHud",F),transpiler:new HarmonyMethod(typeof(NativeHudControls).GetMethod("PreserveControls",F)));
  foreach(var row in new[]{
   new[]{"StudioBootstrap","LayoutHud",null,"Layout"},
   new[]{"StudioBootstrap","LayoutChrome",null,"Layout"},
   new[]{"StudioBootstrap","DrawSongHud","Hud","EndHud"},
   new[]{"StudioBootstrap","HR",null,"MapRect"},
   new[]{"StudioBootstrap","DrawIntegratedPlay","Play",null},
   new[]{"StudioBootstrap","InPlayButton","PlayContains",null},
   new[]{"StudioBootstrap","DrawSongProgress","Progress",null},
   new[]{"StudioBootstrap","HudBlocksInput",null,"BlockProgress"},
   new[]{"StudioBootstrap","LateUpdate",null,"Backdrop"},
   new[]{"FloorComboRenderer","Draw","Combo","ComboUv"}}){
   var method=T(row[0]).GetMethod(row[1],F);if(method==null)throw new Exception("Presentation adapter interface missing: "+row[0]+"."+row[1]);
   harmony.Patch(method,row[2]==null?null:new HarmonyMethod(typeof(PresentationHooks).GetMethod(row[2],F)),row[3]==null?null:new HarmonyMethod(typeof(PresentationHooks).GetMethod(row[3],F)));
  }
  foreach(var name in new[]{"DrawGround","DrawSky","DrawFlick"})harmony.Patch(T("NoteRenderer").GetMethod(name,F),new HarmonyMethod(typeof(PresentationHooks).GetMethod("BeginSelected",F)),new HarmonyMethod(typeof(PresentationHooks).GetMethod("EndSelected",F)));
  harmony.Patch(T("NoteRenderer").GetMethod("Edge",F),new HarmonyMethod(typeof(PresentationHooks).GetMethod("SelectedEdge",F)));
  Logger.LogInfo("Pending native presentation modules 1, 2, 3, 6 loaded.");
 }
}
#pragma warning disable 0649 // Assigned by Unity JsonUtility from layout.json.
[Serializable] internal sealed class PresentationLayout {
 public float[] score,scoreFrame,scoreLabel,digits,play,song,songBacking,jacket,jacketFrame,title,artist,difficulty,difficultyText,level,speed,speedValue,designer,progress,scoreAdvances,comboCrop,comboAdvances;
 public string titleFace,artistFace;public string[] fallbackFaces;public float centerWidth,centerFarOverlap,centerOpaqueBottom;
}
#pragma warning restore 0649
internal static class NativePresentation {
 internal static PresentationLayout L;internal static bool Ready,Scope;
 internal static Dictionary<string,Texture2D> Images=new Dictionary<string,Texture2D>();
 private static string folder;private static List<string> fonts=new List<string>();
 private static Material selectionMaterial;
 private static readonly Dictionary<Texture,Texture2D> selectedHoldTextures=new Dictionary<Texture,Texture2D>();
 private struct SelectedLine {internal Vector3 A,B;internal SelectedLine(Vector3 a,Vector3 b){A=a;B=b;}}
 sealed class OutlineBuffers {internal readonly List<Vector3> Vertices=new List<Vector3>();internal readonly List<int> Triangles=new List<int>();internal readonly List<Color> Colors=new List<Color>();internal readonly List<Vector2> Uvs=new List<Vector2>();}
 private static readonly Dictionary<object,OutlineBuffers> outlineBuffers=new Dictionary<object,OutlineBuffers>();
 private static readonly Dictionary<object,List<SelectedLine>> selectedLines=new Dictionary<object,List<SelectedLine>>();
 internal static void BeginNoteFrame(object notes){List<SelectedLine> lines;if(selectedLines.TryGetValue(notes,out lines))lines.Clear();}
 internal static void DisposeNotes(object notes){selectedLines.Remove(notes);outlineBuffers.Remove(notes);}
 internal static void SelectedOutlineLine(object notes,Vector3 a,Vector3 b){List<SelectedLine> lines;if(!selectedLines.TryGetValue(notes,out lines)){lines=new List<SelectedLine>();selectedLines.Add(notes,lines);}lines.Add(new SelectedLine(a,b));}
 internal static bool Hud{get{return Ready&&NativeVisualPlugin.Active&&NativeVisualPlugin.HudEnabled.Value;}}
 internal static object Value(object o,string n){var t=o.GetType();var p=t.GetProperty(n,NativeVisualPlugin.F);return p!=null?p.GetValue(o,null):NativeVisualPlugin.Get(o,n);}
 internal static void Load(string path){
  folder=path;L=JsonUtility.FromJson<PresentationLayout>(File.ReadAllText(Path.Combine(path,"layout.json")));
  var loadImage=typeof(ImageConversion).GetMethod("LoadImage",new[]{typeof(Texture2D),typeof(byte[]),typeof(bool)});
  foreach(var p in Directory.GetFiles(path,"*.png")){
  string imageName=Path.GetFileNameWithoutExtension(p);
  if(imageName.StartsWith("side-guide-",StringComparison.Ordinal)||imageName.StartsWith("key-mask-",StringComparison.Ordinal)||imageName.StartsWith("key-native-",StringComparison.Ordinal))continue;
// Combo pixels contain raw FastText buffer colors, not an sRGB image.
  // FastText.UpdateCommonParametersBuffer + SDFText2 pass these RGB values
  // directly to the fragment output. Avoid decoding them a second time.
  var tex=new Texture2D(2,2,TextureFormat.RGBA32,false,Path.GetFileNameWithoutExtension(p)=="combo-digits");if(!(bool)loadImage.Invoke(null,new object[]{tex,File.ReadAllBytes(p),false}))throw new Exception("Presentation texture failed: "+p);tex.name=Path.GetFileNameWithoutExtension(p);tex.wrapMode=TextureWrapMode.Clamp;tex.filterMode=FilterMode.Bilinear;Images.Add(tex.name,tex);}
  if(!Images.ContainsKey("clearstate-ap")||!Images.ContainsKey("clearstate-ap-backing"))throw new Exception("Native AP gauge textures missing");
  NativeComboBlend.Init(path);NativeHudFont.Init(path);CreateDifficultySkins();
  foreach(var p in Directory.GetFiles(Path.Combine(path,"Fonts"),"*.ttf")){if(GdiText.AddFontResourceEx(p,0x10,IntPtr.Zero)==0)throw new Exception("Native font registration failed: "+Path.GetFileName(p));fonts.Add(p);}
  L.titleFace=L.artistFace;L.title=new[]{1469.224609f,32.082115f,330f,20f};L.artist=new[]{1469.975067f,54.92781f,330f,20f};
  Ready=true;
 }
 // Use FBD's transparent contour for every difficulty, including the jacket stripe
 // and short label plate. A filled rectangle would cover the title/artist panel.
 internal static void CreateDifficultySkins(){foreach(string name in new[]{"min","evo","ult","fbd"})if(!Images.ContainsKey("difficulty-"+name))throw new Exception("Original native difficulty material missing: "+name);}
 internal static void RowText(Rect r,string text,string face,float size,Color color,float baseline,bool centered=false){
  if(string.IsNullOrEmpty(text))return;
  var glyphs=text.Select(c=>GdiText.Glyph(face,c,L.fallbackFaces)).ToArray();
  float natural=glyphs.Sum(g=>g.Advance),scale=Mathf.Min(size/72f,r.width/Mathf.Max(1,natural));
  int inkBottom=glyphs.Where(g=>g.HasInk).Select(g=>g.InkBottom).DefaultIfEmpty(0).Max();
  float x=r.x+(centered?(r.width-natural*scale)*.5f:0),y=baseline-inkBottom*scale;
  var old=GUI.color;GUI.color=color;
  foreach(var g in glyphs){GUI.DrawTexture(new Rect(x-8*scale,y,g.Texture.width*scale,96*scale),g.Texture,ScaleMode.StretchToFill,true);x+=g.Advance*scale;}
  GUI.color=old;
 }
 internal static Rect R(object boot,float[] a){
  float s=NativeVisualPlugin.N(boot,"hudScale");
  var camera=(Camera)Value(boot,"ViewCamera");
  if(!camera){var origin=(Vector2)NativeVisualPlugin.Get(boot,"hudOrigin");return new Rect(origin.x+a[0]*s,origin.y+a[1]*s,a[2]*s,a[3]*s);}
  var viewport=camera.pixelRect;float top=RenderClock.SurfaceHeight-viewport.yMax;
  float x=a[0]>=1000?viewport.xMax-(1920-a[0])*s:viewport.x+a[0]*s;
  return new Rect(x,top+a[1]*s,a[2]*s,a[3]*s);
 }
 internal static void Layout(object boot){NativeVisualPlugin.Set(boot,"hudScoreRect",R(boot,L.score));NativeVisualPlugin.Set(boot,"hudSongRect",R(boot,L.song));NativeVisualPlugin.Set(boot,"playButtonRect",R(boot,L.play));NativeVisualPlugin.Set(boot,"progressRect",NativeProgress.Hit(boot));}
 internal static void Image(Rect r,string name,float alpha=1){Texture2D tex;if(alpha>0&&Images.TryGetValue(name,out tex)){if(CaptureCanvas.Active)CaptureCanvas.Draw(r,tex,new Color(1,1,1,alpha));else{var old=GUI.color;GUI.color=new Color(old.r,old.g,old.b,old.a*alpha);GUI.DrawTexture(r,tex,ScaleMode.StretchToFill,true);GUI.color=old;}}}
 internal static Rect ApRect(object boot,float width,float height){
  var camera=(Camera)Value(boot,"ViewCamera");var viewport=camera?camera.pixelRect:new Rect(0,0,RenderClock.SurfaceWidth,RenderClock.SurfaceHeight);
  // CoreScene.Update uses a 3840x2160 logical UI, not the editor's 1920 HUD.
  float s=Mathf.Min(viewport.width/3840f,viewport.height/2160f);
  return new Rect(viewport.center.x-width*s*.5f,RenderClock.SurfaceHeight-viewport.yMax,width*s,height*s);
 }
 internal static int LastGaugeState=-1,LastGaugeValue;internal static bool GaugeVisible;
 internal static NativeGaugeTransition gaugeAnimation=new NativeGaugeTransition();internal static object gaugeOwner;
 internal static Rect GaugeIndicatorRect(object boot,int state){
  float width=state==5?712:state==4?359:state==0||state==3?348:178,height=state==5?36:state==4?39:21;
  var rect=ApRect(boot,width,height);float s=ApRect(boot,3840,2160).width/3840f;
  // Native anchor=1 for positive (right end at +15), anchor=0 for
  // negative (left end at -15), rather than centered/stretched images.
  if(state<4)rect.x+=((state>=2?15-width*.5f:-15+width*.5f))*s;
  return rect;
 }
 internal static void DrawLifeGauge(object boot){
  GaugeVisible=false;if(!Ready||!NativeVisualPlugin.Active||!NativeVisualPlugin.ApGauge.Value||(!CaptureCanvas.Active&&Event.current.type!=EventType.Repaint))return;
  bool manual=InFalsusStudio.Gameplay.GameplaySession.Active;int state=manual?InFalsusStudio.Gameplay.GameplaySession.Engine.GaugeState:5,value=manual?InFalsusStudio.Gameplay.GameplaySession.Engine.GaugeValue:15;
  object owner=manual?(object)InFalsusStudio.Gameplay.GameplaySession.Engine:null;if(!ReferenceEquals(owner,gaugeOwner)){gaugeOwner=owner;gaugeAnimation=new NativeGaugeTransition();}gaugeAnimation.Advance(state,RenderClock.Realtime);
  var old=CaptureCanvas.Active?Color.white:GUI.color;if(!CaptureCanvas.Active)GUI.color=Color.white;
  try{
   Image(ApRect(boot,752,109),"clearstate-ap-backing",gaugeAnimation.FullBacking*gaugeAnimation.FullBacking);Image(ApRect(boot,752,109),"clearstate-indicator-backing",gaugeAnimation.NormalBacking*gaugeAnimation.NormalBacking);
   for(int j=0;j<6;j++){float alpha=gaugeAnimation.Alpha[j];Image(GaugeIndicatorRect(boot,j),NativeGaugeTransition.Images[j],alpha*alpha);}
   for(int side=0;side<2;side++){bool positive=side==0;float opacity=positive?gaugeAnimation.PositiveNumber:gaugeAnimation.NegativeNumber;if(opacity<=0)continue;
    var origin=ApRect(boot,0,0);float scale=ApRect(boot,3840,2160).width/3840f;string number=value>0?"+"+value:value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    float size=30*scale,x=origin.x+(positive?-160:160)*scale;if(positive)x-=NativeHudFont.Width(number,"BaiJamjuree-Bold.ttf",size);
    float y=origin.y+(positive?43.1769104f:43.0895195f)*scale;
    Color color=positive?new Color(.6706599f,2.06057835f,2.00058198f,1):new Color(1.26415086f,.40288973f,.36374149f,1);color.a=opacity*opacity;
    NativeHudFont.Begin();try{NativeHudFont.Draw(number,"BaiJamjuree-Bold.ttf",size,x,y,color,color,false,new Rect(0,0,RenderClock.SurfaceWidth,RenderClock.SurfaceHeight));}finally{NativeHudFont.End();}
   }
   LastGaugeState=state;LastGaugeValue=value;GaugeVisible=true;
  }finally{if(!CaptureCanvas.Active)GUI.color=old;}
 }
 internal static void Fill(Rect r,Color color){if(CaptureCanvas.Active){CaptureCanvas.Draw(r,Texture2D.whiteTexture,color);return;}var old=GUI.color;GUI.color=color;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
 internal static void Line(Vector2 a,Vector2 b,Color color,float thickness){var matrix=GUI.matrix;GUIUtility.RotateAroundPivot(Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg,a);Fill(new Rect(a.x,a.y,(b-a).magnitude,thickness),color);GUI.matrix=matrix;}
 internal static void Text(Rect r,string text,string face,float size,Color color,bool centered=false){
  if(string.IsNullOrEmpty(text))return;var glyphs=text.Select(c=>GdiText.Glyph(face,c,L.fallbackFaces)).ToArray();float natural=glyphs.Sum(g=>g.Advance);float scale=Mathf.Min(size/72f,r.width/Mathf.Max(1,natural));float x=r.x+(centered?(r.width-natural*scale)*.5f:0),y=r.y+(r.height-96*scale)*.5f;
  var old=GUI.color;GUI.color=color;foreach(var g in glyphs){GUI.DrawTexture(new Rect(x-8*scale,y,g.Texture.width*scale,96*scale),g.Texture,ScaleMode.StretchToFill,true);x+=g.Advance*scale;}GUI.color=old;
 }
 // HUD label coordinates specify visible ink tops, not padded 96px glyph boxes.
 // Equal horizontal/vertical scaling retains the font's natural proportions.
 internal static void InkText(Rect r,string text,string face,float size,Color color,bool centered=false){
  if(string.IsNullOrEmpty(text))return;
  var glyphs=text.Select(c=>GdiText.Glyph(face,c,L.fallbackFaces)).ToArray();
  float natural=glyphs.Sum(g=>g.Advance),scale=Mathf.Min(size/72f,r.width/Mathf.Max(1,natural));
  int inkTop=glyphs.Where(g=>g.HasInk).Select(g=>g.InkTop).DefaultIfEmpty(0).Min();
  float x=r.x+(centered?(r.width-natural*scale)*.5f:0),y=r.y-inkTop*scale;
  var old=GUI.color;GUI.color=color;
  foreach(var g in glyphs){GUI.DrawTexture(new Rect(x-8*scale,y,g.Texture.width*scale,96*scale),g.Texture,ScaleMode.StretchToFill,true);x+=g.Advance*scale;}
  GUI.color=old;
 }
 private static string Score(object boot){
  if(InFalsusStudio.Gameplay.GameplaySession.Active)return Format(InFalsusStudio.Gameplay.GameplaySession.Engine.Score);
  string score=null;var timeline=NativeVisualPlugin.Get(boot,"comboTimeline");double now=NativeVisualPlugin.P(boot,"CurrentMs");
  if(timeline!=null&&(bool)Value(timeline,"Supported")){long current=Convert.ToInt64(Value(NativeVisualPlugin.Call(timeline,"At",now),"Total"));long total=Convert.ToInt64(Value(Value(timeline,"Totals"),"Total"));long value=(long)NativeVisualPlugin.T("Core.PreviewScore").GetMethod("At",NativeVisualPlugin.F).Invoke(null,new object[]{current,total});score=Format(value);}
  var evidence=NativeVisualPlugin.Get(boot,"scoreEvidence");if(evidence!=null&&(bool)NativeVisualPlugin.Get(boot,"evidenceMatches")){var observation=NativeVisualPlugin.Call(evidence,"At",now);if(observation!=null)score=Format(Convert.ToInt64(Value(observation,"Score")));}
  return score??"---'---'---";
 }
 private static string Format(long value){return (string)NativeVisualPlugin.T("Core.ScoreEvidence").GetMethod("Format",NativeVisualPlugin.F).Invoke(null,new object[]{value});}
 internal static void DrawHud(object boot){
  if(!(bool)NativeVisualPlugin.Get(boot,"guiShowSongHud"))return;NativeVisualPlugin.Call(boot,"EnsureHudAssets");var old=CaptureCanvas.Active?Color.white:GUI.color;if(!CaptureCanvas.Active)GUI.color=Color.white;
  try{
   Image(R(boot,L.scoreFrame),"persistent-ui-topleft-freeplay");Image(R(boot,L.song),"persistent-ui-topright");Image(R(boot,L.songBacking),"song-backing-clean");
   float s=NativeVisualPlugin.N(boot,"hudScale");
   int index=(int)NativeVisualPlugin.Get(boot,"guiSongIndex");var codes=(string[])NativeVisualPlugin.T("Core.SongFolderProject").GetField("Codes",NativeVisualPlugin.F).GetValue(null);string code=index<0?"—":codes[Mathf.Clamp(index,0,codes.Length-1)];
   if(index>=0)Image(R(boot,L.difficulty),"difficulty-"+code.ToLowerInvariant());
   var jacket=NativeVisualPlugin.Get(boot,"songJacket") as Texture2D;if(!jacket)jacket=Images["nojacket"];var jr=R(boot,L.jacket);var jf=R(boot,L.jacketFrame);Fill(jf,new Color(.06f,.10f,.13f,1));if(jacket){Fill(jf,new Color(1,1,1,.5019608f));if(CaptureCanvas.Active)CaptureCanvas.Draw(jr,jacket,Color.white,true);else GUI.DrawTexture(jr,jacket,ScaleMode.ScaleAndCrop,true);}DrawJacketBorder(jf,s);Image(jf,"cornerbrackets-xsmall-ingame-cards");
   DrawRestoredHud(boot,code,s);
  }finally{if(!CaptureCanvas.Active)GUI.color=old;}
 }
 // Border strips are painted after the cover instead of relying on its background ring.
 internal static void DrawJacketBorder(Rect frame,float scale){
  float x=Mathf.Round(frame.xMin),y=Mathf.Round(frame.yMin),right=Mathf.Round(frame.xMax),bottom=Mathf.Round(frame.yMax),w=Mathf.Max(1,Mathf.Round(scale));
  JacketBorderStrip(new Rect(x,y,right-x,w));JacketBorderStrip(new Rect(x,bottom-w,right-x,w));JacketBorderStrip(new Rect(x,y+w,w,bottom-y-2*w));JacketBorderStrip(new Rect(right-w,y+w,w,bottom-y-2*w));
 }
 static void JacketBorderStrip(Rect rect){Fill(rect,new Color(.06f,.10f,.13f,1));Fill(rect,new Color(1,1,1,.5019608f));}
 // Fixed-size metadata and independently clipped native shadow layers.
 static string restoredTitle;static float titleClock,titleLast;
 static double scoreTime=-1;static long scoreTarget,scoreFrom;static float scoreEpoch;
 static Vector2 HudPoint(object boot,float x,float y){var r=R(boot,new[]{x,y,1f,1f});return new Vector2(r.x,r.y);}
 static void HudText(object boot,string text,string face,float size,float x,float y,Color color,float scale,bool center=false){var p=HudPoint(boot,x,y);NativeHudFont.Draw(text,face,size*5*scale,p.x,p.y,color,color,false,new Rect(0,0,RenderClock.SurfaceWidth,RenderClock.SurfaceHeight),center);}
 internal static Vector2 TitleAnimation(float excess,float elapsed){if(excess<=30)return new Vector2(0,1);float d=excess/50,period=d+2.65f,t=Mathf.Repeat(elapsed,period);if(t<=.2f)return new Vector2(0,t/.2f);if(t<=.95f)return new Vector2(0,1);if(t<.95f+d)return new Vector2(-excess*(t-.95f)/d,1);if(t<d+2.45f)return new Vector2(-excess,1);return new Vector2(-excess,1-(t-d-2.45f)/.2f);}
 static void DrawRestoredHud(object boot,string code,float scale){
  float real=RenderClock.Realtime,delta=titleLast==0?0:Mathf.Max(0,real-titleLast);titleLast=real;
  string title=Convert.ToString(NativeVisualPlugin.Get(boot,"guiSongTitle")),artist=Convert.ToString(NativeVisualPlugin.Get(boot,"guiSongArtist"));
  if(restoredTitle!=title){restoredTitle=title;titleClock=0;}
  if(NativeVisualPlugin.Get(boot,"hudField").ToString()=="None")titleClock+=delta;
  NativeHudFont.Begin();try{
   string score=Score(boot);long target;double time=NativeVisualPlugin.P(boot,"CurrentMs");bool playing=RenderClock.Active||(bool)Value(NativeVisualPlugin.Get(boot,"transport"),"Playing");
   if(long.TryParse(score.Replace("'",""),out target)){
    float t=Mathf.Clamp01((real-scoreEpoch)/.5f);long value=(long)(scoreFrom+(scoreTarget-scoreFrom)*(double)t);
    if(scoreTime<0||!playing||time<scoreTime||Math.Abs(time-scoreTime)>500||target<scoreTarget){scoreFrom=scoreTarget=target;scoreEpoch=real;value=target;}
    else if(target!=scoreTarget){scoreFrom=scoreTarget;scoreTarget=target;scoreEpoch=real;}scoreTime=time;score=Format(value);
   }
   HudText(boot,"SCORE","Supreme-Bold.ttf",2.6f,32.478706f,33.540272f,new Color(.6901961f,.79215694f,.8352942f,1),scale);
   HudText(boot,score,"BaiJamjuree-Medium.ttf",12.5f,141.595978f,77.990379f,new Color(.3137255f,.38431376f,.41960788f,1),scale);
   var sp=HudPoint(boot,143.635498f,75.881805f);NativeHudFont.Draw(score,"BaiJamjuree-Medium.ttf",62.5f*scale,sp.x,sp.y,new Color(.46637076f,2.7551539f,2.9960787f,1),new Color(.96089071f,1.5992445f,1.7411011f,1),true,new Rect(0,0,RenderClock.SurfaceWidth,RenderClock.SurfaceHeight));
   float width=NativeHudFont.Width(title,"OT-PUDShinGoPr6N-Regular.ttf",35);var animation=TitleAnimation(Mathf.Max(0,width-620),titleClock);var tp=HudPoint(boot,1469.224609f,47.531334f);var cp=HudPoint(boot,1460.0065f,26.2972f);Rect clip=new Rect(cp.x,cp.y,330*scale,50*scale);
   NativeHudFont.Draw(title,"OT-PUDShinGoPr6N-Regular.ttf",17.5f*scale,tp.x+animation.x*.5f*scale-1*scale,tp.y+1*scale,new Color(.13207549f,.13207549f,.13207549f,.16f*animation.y),Color.clear,false,clip);
   NativeHudFont.Draw(title,"OT-PUDShinGoPr6N-Regular.ttf",17.5f*scale,tp.x+animation.x*.5f*scale,tp.y,new Color(.9960785f,.9960785f,.9960785f,animation.y),Color.clear,false,clip);
   HudText(boot,artist,"OT-PUDShinGoPr6N-Regular.ttf",2.75f,1468.975037f,68.217186f,new Color(.06603771f,.06603771f,.06603771f,.72416765f),scale);
   HudText(boot,artist,"OT-PUDShinGoPr6N-Regular.ttf",2.75f,1469.975067f,67.066482f,new Color(.74901962f,.74901962f,.74901962f,1),scale);
   var codeInk=NativeHudFont.InkBounds(code,"Supreme-Bold.ttf",10);var ratingInk=NativeHudFont.InkBounds(Convert.ToString(NativeVisualPlugin.Get(boot,"guiSongRating")),"BaiJamjuree-SemiBold.ttf",10);
   float codeBaseline=94.626495f+(ratingInk.x+ratingInk.y-codeInk.x-codeInk.y)*.5f;
   HudText(boot,code,"Supreme-Bold.ttf",2f,1462.088593f,codeBaseline,new Color(.9150943f,.9150943f,.9150943f,1),scale);
   HudText(boot,Convert.ToString(NativeVisualPlugin.Get(boot,"guiSongRating")),"BaiJamjuree-SemiBold.ttf",2,1500.812988f,94.626495f,new Color(.9150943f,.9150943f,.9150943f,1),scale,true);
   HudText(boot,"SPEED","Supreme-Regular.ttf",1.7f,1520.829925f,94.717720f,new Color(.45882356f,.51372552f,.56862748f,1),scale);
   HudText(boot,NativeVisualPlugin.N(NativeVisualPlugin.Get(boot,"Profile"),"scrollMultiplier").ToString("0.00",System.Globalization.CultureInfo.InvariantCulture),"BaiJamjuree-SemiBold.ttf",2,1551.68631f,95.354729f,new Color(.78823537f,.87450987f,.89019615f,.81353325f),scale);
  }finally{NativeHudFont.End();}
 }
 internal static void DrawPlay(object boot){
  var r=R(boot,L.play);var old=GUI.color;GUI.color=new Color(.055f,.115f,.145f,.96f);var fill=NativeVisualPlugin.Get(boot,"playNookFill") as Texture2D;
  if(!fill){fill=Resources.Load<Texture2D>("InFalsusStudio/Hud/PlayNookFill");NativeVisualPlugin.Set(boot,"playNookFill",fill);}if(fill)GUI.DrawTexture(r,fill,ScaleMode.StretchToFill,true);GUI.color=Color.white;
  float t=NativeVisualPlugin.N(boot,"hudScale");var points=new[]{new Vector2(r.x,r.y),new Vector2(r.x+r.width*.78f,r.y),new Vector2(r.xMax,r.yMax),new Vector2(r.x,r.yMax)};for(int i=0;i<4;i++)Line(points[i],points[(i+1)%4],new Color(.5f,.82f,.86f),t);
  bool playing=RenderClock.Active||(bool)Value(NativeVisualPlugin.Get(boot,"transport"),"Playing");var icon=new Rect(r.x+r.width*.29f,r.y+r.height*.22f,r.height*.5f,r.height*.5f);NativeVisualPlugin.Call(boot,"DrawSmallIcon",icon,playing?"pause":"play",new Color(.6f,.94f,.98f));GUI.color=old;
 }
 // Progress rendering belongs to the native visual module. Original seeking
 // and drag events continue through StudioBootstrap.DrawSongProgress.
 internal static void DrawProgress(object boot){
  if(NativeProgress.Embedded){NativeProgress.Draw(boot);return;}
  Rect rect=(Rect)NativeVisualPlugin.Get(boot,"progressRect");if(rect.width<=0||rect.height<=0)return;
  double length=Math.Max(1,Convert.ToDouble(NativeVisualPlugin.Get(boot,"durationMs"))),now=NativeVisualPlugin.P(boot,"CurrentMs");
  float ratio=(float)Math.Max(0,Math.Min(1,now/length)),unit=NativeVisualPlugin.N(boot,"hudScale")*.75f;
  Fill(rect,new Color(16/255f,46/255f,56/255f,235/255f));
  Fill(new Rect(rect.x,rect.y,rect.width*ratio,rect.height),new Color(97/255f,200/255f,211/255f));
  Fill(new Rect(rect.x,rect.yMax-unit,rect.width,unit),new Color(76/255f,119/255f,131/255f,94/255f));
  Fill(new Rect(Mathf.Clamp(rect.x+rect.width*ratio-1.5f*unit,rect.x,rect.xMax-3*unit),rect.y,3*unit,rect.height),new Color(196/255f,244/255f,244/255f));
  string text=(bool)Value(boot,"HasDocument")?now.ToString("0",System.Globalization.CultureInfo.InvariantCulture)+" / "+length.ToString("0",System.Globalization.CultureInfo.InvariantCulture)+" ms":"未打开谱面";
  // Dark ink outline preserves legibility over both cyan fill and dark track.
  float outline=Mathf.Max(1,1.25f*unit);
  for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)if(x!=0||y!=0)ProgressText(new Rect(rect.x+x*outline,rect.y+y*outline,rect.width,rect.height),text,14*unit,new Color(5/255f,21/255f,29/255f));
  ProgressText(rect,text,14*unit,new Color(239/255f,1,1));
 }
 internal static void ProgressText(Rect r,string text,float size,Color color){
  var glyphs=text.Select(c=>GdiText.Glyph("Bai Jamjuree Medium",c,L.fallbackFaces)).ToArray();
  float natural=glyphs.Sum(g=>g.Advance),scale=Mathf.Min(size/72f,r.width/Mathf.Max(1,natural));
  int top=glyphs.Where(g=>g.HasInk).Select(g=>g.InkTop).DefaultIfEmpty(0).Min(),bottom=glyphs.Where(g=>g.HasInk).Select(g=>g.InkBottom).DefaultIfEmpty(0).Max();
  float pen=r.x+(r.width-natural*scale)*.5f,y=r.center.y-(bottom-top)*scale*.5f-top*scale;var old=GUI.color;GUI.color=color;
  foreach(var glyph in glyphs){GUI.DrawTexture(new Rect(pen-8*scale,y,glyph.Texture.width*scale,96*scale),glyph.Texture);pen+=glyph.Advance*scale;}GUI.color=old;
 }
 internal static bool Contains(object boot,Vector2 point){var r=R(boot,L.play);if(!r.Contains(point))return false;float right=r.x+r.width*(.78f+.22f*(point.y-r.y)/r.height);return point.x<=right;}
 internal static Rect Map(object boot,float x,float y,float w,float h,Rect original){
  if(!Scope)return original;
  if(Mathf.Abs(x-1338)<.1f&&Mathf.Abs(y-11)<.1f)return R(boot,L.jacket);
  if(Mathf.Abs(x-1460)<.1f&&Mathf.Abs(y-19)<.1f)return NativeHudControls.TitleRect(R(boot,L.title),R(boot,L.artist),NativeVisualPlugin.N(boot,"hudScale"));
  if(Mathf.Abs(x-1460)<.1f&&Mathf.Abs(y-51)<.1f)return NativeHudControls.ArtistRect(R(boot,L.title),R(boot,L.artist),NativeVisualPlugin.N(boot,"hudScale"));
  if(Mathf.Abs(x-1483)<.1f&&(y==83||y==87))return R(boot,L.level);
  if(Mathf.Abs(x-1668)<.1f&&(y==86||y==87))return R(boot,L.designer);
  return original;
 }
  internal static void Backdrop(object boot){if(Ready){NativeCenter.Draw(boot);NativeComboBlend.View((Camera)Value(boot,"ViewCamera"));NativeKeyLabels.View((Camera)Value(boot,"ViewCamera"));}}
 internal static void RestoreSelectionTint(Slot slot){
  foreach(var pair in slot.SelectionColors)slot.Material.SetVector(pair.Key,pair.Value);
  foreach(var pair in slot.SelectionFloats)slot.Material.SetFloat(pair.Key,pair.Value);
  foreach(var pair in slot.SelectionTextures)slot.Material.SetTexture(pair.Key,pair.Value);
  slot.SelectionColors.Clear();slot.SelectionFloats.Clear();slot.SelectionTextures.Clear();
 }
 internal static Texture2D DimHoldTexture(Texture source){
  Texture2D dimmed;if(selectedHoldTextures.TryGetValue(source,out dimmed)&&dimmed)return dimmed;
  var previous=RenderTexture.active;var temporary=RenderTexture.GetTemporary(source.width,source.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
  try{
   Graphics.Blit(source,temporary);RenderTexture.active=temporary;
   dimmed=new Texture2D(source.width,source.height,TextureFormat.RGBA32,true,true);
   dimmed.name="Selected hold "+source.name;dimmed.hideFlags=HideFlags.HideAndDontSave;
   dimmed.ReadPixels(new Rect(0,0,source.width,source.height),0,0,false);
   var pixels=dimmed.GetPixels32();for(int i=0;i<pixels.Length;i++){pixels[i].r=(byte)(pixels[i].r*.55f);pixels[i].g=(byte)(pixels[i].g*.55f);pixels[i].b=(byte)(pixels[i].b*.55f);}
   dimmed.SetPixels32(pixels);dimmed.Apply(true,true);dimmed.wrapMode=source.wrapMode;dimmed.filterMode=source.filterMode;dimmed.anisoLevel=source.anisoLevel;
   selectedHoldTextures[source]=dimmed;return dimmed;
  }catch{if(dimmed)UnityEngine.Object.Destroy(dimmed);throw;}
  finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(temporary);}
 }
 internal static void DimSelected(object notes,object note,Slot slot){
  if((bool)NativeVisualPlugin.Get(notes,"ghost")||!InFalsusStudio.Performance.RendererAccess.Selected((InFalsusStudio.NoteRenderer)notes,((InFalsusStudio.Core.SpcEvent)note).SourceId))return;
  if(((InFalsusStudio.Core.SpcEvent)note).Kind==InFalsusStudio.Core.EventKind.Hold){
   foreach(var key in new[]{"_NonHeldMap","_HeldMap","_HeldBoostedMap","_EmissionMap"})if(slot.Material.HasProperty(key)){
    var texture=slot.Material.GetTexture(key);if(texture){slot.SelectionTextures[key]=texture;slot.Material.SetTexture(key,DimHoldTexture(texture));}
   }
   return;
  }
  foreach(var key in new[]{"_BaseColor","_Color","_EdgeCenterColor","_EdgeGlowColor","_FrontBackColor","_EmissionColor"})if(slot.Material.HasProperty(key)){
   var color=slot.Material.GetVector(key);slot.SelectionColors[key]=color;color.x*=.55f;color.y*=.55f;color.z*=.55f;slot.Material.SetVector(key,color);
  }
  if(slot.SelectionColors.Count==0)foreach(var key in new[]{"_BaseAlpha","_Opacity"})if(slot.Material.HasProperty(key)){float value=slot.Material.GetFloat(key);slot.SelectionFloats[key]=value;slot.Material.SetFloat(key,value*.55f);}
 }
 internal static void SelectionOutlines(object notes){
  if(!NativeVisualPlugin.Active||(bool)NativeVisualPlugin.Get(notes,"ghost"))return;
  List<SelectedLine> lines;if(!selectedLines.TryGetValue(notes,out lines)||lines.Count==0)return;
  OutlineBuffers buffers;if(!outlineBuffers.TryGetValue(notes,out buffers))outlineBuffers[notes]=buffers=new OutlineBuffers();var vertices=buffers.Vertices;var triangles=buffers.Triangles;vertices.Clear();triangles.Clear();buffers.Colors.Clear();buffers.Uvs.Clear();
  var camera=(Camera)NativeVisualPlugin.Get(notes,"camera");var profile=NativeVisualPlugin.Get(notes,"p");float near=NativeVisualPlugin.N(profile,"stageNear"),far=NativeVisualPlugin.N(profile,"stageFar");float halfPixels=Mathf.Max(1.2f,1.85f*camera.pixelHeight/1080f);
  foreach(var line in lines){
   var a=line.A;var b=line.B;if((a.z<near&&b.z<near)||(a.z>far&&b.z>far))continue;
   if(a.z<near)a=Vector3.Lerp(a,b,(near-a.z)/(b.z-a.z));if(a.z>far)a=Vector3.Lerp(a,b,(far-a.z)/(b.z-a.z));if(b.z<near)b=Vector3.Lerp(b,a,(near-b.z)/(a.z-b.z));if(b.z>far)b=Vector3.Lerp(b,a,(far-b.z)/(a.z-b.z));
   var sa=camera.WorldToScreenPoint(a);var sb=camera.WorldToScreenPoint(b);if(sa.z<=camera.nearClipPlane||sb.z<=camera.nearClipPlane)continue;var delta=new Vector2(sb.x-sa.x,sb.y-sa.y);if(delta.sqrMagnitude<.01f)continue;var normal=new Vector2(-delta.y,delta.x).normalized*halfPixels;int start=vertices.Count;
   vertices.Add(camera.ScreenToWorldPoint(new Vector3(sa.x+normal.x,sa.y+normal.y,sa.z)));vertices.Add(camera.ScreenToWorldPoint(new Vector3(sb.x+normal.x,sb.y+normal.y,sb.z)));vertices.Add(camera.ScreenToWorldPoint(new Vector3(sb.x-normal.x,sb.y-normal.y,sb.z)));vertices.Add(camera.ScreenToWorldPoint(new Vector3(sa.x-normal.x,sa.y-normal.y,sa.z)));triangles.Add(start);triangles.Add(start+1);triangles.Add(start+2);triangles.Add(start);triangles.Add(start+2);triangles.Add(start+3);
  }
  if(triangles.Count==0)return;
  if(!selectionMaterial){selectionMaterial=new Material(Shader.Find("UI/Default"));selectionMaterial.name="Selected bright orange outline";selectionMaterial.mainTexture=Texture2D.whiteTexture;selectionMaterial.SetColor("_Color",PresentationHooks.SelectedColor);selectionMaterial.SetFloat("unity_GUIZTestMode",(float)CompareFunction.Always);selectionMaterial.renderQueue=3100;}
  var slot=NativeVisualPlugin.Visuals(notes).Take("selectionoutline",selectionMaterial);slot.Mesh.Clear();slot.Mesh.SetVertices(vertices);slot.Mesh.SetTriangles(triangles,0);for(int i=0;i<vertices.Count;i++){buffers.Colors.Add(Color.white);buffers.Uvs.Add(Vector2.zero);}slot.Mesh.SetColors(buffers.Colors);slot.Mesh.SetUVs(0,buffers.Uvs);slot.Mesh.RecalculateBounds();
 }
 internal static void Dispose(){NativeHudFont.Dispose();NativeComboBlend.Dispose();Ready=false;selectedLines.Clear();outlineBuffers.Clear();foreach(var t in selectedHoldTextures.Values)if(t)UnityEngine.Object.Destroy(t);selectedHoldTextures.Clear();foreach(var p in fonts)GdiText.RemoveFontResourceEx(p,0x10,IntPtr.Zero);fonts.Clear();GdiText.Dispose();foreach(var t in Images.Values)UnityEngine.Object.Destroy(t);Images.Clear();NativeCenter.Dispose();if(selectionMaterial)UnityEngine.Object.Destroy(selectionMaterial);}
}
internal static class PresentationHooks {
 internal static readonly Color SelectedColor=new Color(1f,.55f,.035f,1f);
 private static readonly HashSet<object> selecting=new HashSet<object>();
 private static Shader comboOriginalShader;
 internal static void BeginSelected(object __instance,object __0,ref bool __state){__state=selecting.Contains(__instance);if(NativeVisualPlugin.Active&&InFalsusStudio.Performance.RendererAccess.Selected((NoteRenderer)__instance,((InFalsusStudio.Core.SpcEvent)__0).SourceId))selecting.Add(__instance);}
 internal static void EndSelected(object __instance,bool __state){if(!__state)selecting.Remove(__instance);}
 internal static void SelectedEdge(object __instance,ref Color __2){if(selecting.Contains(__instance))__2=SelectedColor;}
 internal static void Layout(object __instance){if(NativePresentation.Hud)NativePresentation.Layout(__instance);}
 internal static bool Hud(object __instance){if(!NativePresentation.Hud)return true;NativePresentation.Scope=true;if(Event.current.type!=EventType.Repaint)return true;NativePresentation.DrawHud(__instance);return true;}
 internal static void EndHud(object __instance){NativePresentation.Scope=false;NativePresentation.DrawLifeGauge(__instance);}
 internal static void MapRect(object __instance,float __0,float __1,float __2,float __3,ref Rect __result){if(NativePresentation.Hud)__result=NativePresentation.Map(__instance,__0,__1,__2,__3,__result);}
 internal static bool Play(object __instance){if(!NativePresentation.Hud)return true;NativeVisualPlugin.Set(__instance,"playButtonRect",NativePresentation.R(__instance,NativePresentation.L.play));if(Event.current.type!=EventType.Repaint)return true;NativeVisualPlugin.Set(__instance,"playControlId",GUIUtility.GetControlID("IfStudio.Play1".GetHashCode(),FocusType.Passive));NativePresentation.DrawPlay(__instance);return false;}
 internal static bool PlayContains(object __instance,Vector2 __0,ref bool __result){if(!NativePresentation.Hud)return true;__result=NativePresentation.Contains(__instance,__0);return false;}
 internal static bool Progress(object __instance){
  if(!NativePresentation.Hud)return true;
  InFalsusStudio.Performance.NativePointerMessages.RefreshGuiPointer(Event.current);
  NativeVisualPlugin.Set(__instance,"progressRect",NativeProgress.Hit(__instance));
  if(Event.current.type!=EventType.Repaint)return true;
  NativeVisualPlugin.Set(__instance,"progressControlId",GUIUtility.GetControlID("InFalsusStudio.Progress07".GetHashCode(),FocusType.Passive));
  NativePresentation.DrawProgress(__instance);return false;
 }
 internal static void BlockProgress(object __instance,Vector2 __0,ref bool __result){if(NativePresentation.Hud)__result=__result||NativeProgress.Hit(__instance).Contains(__0)||NativePresentation.Contains(__instance,__0);}
 internal static void Backdrop(object __instance){if(NativePresentation.Ready)NativePresentation.Backdrop(__instance);}
 internal static void Combo(object __instance,int __0,bool __1,float __2,ref bool __state){
  __state=false;if(!NativePresentation.Ready)return;var material=(Material)NativeVisualPlugin.Get(__instance,"material");bool active=NativeVisualPlugin.Active&&NativeVisualPlugin.ComboEnabled.Value;
  if(!comboOriginalShader)comboOriginalShader=material.shader;var shader=active?NativeComboBlend.Shader:comboOriginalShader;NativeComboBlend.Request(active&&__1&&__0>0);
  if(material.shader!=shader){material.shader=shader;material.renderQueue=2940;NativeVisualPlugin.Set(__instance,"previousCount",-1);}
  if(active)NativeComboBlend.Bind(material);
  Texture texture=active?NativePresentation.Images["combo-digits"]:Resources.Load<Texture2D>("InFalsusStudio/Hud/ComboDigits");if(material.GetTexture("_MainTex")!=texture){material.SetTexture("_MainTex",texture);NativeVisualPlugin.Set(__instance,"previousCount",-1);}
  __state=active&&__1&&__0>0&&((int)NativeVisualPlugin.Get(__instance,"previousCount")!=__0||NativeVisualPlugin.N(__instance,"previousHalfWidth")!=__2);
 }
 internal static void ComboUv(object __instance,int __0,bool __state){
  if(!__state)return;var mesh=(Mesh)NativeVisualPlugin.Get(__instance,"mesh");var uv=mesh.uv;string digits=__0.ToString(System.Globalization.CultureInfo.InvariantCulture);
  for(int j=0;j<digits.Length;j++){int digit=digits[j]-'0',column=digit%5;for(int k=0;k<4;k++){int i=j*4+k;float local=uv[i].x*5-column;uv[i].x=(column+Mathf.Lerp(NativePresentation.L.comboCrop[0],NativePresentation.L.comboCrop[1],local))/5;}}
  mesh.uv=uv;
  // The original layout gives every digit the same advance. Native text
  // uses the font's proportional advances (particularly narrow for 1).
  var v=mesh.vertices;float pixelScale=Mathf.Abs(v[1].x-v[0].x)/(256*(NativePresentation.L.comboCrop[1]-NativePresentation.L.comboCrop[0]));
  float total=digits.Sum(c=>NativePresentation.L.comboAdvances[c-'0'])*pixelScale;float fit=Mathf.Min(1,NativeVisualPlugin.N(__instance,"previousHalfWidth")*1.56f/Mathf.Max(.0001f,total));pixelScale*=fit;total*=fit;float pen=-total*.5f;
  for(int j=0;j<digits.Length;j++){float advance=NativePresentation.L.comboAdvances[digits[j]-'0']*pixelScale,center=pen+advance*.5f,oldCenter=(v[j*4].x+v[j*4+1].x)*.5f;for(int k=0;k<4;k++){int i=j*4+k;v[i].x=center+(v[i].x-oldCenter)*fit;}pen+=advance;}
  mesh.vertices=v;mesh.colors=Enumerable.Repeat(Color.white,v.Length).ToArray();mesh.RecalculateBounds();
 }
}
// Private process fonts + GDI grayscale coverage. No OS font installation.
internal static class GdiText {
 [StructLayout(LayoutKind.Sequential)]struct Size {internal int X,Y;}
 [StructLayout(LayoutKind.Sequential)]struct Header {internal uint Size;internal int Width,Height;internal ushort Planes,Bits;internal uint Compression,ImageSize;internal int XPels,YPels;internal uint Colors,Important;}
 [DllImport("gdi32.dll",CharSet=CharSet.Unicode)]internal static extern int AddFontResourceEx(string file,uint flags,IntPtr reserved);
 [DllImport("gdi32.dll",CharSet=CharSet.Unicode)]internal static extern bool RemoveFontResourceEx(string file,uint flags,IntPtr reserved);
 [DllImport("gdi32.dll")]static extern IntPtr CreateCompatibleDC(IntPtr hdc);
 [DllImport("gdi32.dll")]static extern bool DeleteDC(IntPtr hdc);
 [DllImport("gdi32.dll")]static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
 [DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr obj);
 [DllImport("gdi32.dll",CharSet=CharSet.Unicode)]static extern IntPtr CreateFont(int h,int w,int e,int o,int weight,uint italic,uint underline,uint strike,uint charset,uint output,uint clip,uint quality,uint pitch,string face);
 [DllImport("gdi32.dll",CharSet=CharSet.Unicode)]static extern bool GetTextExtentPoint32(IntPtr dc,string text,int count,out Size size);
 [DllImport("gdi32.dll",CharSet=CharSet.Unicode)]static extern uint GetGlyphIndices(IntPtr dc,string text,int count,[Out]ushort[] indices,uint flags);
 [DllImport("gdi32.dll")]static extern IntPtr CreateDIBSection(IntPtr dc,ref Header info,uint usage,out IntPtr bits,IntPtr section,uint offset);
 [DllImport("gdi32.dll")]static extern uint SetTextColor(IntPtr dc,uint color);
 [DllImport("gdi32.dll")]static extern int SetBkMode(IntPtr dc,int mode);
 [DllImport("gdi32.dll",CharSet=CharSet.Unicode)]static extern bool TextOut(IntPtr dc,int x,int y,string text,int count);
 [DllImport("gdi32.dll")]static extern bool GdiFlush();
 internal sealed class Letter {internal Texture2D Texture;internal float Advance;internal int InkTop,InkBottom;internal bool HasInk;}
 private static Dictionary<string,Letter> letters=new Dictionary<string,Letter>();
 internal static Letter Glyph(string face,char c,string[] fallback){
  string key=face+":"+c;Letter result;if(letters.TryGetValue(key,out result))return result;
  IntPtr dc=CreateCompatibleDC(IntPtr.Zero),font=IntPtr.Zero,bmp=IntPtr.Zero,oldFont=IntPtr.Zero,oldBmp=IntPtr.Zero;
  try{
   foreach(string choice in new[]{face}.Concat(fallback)){if(font!=IntPtr.Zero){SelectObject(dc,oldFont);DeleteObject(font);}font=CreateFont(-72,0,0,0,400,0,0,0,1,0,0,4,0,choice);if(font==IntPtr.Zero)throw new Exception("CreateFont failed");oldFont=SelectObject(dc,font);var indexes=new ushort[1];GetGlyphIndices(dc,c.ToString(),1,indexes,1);if(indexes[0]!=0xffff)break;}
   Size size;if(!GetTextExtentPoint32(dc,c.ToString(),1,out size))throw new Exception("Native glyph measurement failed");int width=Math.Max(16,size.X+16),height=96;var header=new Header{Size=40,Width=width,Height=-height,Planes=1,Bits=32,ImageSize=(uint)(width*height*4)};IntPtr bits;bmp=CreateDIBSection(dc,ref header,0,out bits,IntPtr.Zero,0);if(bmp==IntPtr.Zero)throw new Exception("Native glyph DIB failed");oldBmp=SelectObject(dc,bmp);var data=new byte[width*height*4];Marshal.Copy(data,0,bits,data.Length);SetBkMode(dc,1);SetTextColor(dc,0xffffff);TextOut(dc,8,0,c.ToString(),1);GdiFlush();Marshal.Copy(bits,data,0,data.Length);
   int inkTop=96,inkBottom=0;bool hasInk=false;
   for(int y=0;y<96;y++)for(int x=0;x<width;x++){int j=(y*width+x)*4;if(Math.Max(data[j],Math.Max(data[j+1],data[j+2]))>0){inkTop=Math.Min(inkTop,y);inkBottom=Math.Max(inkBottom,y+1);hasInk=true;}}
   for(int i=0;i<data.Length;i+=4){byte alpha=Math.Max(data[i],Math.Max(data[i+1],data[i+2]));data[i]=data[i+1]=data[i+2]=255;data[i+3]=alpha;}
   // DIB is top-down, Unity raw texture rows are bottom-up.
   var rgba=new byte[data.Length];int stride=width*4;for(int y=0;y<height;y++)Buffer.BlockCopy(data,y*stride,rgba,(height-1-y)*stride,stride);
   var tex=new Texture2D(width,height,TextureFormat.RGBA32,false);tex.LoadRawTextureData(rgba);tex.Apply();tex.wrapMode=TextureWrapMode.Clamp;tex.filterMode=FilterMode.Bilinear;result=new Letter{Texture=tex,Advance=size.X,InkTop=inkTop,InkBottom=inkBottom,HasInk=hasInk};letters.Add(key,result);return result;
  }finally{if(oldBmp!=IntPtr.Zero)SelectObject(dc,oldBmp);if(bmp!=IntPtr.Zero)DeleteObject(bmp);if(oldFont!=IntPtr.Zero)SelectObject(dc,oldFont);if(font!=IntPtr.Zero)DeleteObject(font);DeleteDC(dc);}
 }
 internal static void Dispose(){foreach(var g in letters.Values)UnityEngine.Object.Destroy(g.Texture);letters.Clear();}
}
}

namespace InFalsusStudio.NativeVisual {
internal static class NativeHudFont {
 internal sealed class Face {internal readonly List<Texture2D> Pages=new List<Texture2D>();internal readonly Dictionary<UnityEngine.TextCore.Glyph,Texture2D> Textures=new Dictionary<UnityEngine.TextCore.Glyph,Texture2D>();internal object Handle;internal readonly Dictionary<ulong,float> Pairs=new Dictionary<ulong,float>();internal Texture2D Atlas;internal List<UnityEngine.TextCore.GlyphRect> Free=new List<UnityEngine.TextCore.GlyphRect>(),Used=new List<UnityEngine.TextCore.GlyphRect>();internal Dictionary<uint,UnityEngine.TextCore.Glyph> Glyphs=new Dictionary<uint,UnityEngine.TextCore.Glyph>();}
 static readonly Dictionary<string,Face> faces=new Dictionary<string,Face>();
 static readonly Type engine=typeof(UnityEngine.TextCore.LowLevel.FontEngine);
 static readonly BindingFlags flags=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
 static MethodInfo load=engine.GetMethods(flags).First(m=>m.Name=="LoadFontFace"&&m.GetParameters().Length==4&&m.GetParameters()[0].ParameterType==typeof(string));
 static MethodInfo add=engine.GetMethods(flags).First(m=>m.Name=="TryAddGlyphToTexture"&&m.GetParameters().Length==9);
 static MethodInfo index=engine.GetMethods(flags).First(m=>m.Name=="TryGetGlyphWithUnicodeValue"&&m.GetParameters().Length==4);
 static MethodInfo kern=engine.GetMethods(flags).First(m=>m.Name=="GetGlyphPairAdjustmentRecord"&&m.GetParameters().Length==3);
 static AssetBundle shaderBundle;
 internal static Material Ink;
 internal static void Init(string root){var bundle=AssetBundle.LoadFromFile(System.IO.Path.Combine(root,"native-hud.bundle"));shaderBundle=bundle;if(!bundle)throw new Exception("Native HUD shader bundle missing");var shader=bundle.LoadAsset<Shader>("native/hud-sdf");if(!shader||shader.passCount!=6)throw new Exception("Native HUD shader passes invalid");Ink=new Material(shader);UnityEngine.TextCore.LowLevel.FontEngine.InitializeFontEngine();Root=root;}
 static RenderTexture target;static readonly List<RenderTexture> pyramid=new List<RenderTexture>();static RenderTexture previous;static bool previousSrgbWrite;
 internal static void Begin(){previousSrgbWrite=GL.sRGBWrite;GL.sRGBWrite=false;if(target&&(target.width!=RenderClock.SurfaceWidth||target.height!=RenderClock.SurfaceHeight)){UnityEngine.Object.Destroy(target);target=null;}if(!target){target=new RenderTexture(RenderClock.SurfaceWidth,RenderClock.SurfaceHeight,0,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);target.Create();}previous=RenderTexture.active;RenderTexture.active=target;GL.Clear(false,true,Color.clear);GL.PushMatrix();GL.LoadPixelMatrix(0,RenderClock.SurfaceWidth,RenderClock.SurfaceHeight,0);GL.modelview=Matrix4x4.identity;}
 internal static void End(){GL.PopMatrix();RenderTexture.active=previous;RenderTexture source=target;int levels=Mathf.Clamp((int)Mathf.Log(Mathf.Max(RenderClock.SurfaceWidth,RenderClock.SurfaceHeight)/2f,2)-2,1,6);for(int i=0;i<levels;i++){int w=Mathf.Max(1,source.width/2),h=Mathf.Max(1,source.height/2);var temp=RenderTexture.GetTemporary(w,h,0,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);temp.filterMode=FilterMode.Bilinear;temp.wrapMode=TextureWrapMode.Clamp;if(i==0){Ink.SetFloat("_Threshold",1);Graphics.Blit(source,temp,Ink,2);}else{var horizontal=RenderTexture.GetTemporary(w,h,0,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);Graphics.Blit(source,horizontal,Ink,4);Graphics.Blit(horizontal,temp,Ink,5);RenderTexture.ReleaseTemporary(horizontal);}pyramid.Add(temp);source=temp;}for(int i=pyramid.Count-2;i>=0;i--){var temp=RenderTexture.GetTemporary(pyramid[i].width,pyramid[i].height,0,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);Ink.SetTexture("_Bloom",source);Graphics.Blit(pyramid[i],temp,Ink,3);if(source!=pyramid[pyramid.Count-1])RenderTexture.ReleaseTemporary(source);source=temp;}Ink.SetTexture("_Bloom",source);Ink.SetFloat("_OutputGamma",CaptureCanvas.Active?1:0);RenderTexture.active=previous;GL.sRGBWrite=previous?previous.sRGB:QualitySettings.activeColorSpace==ColorSpace.Linear;GL.PushMatrix();GL.LoadPixelMatrix(0,RenderClock.SurfaceWidth,RenderClock.SurfaceHeight,0);GL.modelview=Matrix4x4.identity;Quad(new Rect(0,0,RenderClock.SurfaceWidth,RenderClock.SurfaceHeight),target,new Rect(0,0,1,1),1);GL.PopMatrix();RenderTexture.ReleaseTemporary(source);foreach(var p in pyramid)RenderTexture.ReleaseTemporary(p);pyramid.Clear();GL.sRGBWrite=previousSrgbWrite;}
 static void Quad(Rect r,Texture texture,Rect uv,int pass){Ink.SetTexture("_MainTex",texture);Ink.SetPass(pass);GL.Begin(GL.QUADS);GL.TexCoord2(uv.xMin,uv.yMax);GL.Vertex3(r.xMin,r.yMin,0);GL.TexCoord2(uv.xMax,uv.yMax);GL.Vertex3(r.xMax,r.yMin,0);GL.TexCoord2(uv.xMax,uv.yMin);GL.Vertex3(r.xMax,r.yMax,0);GL.TexCoord2(uv.xMin,uv.yMin);GL.Vertex3(r.xMin,r.yMax,0);GL.End();}
 static float Kerning(Face face,uint left,uint right){ulong key=((ulong)left<<32)|right;float value;if(face.Pairs.TryGetValue(key,out value))return value;var record=(UnityEngine.TextCore.LowLevel.GlyphPairAdjustmentRecord)kern.Invoke(null,new object[]{face.Handle,left,right});value=record.firstAdjustmentRecord.glyphValueRecord.xAdvance;face.Pairs[key]=value;return value;}
 internal static void Dispose(){foreach(var f in faces.Values){if(f.Atlas)UnityEngine.Object.Destroy(f.Atlas);foreach(var page in f.Pages)UnityEngine.Object.Destroy(page);}faces.Clear();if(target){target.Release();UnityEngine.Object.Destroy(target);target=null;}if(Ink)UnityEngine.Object.Destroy(Ink);Ink=null;if(shaderBundle){shaderBundle.Unload(false);shaderBundle=null;}}
 static string Root;
 static Face Get(string name){Face f;if(faces.TryGetValue(name,out f))return f;object[] args={System.IO.Path.Combine(Root,"Fonts",name),128f,0,null};object error=load.Invoke(null,args);if(Convert.ToInt32(error)!=0)throw new Exception("Native font load failed: "+name+" "+error);f=new Face{Handle=args[3],Atlas=new Texture2D(2048,2048,TextureFormat.Alpha8,false,true)};f.Atlas.name="Native HUD SDF "+name;f.Atlas.LoadRawTextureData(new byte[2048*2048]);f.Atlas.Apply(false,false);f.Atlas.wrapMode=TextureWrapMode.Clamp;f.Atlas.filterMode=FilterMode.Bilinear;f.Free.Add(new UnityEngine.TextCore.GlyphRect(0,0,2048,2048));faces.Add(name,f);return f;}
 static UnityEngine.TextCore.Glyph Glyph(Face f,uint c){UnityEngine.TextCore.Glyph g;if(f.Glyphs.TryGetValue(c,out g))return g;object[] query={f.Handle,c,UnityEngine.TextCore.LowLevel.GlyphLoadFlags.LOAD_NO_BITMAP,null};if(!(bool)index.Invoke(null,query))return null;g=(UnityEngine.TextCore.Glyph)query[3];object[] args={f.Handle,g.index,8,UnityEngine.TextCore.LowLevel.GlyphPackingMode.BestShortSideFit,f.Free,f.Used,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,f.Atlas,null};if(!(bool)add.Invoke(null,args)){f.Pages.Add(f.Atlas);f.Atlas=new Texture2D(2048,2048,TextureFormat.Alpha8,false,true);f.Atlas.LoadRawTextureData(new byte[2048*2048]);f.Atlas.Apply(false,false);f.Atlas.wrapMode=TextureWrapMode.Clamp;f.Atlas.filterMode=FilterMode.Bilinear;f.Free.Clear();f.Used.Clear();f.Free.Add(new UnityEngine.TextCore.GlyphRect(0,0,2048,2048));args[7]=f.Atlas;args[8]=null;if(!(bool)add.Invoke(null,args))throw new Exception("Native HUD glyph exceeds atlas page");}g=(UnityEngine.TextCore.Glyph)args[8];f.Atlas.Apply(false,false);f.Glyphs.Add(c,g);f.Textures[g]=f.Atlas;return g;}
 static Face Resolve(string name,uint c,out UnityEngine.TextCore.Glyph glyph){var f=Get(name);glyph=Glyph(f,c);if(glyph!=null)return f;foreach(var fallback in new[]{"OT-PUDShinGoPr6N-Regular.ttf","vivoSansSCVF-partial600.ttf","ChironHeiHK-N.ttf"}){f=Get(fallback);glyph=Glyph(f,c);if(glyph!=null)return f;}glyph=Glyph(f,0xfffd);return f;}
 internal static Vector2 InkBounds(string text,string name,float size){float top=float.PositiveInfinity,bottom=float.NegativeInfinity;foreach(char c in text??""){UnityEngine.TextCore.Glyph g;Resolve(name,c,out g);if(g==null||g.metrics.height<=0)continue;top=Mathf.Min(top,-g.metrics.horizontalBearingY*size/128f);bottom=Mathf.Max(bottom,(g.metrics.height-g.metrics.horizontalBearingY)*size/128f);}return float.IsInfinity(top)?Vector2.zero:new Vector2(top,bottom);}
 internal static float Width(string text,string name,float size){float width=0,left=float.PositiveInfinity,right=float.NegativeInfinity;Face previous=null;uint last=0;for(int i=0;i<text.Length;i++){uint c=text[i];if(char.IsHighSurrogate(text[i])&&i+1<text.Length&&char.IsLowSurrogate(text[i+1])){c=(uint)char.ConvertToUtf32(text,i);i++;}else if(char.IsSurrogate(text[i]))c=0xfffd;UnityEngine.TextCore.Glyph g;var f=Resolve(name,c,out g);if(g==null)continue;if(f==previous){width+=Kerning(f,last,g.index)*size/128f;}left=Mathf.Min(left,width+g.metrics.horizontalBearingX*size/128f);right=Mathf.Max(right,width+(g.metrics.horizontalBearingX+g.metrics.width)*size/128f);width+=g.metrics.horizontalAdvance*size/128f;previous=f;last=g.index;}return float.IsInfinity(left)?0:right-left;}
 internal static void Draw(string text,string name,float size,float x,float baseline,Color top,Color bottom,bool split,Rect clip,bool center=false){if(string.IsNullOrEmpty(text))return;if(center)x-=Width(text,name,size)*.5f;Face previous=null;uint last=0;float scale=size/128f;for(int i=0;i<text.Length;i++){uint c=text[i];if(char.IsHighSurrogate(text[i])&&i+1<text.Length&&char.IsLowSurrogate(text[i+1])){c=(uint)char.ConvertToUtf32(text,i);i++;}else if(char.IsSurrogate(text[i]))c=0xfffd;UnityEngine.TextCore.Glyph g;var f=Resolve(name,c,out g);if(g==null)continue;if(f==previous){x+=Kerning(f,last,g.index)*scale;}var m=g.metrics;// Keep the sampling quad inside this glyph's eight-texel packing gutter; the full gutter can touch adjacent glyphs.
Rect target=new Rect(x+(m.horizontalBearingX-2)*scale,baseline-(m.horizontalBearingY+2)*scale,(m.width+4)*scale,(m.height+4)*scale);var gr=g.glyphRect;Rect uv=new Rect((gr.x-2)/2048f,(gr.y-2)/2048f,(gr.width+4)/2048f,(gr.height+4)/2048f);Ink.SetVector("_GlyphBounds",new Vector4((gr.x+.5f)/2048f,(gr.y+.5f)/2048f,(gr.x+gr.width-.5f)/2048f,(gr.y+gr.height-.5f)/2048f));Ink.SetColor("_Top",top);Ink.SetColor("_Bottom",split?bottom:top);Ink.SetVector("_Baseline",new Vector4(baseline-size,baseline+size*.25f,size*1.25f,split?1:0));Ink.SetVector("_Clip",new Vector4(clip.x,clip.y,clip.xMax,clip.yMax));RenderTexture.active=NativeHudFont.target;Quad(target,f.Textures[g],uv,0);x+=m.horizontalAdvance*scale;previous=f;last=g.index;}}
}
}
