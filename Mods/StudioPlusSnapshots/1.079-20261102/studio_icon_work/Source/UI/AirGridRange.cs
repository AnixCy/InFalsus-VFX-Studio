using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using InFalsusStudio;
using InFalsusStudio.Core;
namespace InFalsusStudio.NativeEditorUI {
 internal static class AirGridRange {
  internal static ConfigEntry<double> Minimum,Maximum;
  internal static bool Active {get{return AirOverflow.Enabled&&Minimum!=null&&Valid(Minimum.Value,Maximum.Value);}}
  internal static double Low {get{return Active?Minimum.Value/100:0;}}
  internal static double High {get{return Active?Maximum.Value/100:1;}}
  internal static bool Expanded {get{return Low!=0||High!=1;}}
  internal static bool Valid(double lo,double hi){return AuthoringSafety.Finite(lo)&&AuthoringSafety.Finite(hi)&&lo>=-10000&&hi<=10000&&hi>lo&&hi-lo>=.0001;}
  internal static void Init(ConfigFile config,Harmony h){
   Minimum=config.Bind("Authoring","AirGridMinPercent",0d,"Air X ruler/snap minimum percent when air overflow is enabled. Minimum < Maximum, within -10000..10000. Does not move existing notes.");
   Maximum=config.Bind("Authoring","AirGridMaxPercent",100d,"Air X ruler/snap maximum percent when air overflow is enabled. Existing 0..100 is retained by default.");
   if(!Valid(Minimum.Value,Maximum.Value)){Minimum.Value=0;Maximum.Value=100;}
   h.Patch(typeof(AirAuthoringGrid).GetMethod("Snap"),new HarmonyMethod(typeof(AirGridRange).GetMethod("Snap",NativeEditorUIPlugin.F)));
   h.Patch(NativeEditorUIPlugin.Method("XOnRuler"),new HarmonyMethod(typeof(AirGridRange).GetMethod("Pointer",NativeEditorUIPlugin.F)));
   h.Patch(NativeEditorUIPlugin.Method("PointerAuthoringTime"),new HarmonyMethod(typeof(AirGridRange).GetMethod("PlacementTime",NativeEditorUIPlugin.F)));
   h.Patch(NativeEditorUIPlugin.Method("RefreshAuthoringHover"),postfix:new HarmonyMethod(typeof(AirGridRange).GetMethod("Hover",NativeEditorUIPlugin.F)));
  }
  internal static double SnapValue(AirAuthoringGrid grid,double input){
   if(!AuthoringSafety.Finite(input))throw new ArgumentException("Non-finite X.");double clamped=Math.Max(Low,Math.Min(High,input));double step=grid.Division>0?1d/grid.Division:(double)grid.StepPercent/100;
   double snapped=Math.Round(clamped/step,MidpointRounding.AwayFromZero)*step;snapped=Math.Max(Low,Math.Min(High,snapped));if(Math.Abs(clamped-Low)<Math.Abs(clamped-snapped))snapped=Low;if(Math.Abs(clamped-High)<Math.Abs(clamped-snapped))snapped=High;return snapped;
  }
  static bool Snap(AirAuthoringGrid __instance,double __0,ref double __result){if(!Active)return true;__result=SnapValue(__instance,__0);return false;}
  static bool PlacementTime(StudioBootstrap __instance,Vector2 __0,double __1,ref double __2,ref int __3,ref bool __result){
   if(!Expanded||!__instance.HasDocument)return true;
   string tool=NativeEditorUIPlugin.Get(__instance,"tool").ToString();if(tool!="Sky"&&tool!="FlickLeft"&&tool!="FlickRight")return true;
   if(!(bool)NativeEditorUIPlugin.Call(__instance,"AllowedCanvas",__0))return true;
   object[] plane={__0,true,1,Vector3.zero};if(!(bool)NativeEditorUIPlugin.Call(__instance,"PlanePoint",plane))return true;
   var point=(Vector3)plane[3];var p=__instance.Profile;double x=(point.x/p.centralHalfWidth+1)*.5;
   if(x>=0&&x<=1)return true; // Preserve existing six-lane time picking inside the native grid.
   __result=false;__2=0;__3=1;
   if(x<Low-1e-8||x>High+1e-8||point.z<p.stageNear||point.z>p.stageFar)return false;
   object[] resolve={point.z,__1,0d};if(!(bool)NativeEditorUIPlugin.Call(__instance,"TimeFromDepth",resolve))return false;
   double time=(double)resolve[2];
   if(!(bool)NativeEditorUIPlugin.Get(__instance,"showGrid")){__2=AuthoringGrid.RoundMs(time);__result=true;return false;}
   var grid=(AuthoringGrid)NativeEditorUIPlugin.Get(__instance,"authoringGrid");int division=(int)NativeEditorUIPlugin.Prop(__instance,"Division");double nearest=grid.Nearest(time,division).TimeMs,best=double.PositiveInfinity,chosen=nearest;
   foreach(double candidate in new[]{nearest,grid.Adjacent(nearest,-1,division),grid.Adjacent(nearest,1,division)}){
    float z=(float)NativeEditorUIPlugin.Call(__instance,"Depth",candidate);var a=__instance.ViewCamera.WorldToScreenPoint(new Vector3(StageSpace.AirX(p,Low),p.skyHeight+.019f,z));var b=__instance.ViewCamera.WorldToScreenPoint(new Vector3(StageSpace.AirX(p,High),p.skyHeight+.019f,z));if(a.z<=0||b.z<=0)continue;
    Vector2 start=new Vector2(a.x,Screen.height-a.y),delta=new Vector2(b.x-a.x,a.y-b.y);float along=delta.sqrMagnitude<.001f?0:Mathf.Clamp01(Vector2.Dot(__0-start,delta)/delta.sqrMagnitude);double distance=(__0-start-delta*along).sqrMagnitude;if(distance<best){best=distance;chosen=candidate;}
   }
   __2=chosen;__result=true;return false;
  }
  static bool Pointer(StudioBootstrap __instance,Vector2 __0,double __1,ref double __2,ref bool __result){
   if(!Expanded)return true;var p=__instance.Profile;var camera=__instance.ViewCamera;float z=(float)NativeEditorUIPlugin.Call(__instance,"Depth",__1);var a=camera.WorldToScreenPoint(new Vector3(-p.centralHalfWidth,p.skyHeight+.019f,z));var b=camera.WorldToScreenPoint(new Vector3(p.centralHalfWidth,p.skyHeight+.019f,z));
   __result=false;__2=.5;if(a.z>camera.nearClipPlane&&b.z>camera.nearClipPlane&&Math.Abs(b.x-a.x)>=1){__2=Math.Max(Low,Math.Min(High,(__0.x-a.x)/(b.x-a.x)));__result=true;}return false;
  }
  internal struct Tick {internal double Position;internal bool Label;}
  internal static Tick[] VisibleTicks(AirAuthoringGrid grid,double pixels,double scale,double lo,double hi){
   lo=Math.Max(Low,lo);hi=Math.Min(High,hi);if(hi<lo)return new Tick[0];double step=grid.Division>0?1d/grid.Division:(double)grid.StepPercent/100;
   double spacing=Math.Max(.000001,Math.Abs(pixels)*step);int stride=Math.Max(1,(int)Math.Ceiling(6*scale/spacing)),labels=Math.Max(1,(int)Math.Ceiling(64*scale/spacing));
   int first=(int)Math.Ceiling(lo/step-1e-8),last=(int)Math.Floor(hi/step+1e-8);var points=new SortedDictionary<double,bool>();
   for(long i=(long)Math.Ceiling(first/(double)stride)*stride;i<=last;i+=stride)points[i*step]=false;
   for(long i=(long)Math.Ceiling(first/(double)labels)*labels;i<=last;i+=labels)points[i*step]=true;
   if(lo<=Low&&High>=Low)points[Low]=true;if(hi>=High)points[High]=true;
   var result=new List<Tick>();foreach(var point in points)result.Add(new Tick{Position=point.Key,Label=point.Value});return result.ToArray();
  }
  internal static long HoverQueries,HoverCacheHits;
  internal static float SnapPixels(float scale){return 12*scale;}
  // Grid mode reads rendered segments; free Select mode resolves once per
  // repaint and caches unchanged input, never at mouse-message frequency.
  static void Hover(StudioBootstrap __instance,Vector2 __0){
   if(Event.current!=null&&Event.current.type!=EventType.Repaint)return;
   var ui=NativeEditorUIPlugin.I;if(ui==null||!ui.ready||!ui.CrosshairEnabled||ui.formal||!__instance.HasDocument||!(bool)NativeEditorUIPlugin.Get(__instance,"showUi")||((StudioTransport)NativeEditorUIPlugin.Get(__instance,"transport")).Playing||NativeEditorUIPlugin.Get(__instance,"tool").ToString()!="Select"||(bool)NativeEditorUIPlugin.Prop(__instance,"WorkflowActive")||(bool)NativeEditorUIPlugin.Prop(__instance,"EditHeld")||(bool)NativeEditorUIPlugin.Prop(__instance,"PointerEditGesture")||!(bool)NativeEditorUIPlugin.Call(__instance,"AllowedCanvas",__0))return;
   double time;
   if(!(bool)NativeEditorUIPlugin.Get(__instance,"showGrid")){
    if(ui.TryFreeHoverTime(__0,out time))ApplyHover(__instance,__0,time);return;
   }
   float depth;HoverQueries++;
   if(!InFalsusStudio.Performance.BeatHoverIndex.Pick((NoteRenderer)NativeEditorUIPlugin.Get(__instance,"notes"),__0,SnapPixels((float)NativeEditorUIPlugin.Prop(__instance,"OverlayScale")),out time,out depth))return;
   HoverCacheHits++;ApplyHover(__instance,__0,time);
  }
  static void ApplyHover(StudioBootstrap boot,Vector2 pointer,double time){
   NativeEditorUIPlugin.Set(boot,"guideActive",true);NativeEditorUIPlugin.Set(boot,"guideTime",time);NativeEditorUIPlugin.Set(boot,"guideAir",false);NativeEditorUIPlugin.Set(boot,"hoverPointer",pointer);
  }
 }
 public sealed partial class NativeEditorUIPlugin {
  void GridRangeFields(){
   bool enabled=GUI.enabled;GUI.enabled=enabled&&AirOverflow.Enabled;
   try{MatchedField("空域 X 最小值","field_air_grid_min",DisplayPrecision.Number(AirGridRange.Minimum.Value.ToString("R",CultureInfo.InvariantCulture),4),v=>SetGridRange(v,true),"%");MatchedField("空域 X 最大值","field_air_grid_max",DisplayPrecision.Number(AirGridRange.Maximum.Value.ToString("R",CultureInfo.InvariantCulture),4),v=>SetGridRange(v,false),"%");}finally{GUI.enabled=enabled;}
   Rect row=ContentRow(130);UiParagraph(new Rect(0,row.y+10*scale,405*scale,120*scale),"开启天空超界后，可自定 X 网格范围（百分比）。原轨道为 0～100；范围只影响标尺和吸附，不移动现有物件。",13,Css("#7c9dad"));
  }
  void SetGridRange(string value,bool minimum){double number;if(!double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out number)||!AirGridRange.Valid(minimum?number:AirGridRange.Minimum.Value,minimum?AirGridRange.Maximum.Value:number))throw new ArgumentException(T("X 网格范围须在 -10000～10000 内，最小值小于最大值。"));Call(boot,"CancelGesture");if(minimum)AirGridRange.Minimum.Value=number;else AirGridRange.Maximum.Value=number;}
 }
}
