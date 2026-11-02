using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using InFalsusStudio.Core;
using InFalsusStudio.TimeGroups;
using InFalsusStudio.NativeEditorUI;
using UnityEngine;
namespace InFalsusStudio.Performance {
 internal static class RulerTickPolicy {
  internal static string Percent(double value){return decimal.Round((decimal)value,2,MidpointRounding.AwayFromZero).ToString("0.##",CultureInfo.InvariantCulture);}
  internal static double Norm(int i,int division,decimal step,int last){return i==last?1:division>0?i/(double)division:(double)(i*step/100m);}
  internal static int Last(int division,decimal step){if(division>0)return division;int n=(int)decimal.Floor(100m/step);return n*step<100?n+1:n;}
  internal static int[] Select(int division,decimal step,double pixels,double scale,double low,double high,out int labels){
   int last=Last(division,step);double spacing=division>0?pixels/division:pixels*(double)step/100;
   int stride=Math.Max(1,(int)Math.Ceiling(Math.Max(4,6*scale)/Math.Max(.001,spacing)));
   labels=Math.Max(1,(int)Math.Ceiling(64*scale/Math.Max(.01,spacing)));
   var result=new SortedSet<int>();int first=Math.Max(0,(int)Math.Ceiling(low*(division>0?division:100/(double)step)-1e-8)),end=Math.Min(last,(int)Math.Floor(high*(division>0?division:100/(double)step)+1e-8));
   Action<int> add=i=>{if(i>=first&&i<=end)result.Add(i);};
   for(int i=((first+stride-1)/stride)*stride;i<=end;i+=stride)add(i);
   for(int i=((first+labels-1)/labels)*labels;i<=end;i+=labels)add(i);
   for(int major=0;major<=10;major++){double exact=major*(division>0?division:100/(double)step)/10;int i=(int)Math.Round(exact);if(Math.Abs(exact-i)<1e-7)add(i);}
   if(low<=0)add(0);if(high>=1)result.Add(last);return new List<int>(result).ToArray();
  }
 }
 internal struct HandleView:IEquatable<HandleView> {
  internal object History,Scroll,Profile;internal int Revision,Selected,Width,Height;internal double Now,Units;internal Matrix4x4 View,Projection;internal Rect Pixel;internal Vector4 Geometry,Ground;internal bool Hide;
  public bool Equals(HandleView b){return ReferenceEquals(History,b.History)&&ReferenceEquals(Scroll,b.Scroll)&&ReferenceEquals(Profile,b.Profile)&&Revision==b.Revision&&Selected==b.Selected&&Width==b.Width&&Height==b.Height&&Now==b.Now&&Units==b.Units&&View==b.View&&Projection==b.Projection&&Pixel==b.Pixel&&Geometry==b.Geometry&&Ground==b.Ground&&Hide==b.Hide;}
 }
 internal struct RulerKey:IEquatable<RulerKey> {
  internal double Time,Low,High;internal int Division,Skin;internal decimal Step;
  public bool Equals(RulerKey b){return Time==b.Time&&Low==b.Low&&High==b.High&&Division==b.Division&&Step==b.Step&&Skin==b.Skin;}
  public override bool Equals(object o){return o is RulerKey&&Equals((RulerKey)o);}public override int GetHashCode(){return Time.GetHashCode()^Low.GetHashCode()^High.GetHashCode()^Division^Step.GetHashCode()^Skin;}
 }
 internal sealed class RulerGeometry {internal Vector2 A,B;internal double Low,High;internal Vector2[] Ticks;internal Rect[] Labels;internal string[] Text;internal float Scale;internal Rect Clip;internal bool Valid;}
 internal sealed class HandleCache {internal HandleView View;internal bool Captured,Handles;internal readonly HashSet<int> Selection=new HashSet<int>();internal readonly Dictionary<RulerKey,RulerGeometry> Rulers=new Dictionary<RulerKey,RulerGeometry>();}
 internal static class HandleOptimization {
  internal static bool Enabled=true;internal static long HandleHits,HandleMisses,RulerHits,RulerMisses,DrawnTicks;
  static readonly ConditionalWeakTable<StudioBootstrap,HandleCache> caches=new ConditionalWeakTable<StudioBootstrap,HandleCache>();
  static readonly BindingFlags F=TimingGroups.F;static readonly Dictionary<string,MethodInfo> methods=new Dictionary<string,MethodInfo>();
  static MethodInfo M(string name){MethodInfo m;if(!methods.TryGetValue(name,out m)){m=typeof(StudioBootstrap).GetMethod(name,F);methods.Add(name,m);}return m;}
  static object Call(StudioBootstrap boot,string name,params object[] args){return M(name).Invoke(boot,args);}
  static object Get(StudioBootstrap boot,string name){return GroupRuntime.Get(boot,name);}
  static object Prop(StudioBootstrap boot,string name){return typeof(StudioBootstrap).GetProperty(name,F).GetValue(boot,null);}
  static HandleCache Cache(StudioBootstrap boot){return caches.GetValue(boot,b=>new HandleCache());}
  internal static void Invalidate(StudioBootstrap boot){HandleCache cache;if(caches.TryGetValue(boot,out cache)){cache.Captured=false;cache.Handles=false;cache.Rulers.Clear();}}
  static void Capture(StudioBootstrap boot){
   if(boot.History==null)return;var camera=(Camera)Prop(boot,"ViewCamera");if(!camera)return;var p=(StudioProfile)Get(boot,"Profile");
   var key=new HandleView{History=boot.History,Revision=boot.History.Revision,Selected=(int)Get(boot,"selectedId"),Now=boot.CurrentMs,Scroll=Prop(boot,"EffectiveScroll"),Profile=p,Units=p.UnitsPerMs,Width=Screen.width,Height=Screen.height,View=camera.worldToCameraMatrix,Projection=camera.projectionMatrix,Pixel=camera.pixelRect,Geometry=new Vector4(p.centralHalfWidth,p.skyHeight,p.sideRun,p.sideRise),Ground=new Vector4(p.tapDepth,p.tapWidthRatio,p.stageNear,p.stageFar),Hide=(bool)Get(boot,"hidePast")};
   var cache=Cache(boot);var selected=(HashSet<int>)Get(boot,"selection");if(!cache.Selection.SetEquals(selected)){cache.Selection.Clear();cache.Selection.UnionWith(selected);cache.Handles=false;}if(!cache.Captured||!cache.View.Equals(key)){cache.View=key;cache.Captured=true;cache.Handles=false;cache.Rulers.Clear();}
  }
  static bool BuildHandles(StudioBootstrap __instance,out bool __state){
   __state=false;if(!Enabled)return true;var cache=Cache(__instance);
   if(!(bool)Get(__instance,"showUi")||!(bool)Get(__instance,"showNotes")||!(bool)Prop(__instance,"EditHeld")||((StudioTransport)Get(__instance,"transport")).Playing||(bool)Prop(__instance,"WorkflowActive")){cache.Handles=false;return true;}
   if(!cache.Captured||Event.current==null||Event.current.type==EventType.Layout)Capture(__instance);
   if(cache.Handles){HandleHits++;return false;}HandleMisses++;__state=true;return true;
  }
  static void BuiltHandles(StudioBootstrap __instance,bool __state){if(__state)Cache(__instance).Handles=true;}
  static void Frame(StudioBootstrap __instance){if(Enabled)Capture(__instance);}
  static bool Bound(float p,float q,ref float lo,ref float hi){if(Mathf.Abs(p)<1e-7f)return q>=0;float v=q/p;if(p<0){if(v>hi)return false;if(v>lo)lo=v;}else{if(v<lo)return false;if(v<hi)hi=v;}return true;}
  static bool Finite(Vector3 p){return AuthoringSafety.Finite(p.x)&&AuthoringSafety.Finite(p.y);}
  static RulerGeometry BuildRuler(StudioBootstrap boot,double time,AirAuthoringGrid grid,HandleCache cache){
   var result=new RulerGeometry{Low=AirGridRange.Low,High=AirGridRange.High};var view=cache.View;var p=(StudioProfile)view.Profile;var camera=(Camera)Prop(boot,"ViewCamera");
   float z=(float)(FastScrollHooks.Compute((ScrollTimeline)view.Scroll,time,view.Now)*view.Units),y=p.skyHeight+.021f;
   var aa=camera.WorldToScreenPoint(new Vector3(-p.centralHalfWidth,y,z));var bb=camera.WorldToScreenPoint(new Vector3(p.centralHalfWidth,y,z));
   if(aa.z<=camera.nearClipPlane||bb.z<=camera.nearClipPlane||!Finite(aa)||!Finite(bb))return result;
   result.A=new Vector2(aa.x,view.Height-aa.y);result.B=new Vector2(bb.x,view.Height-bb.y);result.Clip=new Rect(view.Pixel.x,view.Height-view.Pixel.yMax,view.Pixel.width,view.Pixel.height);result.Scale=(float)Prop(boot,"OverlayScale");result.Valid=true;
   var delta=result.B-result.A;float lo=(float)result.Low,hi=(float)result.High;
   bool visible=Bound(-delta.x,result.A.x-result.Clip.xMin,ref lo,ref hi)&&Bound(delta.x,result.Clip.xMax-result.A.x,ref lo,ref hi)&&Bound(-delta.y,result.A.y-result.Clip.yMin,ref lo,ref hi)&&Bound(delta.y,result.Clip.yMax-result.A.y,ref lo,ref hi);
   var ticks=new List<Vector2>();var labels=new List<Rect>();var text=new List<string>();int stride;
   if(visible){var positions=new List<AirGridRange.Tick>();
    if(AirGridRange.Expanded)positions.AddRange(AirGridRange.VisibleTicks(grid,Math.Abs(delta.x),result.Scale,lo,hi));
    else {var chosen=RulerTickPolicy.Select(grid.Division,grid.StepPercent,Math.Abs(delta.x),result.Scale,lo,hi,out stride);int last=RulerTickPolicy.Last(grid.Division,grid.StepPercent);foreach(int i in chosen)positions.Add(new AirGridRange.Tick{Position=RulerTickPolicy.Norm(i,grid.Division,grid.StepPercent,last),Label=i%stride==0||i==last});}
    foreach(var position in positions){double norm=position.Position;var tick=Vector2.LerpUnclamped(result.A,result.B,(float)norm);if(!result.Clip.Contains(tick))continue;ticks.Add(tick);
    }
   }
   result.Ticks=ticks.ToArray();result.Labels=labels.ToArray();result.Text=text.ToArray();return result;
  }
  static bool Ruler(StudioBootstrap __instance,double __0,double __1,bool __2){
   if(!Enabled)return true;var cache=Cache(__instance);if(!cache.Captured||Event.current.type==EventType.Layout)Capture(__instance);if(!cache.Captured)return true;
   var grid=(AirAuthoringGrid)Get(__instance,"airGrid");var key=new RulerKey{Time=__0,Low=AirGridRange.Low,High=AirGridRange.High,Division=grid.Division,Step=grid.StepPercent,Skin=GUI.skin.GetInstanceID()};RulerGeometry g;
   if(!cache.Rulers.TryGetValue(key,out g)){if(cache.Rulers.Count>=24)cache.Rulers.Clear();g=BuildRuler(__instance,__0,grid,cache);cache.Rulers.Add(key,g);RulerMisses++;}else RulerHits++;
   if(!g.Valid)return false;float k=g.Scale;var color=new Color(1,.6f,.15f,.85f);
   if(Event.current.type==EventType.Repaint){Call(__instance,"ClippedGuiLine",Vector2.LerpUnclamped(g.A,g.B,(float)g.Low),Vector2.LerpUnclamped(g.A,g.B,(float)g.High),color,3*k);foreach(var tick in g.Ticks)Call(__instance,"ClippedGuiLine",tick+new Vector2(0,-7*k),tick+new Vector2(0,7*k),color,1.6f*k);DrawnTicks+=g.Ticks.Length;}
   if(__2){var active=Vector2.LerpUnclamped(g.A,g.B,(float)__1);if(Event.current.type==EventType.Repaint)Call(__instance,"ClippedGuiLine",active+new Vector2(0,-16*k),active+new Vector2(0,16*k),new Color(.35f,1,.65f,1),3*k);if(g.Clip.Contains(active))Call(__instance,"OverlayText",active+new Vector2(-90*k,-49*k),"X "+RulerTickPolicy.Percent(__1*100)+" / 100",180f,true,17f,TextAnchor.MiddleCenter);}
   return false;
  }
  struct StyleKey:IEquatable<StyleKey>{internal bool Box;internal int Pixels,Padding,Anchor;public bool Equals(StyleKey b){return Box==b.Box&&Pixels==b.Pixels&&Padding==b.Padding&&Anchor==b.Anchor;}public override bool Equals(object o){return o is StyleKey&&Equals((StyleKey)o);}public override int GetHashCode(){return Pixels^(Padding<<8)^(Anchor<<16)^(Box?1<<24:0);}}
  static readonly Dictionary<StyleKey,GUIStyle> styles=new Dictionary<StyleKey,GUIStyle>();static GUISkin lastSkin;
  static bool Style(StudioBootstrap __instance,bool __0,float __1,TextAnchor __2,ref GUIStyle __result){
   if(!Enabled)return true;var skin=GUI.skin;if(lastSkin!=skin){styles.Clear();lastSkin=skin;}float scale=(float)Prop(__instance,"OverlayScale");int pixels=Math.Max(1,Mathf.RoundToInt(__1*scale)),padding=Mathf.RoundToInt((__0?6:2)*scale);
   var key=new StyleKey{Box=__0,Pixels=pixels,Padding=padding,Anchor=(int)__2};GUIStyle value;
   if(!styles.TryGetValue(key,out value)){if(styles.Count>96)styles.Clear();value=new GUIStyle(__0?skin.box:skin.label){fontSize=pixels,alignment=__2,wordWrap=true,fixedWidth=0,fixedHeight=0,contentOffset=Vector2.zero,padding=new RectOffset(padding,padding,padding,padding),margin=new RectOffset()};value.normal.textColor=new Color(.86f,.96f,1,1);styles.Add(key,value);}__result=value;return false;
  }
  internal static void Install(Harmony h){h.Patch(M("BeginGuiFrame"),postfix:new HarmonyMethod(typeof(HandleOptimization).GetMethod("Frame",F)));h.Patch(M("BuildHandles"),new HarmonyMethod(typeof(HandleOptimization).GetMethod("BuildHandles",F)),new HarmonyMethod(typeof(HandleOptimization).GetMethod("BuiltHandles",F)));h.Patch(M("DrawAirRuler"),new HarmonyMethod(typeof(HandleOptimization).GetMethod("Ruler",F)));h.Patch(M("OverlayStyle"),new HarmonyMethod(typeof(HandleOptimization).GetMethod("Style",F)));}
 }
}
